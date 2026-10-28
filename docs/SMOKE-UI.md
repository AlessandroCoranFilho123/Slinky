# Roteiro de smoke test da interface

Este roteiro valida comportamentos que os testes headless não conseguem observar. Execute-o em uma instalação MSIX funcional do Slinky e registre a data, a versão do Windows, a arquitetura e o resultado de cada cenário. Não marque um cenário como aprovado apenas porque o teste unitário correspondente passou.

## Preparação

- Instalar e iniciar o pacote MSIX em uma máquina Windows com Windows App SDK funcional.
- Usar inicialmente o tema Claro Padrão e uma janela sem diálogos abertos.
- Preparar um arquivo de texto com pelo menos três linhas e palavras repetidas.
- Para os cenários de recovery e múltiplos processos, anotar os PIDs das janelas.

## 1. Edição, caret e cliques

- [ ] Abrir uma aba vazia e digitar imediatamente, sem clicar no editor. O texto deve aparecer na primeira linha.
- [ ] Pressionar `Enter` duas vezes, digitar texto nas três linhas e clicar em posições diferentes de cada linha. O caret deve seguir o ponto clicado, sem saltar para o final do documento.
- [ ] Dar duplo clique em uma palavra. Somente essa palavra deve ser selecionada.
- [ ] Clicar na área visual abaixo da última linha. O editor deve receber foco sem criar linhas, alterar o texto ou deslocar o caret de forma inesperada.
- [ ] Clicar fora do editor e voltar a clicar no texto. A edição deve continuar normalmente.
- [ ] Usar `Ctrl+Z` e `Ctrl+Y` após edições e substituições. O histórico nativo deve permanecer utilizável.

## 2. Navegação por teclado e indicador de foco

- [ ] Sair do editor com `Tab`, percorrer os controles e retornar ao editor somente pelo teclado.
- [ ] Confirmar que o editor apresenta o sublinhado de foco na cor de destaque ao receber foco por teclado.
- [ ] Retirar o foco e devolvê-lo com o mouse. O clique não deve apagar texto nem exigir um segundo clique para editar.
- [ ] Repetir a navegação nos cinco temas. O indicador deve permanecer visível sobre cada superfície.
- [ ] Abrir a busca com `Ctrl+F` e percorrer campo, botões e opções com `Tab` e `Shift+Tab`. A ordem deve ser previsível e o foco deve permanecer identificável.

## 3. Busca e substituição

- [ ] Abrir a busca com texto selecionado no editor. O campo deve receber a seleção e manter o foco.
- [ ] Digitar um termo incrementalmente. O foco deve permanecer no campo enquanto as ocorrências são atualizadas.
- [ ] Usar `Enter`, **Próximo** e **Anterior**, incluindo o retorno à primeira/última ocorrência.
- [ ] Abrir **Localizar e substituir**, alternar **Maiúsc/minúsc**, substituir uma ocorrência e depois todas.
- [ ] Fechar a busca com `Esc`. O foco deve retornar ao editor, preservando um caret ou seleção válidos.

## 4. Atalhos e fechamento

- [ ] Validar `Ctrl++` e `Ctrl+-` no teclado principal e no numérico, além de `Ctrl+0` e `Ctrl+NumPad0`.
- [ ] Validar `Ctrl+N`, `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`, `Ctrl+W`, `Ctrl+F`, `Ctrl+H` e `Ctrl+P`.
- [ ] Modificar uma nota e abrir a confirmação de fechamento. Enquanto o diálogo estiver aberto, atalhos não devem executar comandos na janela ao fundo.
- [ ] Testar separadamente salvar, descartar e cancelar. Cancelar deve manter a aba e o conteúdo modificados.

## 5. Sessão, recovery e dois processos

- [ ] Abrir duas janelas por **Nova janela**, anotar os PIDs e manter arquivos diferentes abertos em cada uma.
- [ ] Fechar normalmente apenas uma janela. A outra deve continuar editável e sua sessão/recovery não deve ser apagado.
- [ ] Encerrar os dois processos e reabri-los. Cada sessão deve restaurar somente os documentos do processo correspondente, sem duplicar abas.
- [ ] Em uma nova janela, criar conteúdo não salvo, aguardar o autosave e encerrar o processo sem confirmação. Ao reabrir, o conteúdo deve ser recuperado uma única vez.
- [ ] Repetir com duas janelas ativas para confirmar que snapshots de um PID não são consumidos nem removidos pelo outro.

## 6. Tema e impressão

- [ ] Com várias abas abertas, percorrer todos os temas. Todas as abas, inclusive a não selecionada, devem adotar a paleta ao serem exibidas.
- [ ] Verificar texto, texto secundário, superfícies, estados hover e indicador de foco em cada tema.
- [ ] Abrir a impressão com texto claro e escuro, conferir foreground legível, margens, orientação, escala, quebra de palavras longas e ausência de páginas em branco indevidas.

## Registro do resultado

| Data | Versão do Windows | Arquitetura | Commit/pacote | Resultado | Falhas e evidências |
|---|---|---|---|---|---|
| 28/10/2026 | 10.0.19045 | x64 | pacote `…_1xsa4dc8b90qj` (dev, commits até `68a0f52`) | Executado em parte (automação UIA) | 15 PASS; zoom numpad e cenários visuais/manuais pendentes; defeito `Ctrl+W` com diálogo aberto — ver `SMOKE-TESTES-2026-10-28.md` |
