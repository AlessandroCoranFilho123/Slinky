# AGENTS.md — Guia de trabalho para agentes de IA

Este arquivo é o ponto de entrada para qualquer agente (Codex, Claude, Kimi, GLM/ZCode)
trabalhando neste repositório. Ele resume o essencial e aponta para a documentação
completa. Mantenha-o atualizado ao mudar o ambiente, os comandos ou o estado do projeto.

## O que é o projeto

Slinky é um editor de texto leve para Windows em WinUI 3 (C#/.NET 10), com abas,
cinco temas com contraste WCAG AA, busca/substituição, impressão e recuperação de
conteúdo não salvo. Detalhes: [`README.md`](README.md) e
[`docs/AUDITORIA-ARQUITETURA-E-PLANO.md`](docs/AUDITORIA-ARQUITETURA-E-PLANO.md).

## Mapa do repositório

- `Slinky/` — app WinUI 3 (`Slinky.csproj`, `Package.appxmanifest`, serviços de
  sessão, recovery, arquivos recentes e tema).
- `Tests/` — xUnit headless (`Slinky.Tests.csproj`): persistência, busca, contraste,
  paginação, estados de aba e temas. Nenhum teste instancia janelas WinUI.
- `scripts/` — `Prepare-DevelopmentCertificate.ps1` (cert local `CN=Alessandro`) e
  `Install-Slinky.ps1` (instala bundle de release, exige UAC — publicado como asset `Install-Slinky.ps1`).
- `instalar-slinky.bat` — fluxo local completo: cert + build assinado self-contained +
  registro do cert nas stores de máquina (UAC) + `Add-AppxPackage`.
- `.github/workflows/tests.yml` — CI: testes, builds x86/x64/ARM64, MSIX x64 assinado
  com cert efêmero, smoke de instalação e build trimmed diagnóstico.
- `.github/workflows/release.yml` — packages assinados por arquitetura
  (cert efêmero `CN=Slinky Release`) publicados como release com `Slinky-Release.cer`.

## Ambiente (máquina do Alessandro, verificado em 28/10/2026)

- Windows 10 19045 x64; shell Git Bash.
- .NET SDKs locais: `10.0.401` e `11.0.100-rc.2`; alvo `net10.0-windows10.0.19041.0` (mínimo 17763).
- Apenas Windows PowerShell 5.1 (`powershell.exe`). **Não há `pwsh` nem `gh` instalados.**
- Modo de desenvolvedor ativo (`AllowAllTrustedApps=1`); AppXSvc em execução.
- Certificados de assinatura locais: dois `CN=Alessandro` em `CurrentUser\My` com
  chave privada e EKU de assinatura de código, válidos até 14/10/2027:
  - `307E3EC24E852C4588156FDB999433A1CCC11BC0` (usado na instalação de 28/10;
    também está nas stores de máquina `TrustedPeople` e `Root`)
  - `DF0BAEE0AD5C5F59F40443B4148B77D445B7BCB1`
- O `instalar-slinky.bat` reutiliza o certificado existente mais novo, não cria outro.

## Comandos essenciais

```bash
# Testes (devem passar antes de qualquer commit)
dotnet test Tests/Slinky.Tests.csproj -c Release

# Build Release x64
dotnet build Slinky/Slinky.csproj -c Release -p:Platform=x64
```

MSIX assinado self-contained (receita idêntica à da CI; troque o thumbprint se o
certificado mudou):

```bash
dotnet build Slinky/Slinky.csproj -c Release --no-restore \
  -p:Platform=x64 -p:RuntimeIdentifier=win-x64 \
  -p:GenerateAppxPackageOnBuild=true -p:PublishReadyToRun=false \
  -p:PublishTrimmed=false -p:WindowsAppSDKSelfContained=true \
  -p:AppxPackageSigningEnabled=true \
  -p:PackageCertificateThumbprint=307E3EC24E852C4588156FDB999433A1CCC11BC0
# Saída: Slinky/AppPackages/Slinky_1.0.4.0_Test/*.msixbundle
```

Instalação e lançamento (PowerShell) — leia [`docs/INSTALACAO-MSIX.md`](docs/INSTALACAO-MSIX.md)
antes de instalar pela primeira vez:

```powershell
Add-AppxPackage -Path <bundle> -ForceUpdateFromAnyVersion
Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
```

Avisos de build benignos: `mspdbcmf.exe` não encontrado (pacote de símbolos) e
`PRI249 DESKTOP-EVENTLOG-INSTRUMENTATION`.

## Estado atual (20/11/2026)

- Plano de correções (`docs/PLANO-CORRECOES-E-CHECKLIST.md`): **100% concluído**,
  incluindo o smoke UI completo (automação UIA/SendInput) e a correção de dois
  defeitos encontrados por ele (diálogo duplo `Ctrl+W`, indicador de foco).
- Versão atual: **1.0.12.0** (release `v1.0.8` no GitHub; v1.0.9 a v1.0.12 ainda
  não taggadas — RELEASE-NOTES prontos). A tag `v1.0.6` ficou fixada no commit
  `6207625`, anterior às melhorias de 10/11; mover tag publicada não é opção,
  por isso as versões avançam sem mover tags.
- **Tema: pincéis estáveis (20/11, `docs/TEMA-PINCEIS-ESTAVEIS-2026-11-20.md`)**:
  causa raiz dos bugs de cor (hover do "X" preso, "+" com paleta velha, transições
  no mesmo grupo claro/escuro) era `ThemeService.Apply` criar pincéis novos a cada
  troca — os `{ThemeResource}` dos templates ficam órfãos sem mudança de
  `ActualTheme`. Agora 4 pincéis estáveis apenas mutam `Color`. Removidos:
  re-aplicação dos 117 recursos por troca, truque `RequestedTheme` (TabView/tabItems),
  `ThemeTabStateHub`/`ThemeTabState`. **O "x" da aba selecionada** usa
  `TabViewItemHeaderSelectedCloseButtonBackground` → papel Surface (o estado Selected
  NÃO muda o fundo do header; mapear para hover pintava caixa permanente no "x").
  Validação = **matriz 5×5 de transições** em `Tests/automation/tab-hover.ps1`
  (25/25 PASS com hovers reais, pós-leave do "X", resíduo do tema anterior e
  azul-sistema) — os testes headless de paleta/contraste NÃO cobrem este risco;
  rodar a matriz após qualquer bump do WindowsAppSDK. **Armadilha de automação**:
  WinUI 3 consome WM_POINTER — `SetCursorPos` NÃO dispara hover (usar
  `mouse_event`/SendInput ABSOLUTE em passos); estados de hover com janela inativa
  pintam cores erradas; geometria sempre via UIA `BoundingRectangle` (a janela se
  move entre rodadas e coordenadas fixas medem lugar errado).
- **Auditoria da single-instancing (17/11, `docs/QA-2026-11-17.md`)**: 7 achados
  corrigidos (redirect com timeout+fallback, fila drenada no startup, GUID na
  chave, seleção múltipla, token exato, mutex na fila, **arquivo de 0 bytes
  abre**). Bateria: smoke-e2e reescrito como **orquestrador + powershell filho**
  (`e2e-child.ps1`) — o client UIA do processo que executa `Stop-Process` fica
  amarrado à janela morta e nunca mais lê as janelas recriadas (valor vazio,
  0 edits) — leituras de UIA pós-kill SEMPRE em powershell filho novo; asserts
  de recovery por DISCO (snapshot contém o texto) + foto visual.
- **Dependabot major bump do DWrite REJEITADO (18/11)**: `Microsoft.WindowsAppSDK.DWrite`
  2.6.5 é incompatível com o Runtime 1.8 — a CI bloqueia (`Runtime expected
  version 1.8.25090401`). Os pacotes granulares devem ficar na MESMA linha
  (WinUI/Runtime/DWrite 1.8.x); recusar major bumps isolados de componentes
  (o bump do DWrite só se reconsidera com a linha 2.x do Runtime adotada).
- **Single-instancing (17/11)**: arquivos abertos pelo shell concentram em abas
  na janela principal viva (`AppInstance.GetInstances()` + chave `principal-{pid}`
  + fila em LocalSettings); **Nova janela** usa `--new-window` (argv —
  `args.Arguments` da WinUI vem VAZIO em launches por Process.Start do exe).
  Chave fixa órfã fazia o app sair sem abrir (0 processos, sem crash.log).
  `docs/JANELA-UNICA-ABAS-2026-11-17.md`; E2E `Tests/automation/single-instance.ps1`.
- **Tooltip de acelerador suprimida (11/11)**: os aceleradores globais vivem no
  Grid raiz; com `KeyboardAcceleratorPlacementMode` padrão, o hover sobre áreas
  nuas (barra de título) exibia "Ctrl+N" flutuando. Fix = `Hidden` no RootGrid;
  E2E em `Tests/automation/accelerators.ps1` (detalhe: a tooltip NÃO é elemento
  UIA ToolTip e sai quase toda acima do topo da janela — detectar por pixel-diff
  de captura com margem). `docs/ATALHOS-TOOLTIP-2026-11-11.md`.
- **Associação de arquivos `.txt` FUNCIONANDO** (10/11): causa raiz do bloqueio
  anterior não era o empacotador — o contêiner sob `<Application>` precisa ser
  `<Extensions>` do namespace foundation (`uap:Extensions` não existe nesse nível,
  C00CE014) e o arquivo chega via `FileActivatedEventArgs` (AppLifecycle), não via
  argv. Correção + evidência E2E: `docs/ASSOCIACAO-ARQUIVOS-2026-11-10.md`. Os
  pacotes granulares não projetam o AppLifecycle — há uma `<Reference>` direta a
  `Microsoft.Windows.AppLifecycle.Projection.dll` no csproj (sincronizar versão
  com os pacotes ao atualizar).
- **Tema sincroniza entre instâncias** (10/11): `LocalSettings` é compartilhado por
  pacote; cada janela re-lê no `Activated` (`ThemeSync.Resolve`, puro e testado).
- **Release notes**: fonte única em [`RELEASE-NOTES.md`](RELEASE-NOTES.md) — atualize a
  seção da nova release ANTES de criar a tag (`release.yml` monta o `body` via
  `GITHUB_OUTPUT` — o `body_path` da action v3 é ignorado). Para editar o corpo de uma release já publicada é preciso token com
  escopo `repo` (a credencial do git local não tem — tentativa de 29/10 falhou com 404).
- Decisões de comportamento fechadas no §15 da auditoria (undo/`IsDirty`, arquivo
  alterado externamente, Windows-1252, janelas/processos) e **recuperação
  reconectada ao arquivo original** (tokens FAL nos snapshots).
- O `.csproj` usa **referências granulares do Windows App SDK** (WinUI + Runtime +
  DWrite), sem AI/ML/Widgets — não voltar ao metapacote (~40 MB de payload não
  usado). Fundamento e números: `docs/DESEMPENHO-E-TAMANHO.md`.
- Linha de base de desempenho (máquina local): CPU ociosa 0%, Private Working Set
  ~51–67 MB conforme sessão (piso do WinUI 3 medido: 44,7 MB com app vazio),
  partida aquecida ~0,4 s até janela / ~0,65 s até editor; 204 MB instalados
  (self-contained).
- Pacote instalado nesta máquina:
  `46cebc95-b2f1-4e6a-819f-0227204d1c62_1.0.11.0_x64__1xsa4dc8b90qj`
  (publisher `CN=Alessandro`, assinatura Developer). O AUMID da seção anterior
  corresponde a essa família.
- Histórico e causas raiz dos erros de instalação: `docs/INSTALACAO-MSIX.md`.

## Regras e convenções

- Commits em inglês no formato conventional (`fix:`, `test:`, `chore:`, `docs:`,
  `ci:`), um grupo funcional por commit.
- Documentação em PT-BR. Um item do checklist só é marcado com evidência (código,
  teste aprovado ou registro de smoke com data).
- Produção permanece **sem trimming** (sessão/recovery usam `System.Text.Json`
  reflexivo); build trimmed existe só como diagnóstico na CI.
- Nunca commitar `.cer`/`.pfx` (cobertos pelo `.gitignore`); a CI gera certificado
  efêmero por execução.
- Ao trocar de tema/paleta, rodar os testes de contraste — AA é requisito.

## Documentação de referência

| Arquivo | Conteúdo |
|---|---|
| [`docs/AUDITORIA-ARQUITETURA-E-PLANO.md`](docs/AUDITORIA-ARQUITETURA-E-PLANO.md) | Auditoria completa da arquitetura e plano original |
| [`docs/PLANO-CORRECOES-E-CHECKLIST.md`](docs/PLANO-CORRECOES-E-CHECKLIST.md) | Checklist de execução com evidências por item |
| [`docs/INSTALACAO-MSIX.md`](docs/INSTALACAO-MSIX.md) | Erros de MSIX: causas raiz, evidências e receita de instalação verificada |
| [`docs/SMOKE-UI.md`](docs/SMOKE-UI.md) | Roteiro de smoke test visual a executar no app instalado |
| [`docs/SMOKE-TESTES-2026-10-22.md`](docs/SMOKE-TESTES-2026-10-22.md) | Registro da tentativa de 22/10 e dos bloqueios da época |
| [`docs/SMOKE-TESTES-2026-10-28.md`](docs/SMOKE-TESTES-2026-10-28.md) | Registro de 28–29/10: testes, build, MSIX, instalação e lançamento |
| [`docs/DESEMPENHO-E-TAMANHO.md`](docs/DESEMPENHO-E-TAMANHO.md) | Medições de CPU/RAM/partida/tamanho, experimentos (FDD, GC) e decisões |
| [`docs/QA-2026-10-30.md`](docs/QA-2026-10-30.md) | QA profundo: codificações, arquivos grandes (limites), stress, integridade de dados, segurança |
| [`docs/ASSOCIACAO-ARQUIVOS-2026-11-10.md`](docs/ASSOCIACAO-ARQUIVOS-2026-11-10.md) | Associação `.txt`: causa raiz do C00CE014, rota de ativação por arquivo e evidência E2E |
| [`docs/JANELA-UNICA-ABAS-2026-11-17.md`](docs/JANELA-UNICA-ABAS-2026-11-17.md) | Single-instancing com abas: design, armadilhas (argv vazio, chave órfã) e evidência E2E |
| [`docs/TEMA-PINCEIS-ESTAVEIS-2026-11-20.md`](docs/TEMA-PINCEIS-ESTAVEIS-2026-11-20.md) | Tema: causa raiz (pincéis trocados vs `{ThemeResource}`), pincéis estáveis, matriz 5×5 de transições e limitações |
| [`docs/QA-2026-11-17.md`](docs/QA-2026-11-17.md) | Auditoria da single-instancing: 7 achados corrigidos + bateria completa (byte a byte) |
| [`docs/RACE-AUDIT-2026-10-30.md`](docs/RACE-AUDIT-2026-10-30.md) | Auditoria de corridas: R1/R2 (snapshot ressuscitado), R5 (tmp órfão) |
| [`docs/REVISAO-AUDITORIA-CLAUDE-2026-10-30.md`](docs/REVISAO-AUDITORIA-CLAUDE-2026-10-30.md) | Revisão cruzada de auditoria externa: achados M/H corrigidos |
| [`docs/AUDITORIA-CERTIFICADOS-2026-11-02.md`](docs/AUDITORIA-CERTIFICADOS-2026-11-02.md) | Certificados: decisões, stores, rejeição de major bump isolado |
| [`docs/ATALHOS-TOOLTIP-2026-11-11.md`](docs/ATALHOS-TOOLTIP-2026-11-11.md) | Tooltip "Ctrl+N" flutuante: causa raiz e correção |
