# Smoke tests — 28/10/2026

## Ambiente

- Windows com .NET SDK `11.0.100-rc.2.26504.105` (runtime alvo `net8.0-windows10.0.19041.0`).
- Runtime alvo: `win-x64`.

## Resultados executados

| Verificação | Resultado |
|---|---|
| `dotnet test Tests/Slinky.Tests.csproj -c Release` | Aprovado: 72/72 (inclui persistência, concorrência, contraste e paginação) |
| Build Release x64 via `dotnet build` | Aprovado: 0 erros, 0 avisos |
| Geração de MSIX x64 (`GenerateAppxPackageOnBuild`, SideloadOnly) | Aprovado: bundle gerado |
| Auditoria de assets do pacote MSIX | Aprovada: 56 assets; todos os logos referenciados pelo manifesto presentes |
| `git diff --check` | Aprovado |

## Auditoria de assets do manifest

Após remover do `.csproj` as entradas `None Remove` mortas de `BadgeLogo` e excluir
os assets sem referência (`LockScreenLogo.scale-200.png`, `StoreLogo.backup.png`),
o bundle foi recriado e inspecionado:

- Presentes: `StoreLogo`, `BadgeLogo` (escalas 100–400), `LargeTile`, `SmallTile`,
  `Wide310x150Logo`, `SplashScreen`, `Square150x150Logo`, `Square44x44Logo`
  (escalas e variantes unplated) e `AppIcon.ico` (carregado em runtime por
  `WindowIconService`).
- Ausentes, como esperado: `StoreLogo.backup.png` e `LockScreenLogo.scale-200.png`,
  que antes eram empacotados indevidamente pelo globbing do MSIX.

O único aviso do build foi ambiental (`mspdbcmf.exe` ausente impede o pacote de
símbolos) e não afeta o aplicativo.

## Instalação MSIX e lançamento (complemento da mesma data)

O bloqueio registrado em `SMOKE-TESTES-2026-10-22.md` foi resolvido no mesmo dia
28/10, com diagnóstico pelo log de eventos do AppX. Resumo (detalhes e causas raiz
em [`INSTALACAO-MSIX.md`](INSTALACAO-MSIX.md)):

1. Build assinado com a receita da CI (`WindowsAppSDKSelfContained=true` +
   `PackageCertificateThumbprint` do certificado local `CN=Alessandro`).
2. Importação do certificado para `LocalMachine\TrustedPeople` e `Root` com UAC
   aprovado — a confiança só no usuário não basta para o AppXSvc.
3. Remoção da família antiga do release (`…_b2zjezss9gtqm`, publisher
   `CN=Slinky Release`) que conflitava com a família dev (`0x80073CF3`).
4. `Add-AppxPackage` aprovado: pacote
   `46cebc95-…_1.0.0.0_x64__1xsa4dc8b90qj` instalado (assinatura Developer).
5. Lançamento via `shell:AppsFolder\…!App` confirmado: processo em execução
   com janela ativa.

## Testes de interface (automatizados via UIA)

Executados no app instalado, via Windows UI Automation + SendKeys do PowerShell 5.1
(scripts efêmeros em `%TEMP%`, sem alteração do repositório). Onde o roteiro pede
observação visual (contraste, indicador de foco), ficou pendente.

| Cenário (`SMOKE-UI.md`) | Resultado | Evidência |
|---|---|---|
| 1.1 Digitar sem clicar após abrir | PASS | texto digitado direto no editor |
| 1.2 Múltiplas linhas com `Enter` | PASS | `ValuePattern` devolve `\r` (cód. 13) entre linhas — o WinUI reporta CR, não CRLF |
| 1.6 `Ctrl+Z` após apagar tudo | PASS | conteúdo restaurado integralmente |
| 3.1 `Ctrl+F` foca o campo de busca | PASS | elemento focado passa a ser o `Edit` da busca |
| 3.2 Foco permanece na busca incremental | PASS | campo com 'segunda' após digitação |
| 3.5 `Esc` devolve foco ao editor e preserva conteúdo | PASS | foco no editor, texto intacto |
| 4. `Ctrl+T` nova aba editável | PASS | 2 abas, digitação na nova |
| 4. `Ctrl+W` em aba vazia fecha sem diálogo | PASS | aba removida |
| 4. `Ctrl+W` com conteúdo abre confirmação | PASS | botões Salvar / Não salvar / Cancelar |
| 4. Cancelar mantém aba e conteúdo | PASS | `Esc` no diálogo preservou tudo |
| 4. "Não salvar" descarta e fecha | PASS | aba fechada via invoke do botão |
| 4. Zoom `Ctrl+=`, `Ctrl+-`, `Ctrl+0` (teclado principal) | PASS | aceleradores OEM em runtime (fix 03c946f): processo vivo e editor editável após cada |
| 4. Zoom numpad (`Add`/`Subtract`/`NumPad0`) | NÃO TESTÁVEL | `WScript.SendKeys` não envia teclas numpad; fica para execução manual |
| 4. `Ctrl+O`/`Ctrl+S`/`Ctrl+Shift+S`/`Ctrl+P`/menus | PENDENTE | abrem diálogos de arquivo/impressão; execução manual |
| 1.x Cenários de clique do mouse | PENDENTE | UIA não simula clique posicional; manual |
| 5. "Nova janela" cria processo separado | PASS | PIDs distintos; B editável isoladamente |
| 5. Fechar B não afeta A | PASS | A continuou editável após fechamento de B |
| 5.4 Recovery após kill forçado | PASS | prompt "Notas não salvas encontradas" → Restaurar → 3 abas com o conteúdo exato de cada processo morto (`conteudo-janela-B`, `RECOVERY-SMOKE-2810`, `ab\rcd`); snapshots em `LocalState\Recovery\` por aba |
| 6. Percorrer os cinco temas | PASS | janela "Configurar tema" com 5 opções; todas trocadas sem travar; voltou ao Padrão |
| 2/6. Indicador de foco e contraste nos cinco temas | PENDENTE | verificação visual; manual |

### Defeito descoberto: `Ctrl+W` com diálogo já aberto

`crash.log` (28/10 19:56:58Z) registrou `COMException 0x80000019 — "Only a single
ContentDialog can be open at any time"` em `FecharAbaAccelerator_Invoked →
ConfirmCloseNoteAsync → ContentDialog.ShowAsync()`: pressionar `Ctrl+W` enquanto a
confirmação de fechamento já está aberta tenta abrir um segundo diálogo em vez de
ser bloqueado (o item 14 do plano previa o bloqueio). O tratamento de exceções
registrou e o app seguiu vivo — mas o fechamento da aba falha silenciosamente nessa
condição. Foi também a causa dos fechamentos intermitentes durante os testes.

### Limitações do método

- Só o editor da aba selecionada aparece na árvore UIA (o `TabView` descarrega as
  demais); verificação por aba exigiu selecioná-las uma a uma.
- A validação cruzada de duas janelas simultâneas com autosave competindo (item 5.5
  completo) ficou marcada como PASS parcial: snapshots de processos distintos
  coexistiram e todos foram restaurados, mas o cenário "B vivo enquanto A restaura"
  não foi exercitado de forma determinística.
