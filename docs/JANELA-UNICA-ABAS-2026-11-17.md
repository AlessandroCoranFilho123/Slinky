# Janela única com abas para arquivos abertos pelo shell (17/11/2026)

## Sintoma

Abrir diferentes `.txt` pelo Explorer (duplo clique/"Abrir com") criava **uma
janela nova por arquivo** — o esperado é concentrar tudo em abas na janela já
aberta (comportamento do Bloco de Notas do Windows 11).

## Causa raiz

Apps WinUI 3 são **multi-instância por padrão**: cada ativação por arquivo
lançava um processo novo com a sua própria janela. O Slinky não implementava o
mecanismo de instância única/redirecionamento do AppLifecycle.

## Design implementado

- A instância que assume a janela registra chave **`principal-{pid}`**
  (`AppInstance.FindOrRegisterForKey`); toda nova ativação de shell procura uma
  principal **viva** (`AppInstance.GetInstances()` filtrando chave e
  `ProcessId` vivo), enfileira o documento e **redireciona**
  (`RedirectActivationToAsync`) em vez de abrir janela, e então sai
  (`Application.Current.Exit()`).
- O documento viaja pela **fila em LocalSettings**
  (`Slinky.Services.ExternalOpenQueue` sobre `ExternalOpenQueuePolicy`, puro e
  testado): o `Data` do redirect não sobrevive a ativações por linha de
  comando, e o LocalSettings é compartilhado por todos os processos do pacote.
  A principal consome a fila no evento `AppInstance.Activated`
  (`DispatcherQueue.TryEnqueue` — o redirect chega em thread de fundo) e abre
  cada caminho via `MainWindow.EnqueueOpenPath` — enfileira enquanto o startup
  restaura sessão/recovery, depois abre imediatamente.
- **"Nova janela" preservado**: o menu lança o exe com **`--new-window`**; a
  instância com esse flag registra chave `janela-{pid}` e NÃO busca principal
  (multi-janela é intencional).

## Armadilhas encontradas (custaram iterações — não repetir)

1. **`LaunchActivatedEventArgs.Arguments` vem VAZIO** em lançamentos por
   `Process.Start` do exe empacotado: o flag `--new-window` nunca era detectado
   e a "Nova janela" redirecionava e morria. Fonte confiável de argv:
   `Environment.GetCommandLineArgs()` (usado como fallback também para o
   caminho do documento).
2. **Chave fixa `"principal"` fica órfã** quando o processo morre de forma
   abrupta (race documentada de auto-unregister): toda ativação seguinte
   redirecionava para a instância morta e o app **saía sem abrir nada**
   (0 processos, sem crash.log — `Application.Current.Exit()` silencioso).
   Correção final (auditoria 17/11): chave com **GUID** (`principal-{pid}-{guid}`)
   — `FindOrRegisterForKey` nunca colide; o alvo é procurado por
   `GetInstances()` + verificação de vida do processo.
3. **`RedirectActivationToAsync` precisa COMPLETAR antes do exit** ("the
   calling app must wait for the method to complete, otherwise the redirection
   will fail") — padrão oficial: redirect em `Task.Run` + `Semaphore`, sem
   bloquear a STA diretamente
   ([doc oficial de instancing](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-instancing)).
   **Auditoria 17/11**: com timeout de 10 s e **fallback** — se o alvo morrer
   entre o check e o redirect, a instância assume a janela em vez de sair
   sem abrir nada.
4. Na projeção do WinAppSDK 1.8, `AppActivationArguments` vive em
   **`Microsoft.Windows.AppLifecycle`** (não `Microsoft.Windows.AppModel`), o
   evento `Activated` é `EventHandler<AppActivationArguments>`, não existe
   `RedirectActivationTo` sync (só `...Async`) e `AppInstance.ProcessId` é
   `uint`.
5. `ExternalOpenQueuePolicy.TakeAll` tolera JSON inválido/obsoleto (lista
   vazia) — um log corrompido nunca pode impedir a abertura de um arquivo.

## Robustez da auditoria de 17/11 (docs/QA-2026-11-17.md)

- redirect com **timeout + fallback** (A1); fila **drenada no startup** em vez
  de descartada (A2); chave com GUID (A3); **seleção múltipla** do Explorer
  abre todos os arquivos como abas (A4); `--new-window` por **token exato** (A5);
  fila protegida por **mutex nomeado** (A6); arquivo de **0 bytes** abre
  (A7 — `CopyToByteArray` pode devolver null). Evidência completa e bateria
  em `docs/QA-2026-11-17.md`.

## Não-regressão e evidência

`Tests/automation/single-instance.ps1` (manual, contra o pacote de
desenvolvimento instalado): abre `a.txt` e depois `b.txt` pela rota FUA
(ProgId) — resultado esperado: **1 processo, 2 abas com os dois arquivos**;
menu Arquivo → Nova janela → **2 processos**. PASS em 17/11/2026 (pacote dev
1.0.10.0, Windows 10.0.19045 x64): `processos=1, abas=[Sem título |
slinky-si-a.txt | slinky-si-b.txt]` → foto
`docs/smoke-fotos/2026-11-17/single-instancia-abas.png`; Nova janela →
`processos=2`. Regressão do smoke da barra de status (mesmo fluxo de
abertura/restore): PASS. Headless: `ExternalOpenQueuePolicy` (8) +
`HasNewWindowFlag` (7 casos), 103 testes verdes.

## Limitação conhecida

Arquivos externos sempre concentram na janela **principal mais antiga viva** —
não na última janela ativa (concentrar na última exigiria re-registro de chave
a cada ativação; decisão documentada aqui, pode ser revisto depois).
