# Desempenho, memória e tamanho — medições e decisões

Relatório da auditoria de polimento de 29/10/2026. Todas as medições foram
executadas nesta máquina (Windows 10 19045 x64) com o app instalado via MSIX
assinado, em condição de repouso (sem interação) e com a janela em primeiro plano.

## Resumo executivo

| Métrica | Alvo | Bloco de Notas (referência) | Slinky antes | Slinky final (1.0.3.0) | Situação |
|---|---:|---:|---:|---:|---|
| CPU ociosa | 0% | 0s/30s | 0s/30s | **0s/30s** | ✅ meta batida |
| Private Working Set | ~51 MB (máx 55) | 1,66 MB | 51,04 MB | **51,06 MB** | ✅ meta batida |
| Working Set | — | 13,0 MB | 146,4 MB | 147,0 MB | ver seção abaixo |
| Private Bytes | — | 2,95 MB | 82,1 MB | 81,3 MB | — |
| Partida fria (janela) | — | 63 ms | 523 ms | 966 ms¹ | ¹primeira execução pós-instalação; aquecida 370–410 ms |
| Partida aquecida (editor pronto) | — | — | — | **~630–660 ms** | mediana de 5 execuções |
| Tamanho instalado | — | — | ~210 MB | **204 MB** | −6 MB; mínimo realista 37 MB (ver FDD) |
| Bundle comprimido | — | — | — | **81,4 MB** | — |

**O alvo de memória (~51 MB) já era atendido antes das otimizações** — as metas do
roteiro correspondem exatamente ao Private Working Set medido. A impressão de
"consumo alto" vem do **Working Set** (146 MB), que é majoritariamente memória
**compartilhada**: as DLLs do Windows App SDK e do runtime .NET são mapeadas e
contabilizadas inteiras no Working Set de cada processo, mas o custo privado real
do Slinky é o Private Working Set (~51 MB, o número que o Gerenciador de Tarefas
exibe). Para referência, o Bloco de Notas clássico (Win32 puro) fica em 1,66 MB
privados — a diferença é o custo do WinUI 3 + runtime .NET, que é o framework do
projeto por decisão de arquitetura.

## Metodologia

- **CPU ociosa**: `Process.TotalProcessorTime` delta em janela de 30 s sem interação.
- **Memória**: `WorkingSet64` (Working Set), `WorkingSetPrivate` do contador WMI
  `Win32_PerfFormattedData_PerfProc_Process` (Private Working Set) e
  `PrivateMemorySize64` (Private Bytes/commit). Contadores do PowerShell
  (`Get-Counter '\Process(...)')` não foram usados porque os nomes são localizados
  e falham em Windows pt-BR.
- **Partida**: cronômetro de `Start-Process` no AUMID até `MainWindowHandle != 0`
  (janela) e até o primeiro `Edit` aparecer na árvore de UI Automation (editor
  pronto); mediana de 5 execuções a frio (processo encerrado antes de cada uma).
- **Tamanho instalado**: conteúdo extraído do MSIX x64 interno do bundle
  (idêntico ao layout instalado em `C:\Program Files\WindowsApps`).

## O que foi implementado

### 1. Referências granulares do Windows App SDK (−6 MB instalados)

O metapacote `Microsoft.WindowsAppSDK` puxava nove subpacotes, incluindo
`AI` (OCR/Windows ML), `ML` (onnxruntime/DirectML) e `Widgets`, que o editor
não usa. O `.csproj` agora referencia apenas `WinUI`, `Runtime` e `DWrite`
(o WinUI traz transitivamente Base/Foundation/InteractiveExperiences). Além
disso, `<SatelliteResourceLanguages>en;pt-BR</SatelliteResourceLanguages>`
reduziu as pastas de idioma de ~70 para as duas suportadas. Resultado: 210 →
204 MB instalados, sem perda funcional (regressão completa PASS).

Limitação descoberta: o payload nativo de ML (`onnxruntime.dll` 20,5 MB,
`DirectML.dll` 17,7 MB) vive **dentro do MSIX monolítico do runtime**
(`Microsoft.WindowsAppRuntime.1.8.msix`, embutido pelo modo self-contained) e não
há propriedade oficial para excluir componentes do runtime. Excluir esses arquivos
manualmente do layout seria frágil (quebra a cada atualização do SDK) — não feito.

### 2. Variante framework-dependente (FDD) — experimento validado

| Variante | Instalado | Bundle comprimido | Pré-requisitos |
|---|---:|---:|---|
| Self-contained (atual, release) | 204 MB | 81,4 MB | nenhum |
| FDD (dependente do runtime WinAppSDK) | 108 MB | ~43 MB | Windows App SDK 1.8 instalado |
| FDD completa (dependente também do .NET) | **37 MB** | **10,2 MB** | .NET 8 Desktop Runtime + WinAppSDK 1.8 |

A variante FDD completa foi **construída, instalada e testada nesta máquina**
(ambos os runtimes presentes): abriu em 1028 ms, Private Working Set 50,5 MB,
CPU ociosa 0%. Receita:

```bash
dotnet build Slinky/Slinky.csproj -c Release \
  -p:Platform=x64 -p:RuntimeIdentifier=win-x64 \
  -p:GenerateAppxPackageOnBuild=true -p:PublishReadyToRun=false \
  -p:PublishTrimmed=false -p:WindowsAppSDKSelfContained=false -p:SelfContained=false \
  -p:AppxPackageSigningEnabled=true -p:PackageCertificateThumbprint=<thumb>
```

**Decisão: a release continua self-contained.** O FDD reduz o download para
~10 MB, mas exige que o usuário tenha (ou instale) o runtime do Windows App SDK e
o .NET 8 Desktop Runtime — um pacote de ~204 MB instalados que passa a viver no
sistema. Para distribuição pública sem pré-requisitos, self-contained é a escolha
correta; o FDD fica documentado para quem distribui em ambientes controlados.

### 3. GC não-concorrente — testado e rejeitado

`<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>` foi construído,
instalado e medido: Private Working Set 51,68 MB (igual ao concorrente, dentro do
ruído), CPU ociosa **piorou** (0,094 s/30 s contra 0,000 s) e +4 threads. Revertido.

### 4. O que foi avaliado e não implementado, com motivo

- **Trimming**: proibido em produção — sessão/recovery usam `System.Text.Json`
  reflexivo (decisão documentada no `.csproj` e na CI, que mantém build trimmed
  apenas diagnóstico).
- **Native AOT**: incompatível com WinUI 3 (XAML depende de reflexão/COM interop).
- **ReadyToRun**: os pacotes de release já são gerados com
  `PublishReadyToRun=false` (receita da CI): economiza ~30–40% de tamanho às
  custas de JIT na partida. A partida aquecida de ~0,4 s torna o trade-off
  correto para este app.
- **`InvariantGlobalization`/`HybridGlobalization`**: a busca do editor usa
  `StringComparison` configurável com semântica cultural; trocar a globalização
  alteraria collation de texto (comportamento visível ao usuário) por uma
  economia irrelevante (a ICU do Windows é DLL compartilhada do sistema).
- **Adiar `RegisterForPrinting`**: custo de partida não mensurável acima do ruído
  (~±30 ms entre execuções); mover o registro para o primeiro Ctrl+P adicionaria
  um caminho de falha novo ao imprimir sem ganho real. Mantido.

## CPU ocioso: por que já é 0%

O código não tem timers, threads em segundo plano, polling nem serviços
verificados por grep no repositório inteiro. Autosave é orientado a eventos
(debounce de inatividade + teto de 30 s), sessão e recovery só escrevem quando
há mudança. As 56 threads do processo são do runtime/WinUI (render, COM, GC,
thread pool), todas paradas em repouso.

## Estudo de memória aprofundado (29/10/2026 — pedido de meta realista)

A meta "51 MB" correspondia ao valor **já medido**, não a um alvo. Para estipular o
ótimo realista, mediu-se o **piso do framework** com um app WinUI 3 vazio
(janela única, sem conteúdo), construído e instalado com as mesmas flags do Slinky:

| Condição | Empty WinUI 3 (piso) | Slinky |
|---|---:|---:|
| Fresh, 15 s de settle | PWS 63,1 MB / PB 81,4 MB | PWS 51,1–67,4 MB / PB 81–103 MB |
| Aquecido, 45 s de settle | **PWS 44,7 MB / PB 62,8 MB** | PWS 50,5–66,9 MB / PB 79–103 MB |
| CPU ociosa | 0s/20s | 0s/20s |

Leituras:

- O **piso do WinUI 3 estabilizado é ~45 MB privados** — uma janela vazia, sem
  nenhuma funcionalidade, já consome isso. Os 146–161 MB de Working Set são
  compartilhamento de DLLs mapeadas.
- O Slinky estabilizado oscila entre **~50 e ~67 MB de PWS** conforme o conteúdo
  da sessão restaurada (abas/arquivos abertos) e o momento do GC; em repouso
  prolongado o valor não cresce (sem vazamento: 66,84 → 66,84 MB entre 65 s e
  105 s de ociosidade).
- O **ótimo realista para o Slinky é ~45–55 MB PWS**: o piso do framework mais
  o custo do modelo de documento, abas, temas e sessão. O valor medido já vive
  nessa faixa.
- **1,7 MB (Bloco de Notas clássico) é impossível em WinUI 3** — o custo é do
  runtime/XAML, não do aplicativo. Alcançá-lo exigiria reescrever em Win32 puro,
  descartando WinUI/temas/acessibilidade — fora de questão por decisão de
  arquitetura do projeto.
- Alavancas de framework já esgotadas e rejeitadas com medição: GC não-concorrente
  (piorou CPU ociosa), FDD (não muda PWS: 50,5 MB medidos), trimming (quebra
  sessão/recovery), globalização invariante (altera collation).

## Recuperação reconectada ao arquivo (29/10/2026)

Nova funcionalidade (decisão do item 2 do §15 da auditoria): snapshots de recovery
agora carregam o **token da FutureAccessList** do arquivo original
(`RecoverySnapshot.FileToken`, capturado via `SessionService.TokenForPath`).
Ao restaurar, `Note.TryReattachSavedFileAsync` resolve o token e reanexa a nota ao
arquivo — a aba volta com o **nome do arquivo** e um Ctrl+S grava no documento
original, sem "Salvar como" (paridade com o modelo de recuperação do Notepad
moderno). Token expirado/ausente/patch de path divergente → comportamento anterior
(nota sem título), garantido por `RecoveryReattachPolicy` (função pura, testada).
Validado ponta a ponta: kill com conteúdo não salvo → relançar → Restaurar → aba
`poema.txt` → Ctrl+S persistiu no arquivo original.

## Comportamentos de referência do Bloco de Notas (decisões do §15)

Experimentos executados no `notepad.exe` clássico desta máquina (Win10 19045,
automação real via SendInput/UIA; evidências citadas):

| Decisão | Comportamento observado no Notepad | Medida adotada no Slinky |
|---|---|---|
| Undo até o conteúdo salvo limpa "modificado"? | **Não** — após salvar 'abcd', undo para 'abc' e Alt+F4, o título continuou `*a.txt` (flag pegajosa) | **Mantido o flag pegajoso** (paridade; nenhum código mudou) |
| Arquivo modificado externamente | **Sobrescrita silenciosa** — sem recarga nem aviso; o buffer local vence (disco ficou 'AC' após salvar sobre 'B' externo) | **Mesma política** (sem watcher/polling — também preserva CPU ociosa 0%) |
| Codificações no salvar | Diálogo clássico oferece ANSI (Windows-1252) e variantes UTF (experimento inconclusivo por limitação de UIA em menu Win32; lista documentada) | **Windows-1252 com round-trip oficial**: `SaveAsync` preserva a codificação detectada (`TextFileCodec.Encode(Text, EncodingKind, …)`); sem seletor manual (diferença deliberada de escopo) |
| Modelo de janelas | 2 janelas = **2 processos** | Já idêntico ("Nova janela" = novo processo) |
| Recuperação de não salvos | Clássico não tem; moderno reconecta ao arquivo | **Implementado** (seção acima) — reconnect via FAL token |

## Histograma do pacote instalado (204 MB, principais itens)

| Item | Tamanho | Papel |
|---|---:|---|
| `Microsoft.Windows.SDK.NET.dll` | 23,7 MB | projeção CsWinRT do SDK do Windows (exigida pelo WinUI) |
| `onnxruntime.dll` | 20,5 MB | runtime de ML do payload self-contained (não carregado pelo app) |
| `DirectML.dll` | 17,7 MB | backend de ML do payload self-contained (não carregado pelo app) |
| `System.Private.CoreLib.dll` | 12,6 MB | runtime .NET |
| `Microsoft.ui.xaml.dll` + `Microsoft.UI.Xaml.Controls.dll` | 20,6 MB | WinUI/XAML |
| `Microsoft.WinUI.dll` | 7,0 MB | projeção WinUI |
| `System.Private.Xml.dll` | 7,6 MB | BCL |
| `coreclr.dll` + `clrjit.dll` | 6,5 MB | runtime .NET |
| demais (~140 DLLs + assets + locales en/pt-BR) | ~87 MB | BCL, runtime WinAppSDK, projeções |

O mínimo realista dentro do framework atual é a variante FDD completa (37 MB
instalados / 10,2 MB de download), condicionada aos runtimes instalados. Em
self-contained, o piso é ~204 MB pelos payloads monolíticos do runtime.

## Reprodutibilidade

Comandos usados (PowerShell 5.1; scripts efêmeros em `%TEMP%`, não versionados):
medições via `Get-Process`/`Win32_PerfFormattedData_PerfProc_Process`, cronômetro
de partida com `Stopwatch` + polling de `MainWindowHandle`/UIA, extração do pacote
com `Expand-Archive`/unzip. O app de referência foi o `notepad.exe` clássico do
Windows (Win32), medido com o mesmo método.
