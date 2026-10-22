# Plano de correções e checklist de validação

Este documento é o checklist de execução da revisão dos problemas levantados na auditoria. Cada item só será marcado como concluído quando houver código, teste ou documentação correspondente.

## Persistência e perda de dados

- [x] **1. Finais de linha:** invariável `LF` no modelo, preservação do estilo no salvamento e normalização na impressão.
- [x] **2. Codificação:** leitura por bytes, BOM, UTF-8 estrito, UTF-16 LE/BE e fallback Windows-1252, com testes de round-trip.
- [x] **3. Sessão:** restauração antes da aba vazia, com atualização suspensa durante o startup.
- [x] **4. Múltiplas janelas/processos:** sessão e recovery isolados por PID; falta apenas smoke test com dois processos reais.
- [x] **6. Trimming e JSON:** trimming removido do pacote de produção; build trimmed permanece diagnóstico na CI.
- [x] **7. Unpackaged:** perfil removido e modo empacotado documentado como suportado.
- [x] **8. Arquivos recentes:** leitura, alteração e gravação sob a mesma trava; menu atualizado após operações concluídas.
- [x] **15. FutureAccessList:** tokens da sessão atual são removidos e sessões por processo evitam interferência entre janelas; falta teste real da capacidade máxima.

## Editor, busca e impressão

- [x] **5. Busca:** preservar foco no campo durante busca incremental; falta apenas validação UI automatizada.
- [x] **9. Impressão:** foreground explícito, paginação sem margem duplicada e quebra de palavras longas, com testes puros.
- [ ] **11. Foco e cliques:** preservar edição, seleção de palavras, caret e navegação por Tab; adicionar cobertura pura onde possível e roteiro de smoke test UI.
- [x] **12. Tipos de arquivo:** abertura ampla para documentos de texto e opções alinhadas no `Save As`.
- [x] **14. Atalhos:** adicionados `OemPlus`, `OemMinus`, `NumberPad0`, `Ctrl+W` e `Ctrl+Shift+S`, com bloqueio durante confirmação de fechamento.

## Acessibilidade e qualidade visual

- [x] **10. Contraste:** testes para superfície, texto com opacidade efetiva e accent; opacidades ajustadas para cumprir AA.
- [ ] **11. Indicador de foco:** confirmar visualmente o foco por teclado e manter uma alternativa que não dependa apenas dos estados nativos zerados.

## Build, documentação e higiene

- [ ] Corrigir `PublishProfiles` ignorados ou remover a dependência de perfis locais; validar clone limpo.
- [ ] Mesclar regras úteis de `Slinky/gitignore` no `.gitignore` raiz e remover o arquivo sem efeito.
- [ ] Limpar entradas obsoletas do `.csproj` e conferir assets do manifest com build MSIX.
- [x] Corrigir comentários/documentação desatualizados, incluindo `ThemeService`, versão mínima do Visual Studio e modo Unpackaged.
- [ ] Consolidar ou renomear testes de tema para refletir o nível real de integração.
- [ ] Adicionar build do app e publicação às validações locais/CI quando o ambiente permitir.
- [x] Corrigir o tratamento de exceções não tratadas para registrar e sinalizar falhas sem manter silenciosamente um estado inconsistente.
- [x] Adicionar limite máximo de 30 segundos ao autosave, além do debounce de inatividade.

## Critério de conclusão

- [ ] `dotnet test` aprovado com todos os testes novos.
- [ ] Build Release do app aprovado para pelo menos x64, sem depender de arquivos ignorados.
- [ ] Build/publicação com trimming aprovado ou trimming removido deliberadamente.
- [ ] Testes de persistência, concorrência, contraste e paginação executados.
- [ ] Smoke test UI documentado e executado quando o ambiente Windows/WinUI estiver disponível.
- [ ] Commits separados por grupo funcional, com documentação atualizada e `git status` revisado.
