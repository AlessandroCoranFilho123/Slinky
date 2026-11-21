# Tema: pincéis estáveis mutando `Color` — causa raiz e redesign (20/11/2026)

Relato original: hover do "X" de fechar fica ativo mesmo sem o mouse em cima (todos
os temas); hover do "+", cor de fundo da aba, hover da aba e cor do fundo da faixa de
abas quebrando nas trocas de tema — inclusive entre temas do mesmo grupo
(claro/escuro). Várias rodadas de auditoria trataram um sintoma por vez ("+" branco,
hover azul, texto invisível, Dracula fora de sequência) sem tocar a causa.

## 1. Causa raiz

O WinUI só conhece dois temas (Light/Dark); o Slinky tem cinco, e três deles são
"Dark" para o framework. As referências `{ThemeResource}` dentro dos templates de
`TabView`/`TabViewItem`/`TextBox` só são **reavaliadas quando o `ActualTheme` do
elemento muda** — o que não acontece entre temas do mesmo grupo (Dracula→Azulado,
Padrão→Dracula, Padrão→Amarelado).

Enquanto isso, `ThemeService.Apply` criava **pincéis novos** (`new SolidColorBrush`)
a cada troca (antes em `ThemeService.cs:108-111`). Toda referência já resolvida por
um template continuava apontando para a instância antiga — daí os sintomas mudarem
conforme a ordem da sequência de trocas.

O código contornava isso com força-bruta, que aumentava a superfície do bug:

- reescrever as 117 chaves de recursos (`RecursosDeCorAba`) a cada troca, em dois
  escopros (`NotesTabView.Resources` e por `tabItem.Resources`);
- truque `RequestedTheme = Default; RequestedTheme = tema` no `TabView` (para o "+")
  e em cada `tabItem` (para os visual states) — frágil: `Default→Dark` pode resultar
  em `ActualTheme` idêntico nas duas atribuições, e a churn de tema em cima do
  pointer desincroniza o estado visual (o hover do "X" preso);
- `ThemeTabStateHub` para notificar cada aba materializada.

Os testes headless (paleta/contraste) nunca pegaram isso porque validam a tabela de
cores, que nunca foi o problema — a entrega das cores para os templates era.

## 2. Design novo

**Nunca trocar o pincel.** A paleta tem exatamente 4 papéis
(`ThemePalette.cs`: Background, Foreground, Surface, Accent), então 4 instâncias de
`SolidColorBrush` bastam. `ThemeService` cria as 4 uma única vez e `Apply` apenas
muta `brush.Color`. `SolidColorBrush.Color` é uma dependency property: qualquer
elemento que segure a instância é redesenhado sem reavaliar `{ThemeResource}`,
independentemente de `ActualTheme` ou da ordem das trocas.

Pontos de instalação (todos uma única vez, apontando para os pincéis estáveis):

| Onde | O quê |
|---|---|
| `MainWindow.InstallTabBrushesOnce()` (chamado no construtor) | as 117 chaves de `RecursosDeCorAba` em `NotesTabView.Resources` — escopo de TabView cobre as abas materializadas antes/depois **e** o botão "+" (que vive no template do TabView, fora de todo tabItem) |
| `AddNoteTab` | `tabItem.Background`/`Foreground` recebem as instâncias estáveis na criação |
| `NotePage.InstallEditorBrushes()` (construtor) | as 5 chaves do template do TextBox (`TextControlBackground*`, `TextControlForegroundPointerOver`) |

`root.RequestedTheme` (e o `RequestedTheme` dos diálogos) continua existindo **apenas**
para o chrome de sistema (menus nativos, scrollbars, diálogos), que distingue só
Light/Dark — e essa troca é uma mudança real de `ActualTheme`, que o framework trata
bem.

## 3. O que foi removido

- `AplicarRecursosAba` + `ApplyTabItemTheme` (re-aplicação por aba a cada troca).
- Os dois truques `RequestedTheme Default→tema` (TabView e tabItems).
- `ThemeTabState.cs` inteiro: `ThemeTabStateHub` (único consumidor era MainWindow) e
  o record struct `ThemeTabState` (`Apply` agora lê `ThemePalette` direto).
- Reaplicação de `EditorSurface`/`NoteEditor.Background/Foreground` em
  `NotePage.ApplyTheme` (redundante com os `x:Bind` do XAML).
- Texto "Todas as abas e janelas abertas atualizam na hora." da janela de tema
  (era falso pavimentar com os bugs acima).
- Testes do hub/struct (`ThemeTabStateTests`, `ThemeTabStateNotificationTests`,
  `ThemeSelectionWorkflowTests`); o assert útil de `ThemePalette.IsDark` virou
  `Tests/ThemePaletteTests.cs`. Contraste WCAG AA (`ContrastCalculatorTests`)
  intocado.
- Bônus: `Activated` agora filtra `WindowActivationState.Deactivated` (não re-lê
  `LocalSettings` ao perder foco).

Resultado: o diff **diminui** — saíram ~290 linhas, entraram ~90.

## 4. Validação

- **Spike** (gate antes de investir no resto): Dracula→Azulado com 3 abas, hover em
  cada aba/close/"+" via `tab-hover.ps1` com `SO_TRANSICAO`. PASS — o
  `ListViewItemPresenter` do TabViewItem repinta quando `Color` muda; o plano B
  (retemplate do TabViewItem) não foi necessário.
- **Matriz 5×5** (`Tests/automation/tab-hover.ps1`, 20/11): as 25 transições
  de→para, cada uma medindo hover nas 3 abas, hover do "X" da aba 3 e **pós-leave**
  (mouse em área neutra — o sintoma relatado), "+" normal/hover e fundo do strip.
  Critérios por transição: moda da região = paleta do tema de destino (±12),
  0 px de azul-accent do sistema, 0 px de resíduo da paleta do tema anterior no
  strip. **25/25 PASS** — fotos em `docs/smoke-fotos/2026-11-20/`.
- **Sync entre instâncias**: `theme-sync.ps1` PASS (troca em B, ativação de A,
  fundo pixel-exato).
- **Headless**: 104 testes aprovados (`dotnet test Tests/Slinky.Tests.csproj -c Release`).
- Correções colaterais na automação: a tabela de paletas do script tinha as surfaces
  de Amarelado/Azulado/Dracula duplicadas com os backgrounds (asserts mais fracos);
  `PostMessage` não existia no Add-Type e a janela de tema nunca fechava (janelas
  órfãs empilhadas).

## 5. Follow-up do mesmo dia: o "x" da aba selecionada sempre "ativo"

Residual do relato original, confirmado pela matriz com hovers reais. Leitura do
`generic.xaml` do WinUI 1.8 (`Microsoft.WinUI/Themes/generic.xaml`, template do
TabViewItem):

- Os estados **Selected/PointerOverSelected/PressedSelected** aplicam
  `<Setter Target="CloseButton.Background" Value="{ThemeResource
  TabViewItemHeaderSelectedCloseButtonBackground}"/>`, mas **não** mudam
  `TabContainer.Background` — o header selecionado mantém
  `TabViewItemHeaderBackground` (Surface); o visual de "selecionada" vem do "pill"
  (`SelectedBackgroundPath.Fill = TabViewItemHeaderBackgroundSelected`) + borda +
  negrito.
- A tabela de 117 recursos mapeava essa chave para o papel 1 (cor de hover) por
  associação de nome — resultado: a caixa de hover **permanente** no "x" da aba
  selecionada, em todos os temas (o sintoma "x fica sempre ativo").
- **Fix**: `TabViewItemHeaderSelectedCloseButtonBackground` → papel 0 (Surface). O
  "x" mistura com o header nos três estados; o hover do próprio "x"
  (`TabViewItemHeaderCloseButtonBackgroundPointerOver`, papel 1) dá o feedback
  visível e o `PointerExited` reverte ao fundo da aba.

Armadilhas de automação descobertas nesta rodada (explicam por que as matrizes
antigas passavam sem medir nada real):

1. **`SetCursorPos` não dispara hover no WinUI 3**: o framework consome a pilha de
   entrada **WM_POINTER**; `SetCursorPos` move o cursor sem gerar evento de entrada.
   Movimento sintético válido = `mouse_event`/`SendInput` com `MOUSEEVENTF_ABSOLUTE`
   em passos (~40 ms). Com `SetCursorPos`, TODAS as verificações de hover mediam a
   cor de repouso e passavam vacuamente.
2. **Estados de hover com janela inativa pintam cores erradas** (medido: 22/43 em
   vez de 30/40 no Escuro Padrão) — garantir ativação (clique real como fallback do
   foreground lock) antes de medir.
3. **A janela se move entre rodadas**: nunca fixar origem de captura — usar
   `GetWindowRect` do handle real a cada execução; posições de abas/"x"/"+" vem de
   `BoundingRectangle` (UIA), não de coordenadas fixas (a matriz anterior chegou a
   rodar com 1 aba e os hovers em abas 2/3 e "+" caíam na strip vazia).
4. **`$failures +=` dentro de função do PowerShell 5.1 cria cópia local** — os
   failures dentro de `MedirEstados` eram engolidos; usar `$script:failures`.
5. A garantia de 3 abas é verificada por UIA (contagem de `ControlType.TabItem`)
   com retry de `Ctrl+T` antes de medir qualquer coisa.

## 6. Limitações conhecidas / follow-ups

- **MenuBar, flyouts e ContentDialog** continuam com cores de sistema (Light/Dark
  correto via `RequestedTheme`, mas não a paleta exata em Dracula/Azulado). Não
  constavam no relato; a solução é a mesma (chave → pincel estável) e pode ser
  adicionada ao `InstallTabBrushesOnce`/recursos do diálogo quando necessário.
- A tabela `RecursosDeCorAba` depende do `generic.xaml` do WinUI 1.8; um bump
  minor/patch do WindowsAppSDK pode introduzir chaves novas que resolvam para
  accent de sistema. A matriz 5×5 detecta isso (azul-sistema/resíduo) — rodá-la
  após qualquer atualização do SDK.
- A troca de tema feita por **outro processo** enquanto a janela está em segundo
  plano só sincroniza na ativação (limitação documentada; `Activated` agora ignora
  só a desativação).
