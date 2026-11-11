# Tooltip de acelerador ("Ctrl+N" flutuando) — causa raiz e correção (11/11/2026)

## Sintoma

Um popup "Ctrl+N" aparecia sozinho sobre a interface e demorava a sumir,
às vezes sobre o texto em edição. Relato original de 30/10 ("pop up chato em
cima das palavras escrito (ctrl+n)") e reincidente em 11/11.

## Causa raiz

Os aceleradores globais de teclado (`Ctrl+N/T/O/S/F/H/P/W`, zoom e `Ctrl+Shift+S`)
estão registrados no **elemento raiz** da janela (`RootGrid`, `MainWindow.xaml`):
é o que torna os atalhos globais — funcionam com o foco em qualquer controle.

Com o `KeyboardAcceleratorPlacementMode` padrão (`Auto`), um elemento que possui
aceleradores exibe a **tooltip do acelerador** quando o mouse fica parado sobre
ele ([UIElement.KeyboardAccelerators](https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.uielement.keyboardaccelerators),
[guia de aceleradores](https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/develop/input/keyboard-accelerators.md)).
Como o RootGrid cobre a janela inteira, basta o mouse parar sobre uma área
**sem controles filhos** (barra de título custom, margens) para a tooltip
"Ctrl+N" surgir ali e persistir vários segundos — do ponto de vista do usuário,
aparece "do nada" e "não some rápido".

## Como foi reproduzido (e por que era difícil)

- A tooltip **não é** um elemento `ControlType.ToolTip` na árvore UIA (o evento
  `ToolTipOpenedEvent` não dispara para ela) e **não aparece** no `PrintWindow`
  da janela: ela renderiza quase toda **acima do topo da janela**.
- Gatilho reproduzido: mouse parado ~1s sobre a barra de título (área nua do
  Grid) → caixa de ~54×34 px com "Ctrl+N" no topo centro da janela, persistindo
  ≥ 5,6 s (captura de tela com margem de 120 px ao redor da janela + pixel-diff:
  457 px alterados no pacote 1.0.7.0 com o defeito).
- Segurar Ctrl sem mover o mouse, hover sobre o editor/tab strip/menu e
  pressionar Ctrl repetidamente **não** reproduzem — por isso as tentativas
  anteriores de reprodução falharam.

## Correção

`MainWindow.xaml`: `KeyboardAcceleratorPlacementMode="Hidden"` no RootGrid.
Suprime **apenas a tooltip**; o roteamento dos atalhos é intocado
([KeyboardAcceleratorPlacementMode](https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.uielement.keyboardacceleratorplacementmode);
comportamento "Page com aceleradores vaza tooltip no hover" também descrito em
[issue #1134](https://github.com/microsoft/microsoft-ui-xaml/issues/1134)).

## Não-regressão

`Tests/automation/accelerators.ps1` (manual, contra o pacote de desenvolvimento
instalado) valida:

1. **Atalhos vivos**: `Ctrl+T` cria aba; `Ctrl+N` com aba alterada abre a
   confirmação de alterações não salvas (prova o roteamento sem ambiguidade —
   com aba limpa ele substitui a aba e a contagem não muda); `Ctrl+F` abre a
   busca (e `Esc` fecha).
2. **Sem tooltip**: mouse parado 5,6 s sobre a barra de título com pixel-diff
   da captura com margem — 457 px no pacote com defeito, 0 px após a correção.

Resultados (11/11/2026, pacote dev 1.0.7.0 → 1.0.8.0, Windows 10.0.19045 x64):
antes `457 px + FALHA`; depois `0 px + ATALHOS+SEM-TOOLTIP PASS`. Regressão de
`theme-sync.ps1`: PASS.
