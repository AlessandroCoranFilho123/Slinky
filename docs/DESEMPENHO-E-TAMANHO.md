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
