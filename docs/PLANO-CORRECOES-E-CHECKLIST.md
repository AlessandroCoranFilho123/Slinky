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
- [x] **11. Foco e cliques:** edição, seleção de palavras, caret e navegação por Tab preservados; cobertura pura e roteiro em `SMOKE-UI.md` adicionados. A execução visual continua pendente no critério de conclusão.
- [x] **12. Tipos de arquivo:** abertura ampla para documentos de texto e opções alinhadas no `Save As`.
- [x] **14. Atalhos:** adicionados os códigos OEM do `+`/`-` principal, `NumberPad0`, `Ctrl+W` e `Ctrl+Shift+S`, com bloqueio durante confirmação de fechamento.
- [x] **16. Diálogo de fechamento (defeito encontrado no smoke de 28/10):** `Ctrl+W` com confirmação aberta lançava `0x80000019` ("Only a single ContentDialog can be open at any time"). Corrigido com bloqueio compartilhado `contentDialogOpen` em todos os `ShowAsync` (`ConfirmCloseNoteAsync`, `ShowInfoDialogAsync`) e guardas nos aceleradores `Ctrl+S`, `Ctrl+Shift+S` e `Ctrl+W`. Reprodução pós-fix no app instalado: segundo `Ctrl+W` ignorado, sem entrada nova no `crash.log` e cancelamento preservando a aba.

## Acessibilidade e qualidade visual

- [x] **10. Contraste:** testes para superfície, texto com opacidade efetiva e accent; opacidades ajustadas para cumprir AA.
- [x] **11. Indicador de foco:** confirmado visualmente nos cinco temas em 29/10/2026 — com correção: o indicador original (borda da `TextBox`) era sobrescrito pelos visual states e nunca renderizava; substituído por `Rectangle` dedicado (`FocusUnderline`) que aparece só com foco de teclado. Evidência: 1184 pixels accent por tema na faixa inferior, ver `SMOKE-TESTES-2026-10-28.md` (seção 29/10).

## Build, documentação e higiene

- [x] Corrigir `PublishProfiles` ignorados ou remover a dependência de perfis locais; perfis versionados e exceção incluída no `.gitignore` raiz. Validado em clone limpo (`file://` de `dbff66a`) em 29/10/2026: 3 perfis presentes, build Release x64 com 0 erros.
- [x] Mesclar regras úteis de `Slinky/gitignore` no `.gitignore` raiz e remover o arquivo sem efeito.
- [x] Limpar entradas obsoletas do `.csproj` e conferir assets do manifest com build MSIX: entradas `None Remove` mortas de `BadgeLogo` removidas, `LockScreenLogo.scale-200.png` (não referenciado pelo manifesto) e `StoreLogo.backup.png` excluídos; rebuild MSIX x64 confirmou todos os logos referenciados presentes no pacote e o lixo ausente.
- [x] Corrigir comentários/documentação desatualizados, incluindo `ThemeService`, versão mínima do Visual Studio e modo Unpackaged.
- [x] Consolidar ou renomear testes de tema para refletir o nível real de integração; ambos agora são identificados como testes unitários headless.
- [x] Adicionar build do app e publicação às validações locais/CI quando o ambiente permitir; CI cobre x86/x64/ARM64, MSIX, instalação e trimming diagnóstico.
- [x] Corrigir o tratamento de exceções não tratadas para registrar e sinalizar falhas sem manter silenciosamente um estado inconsistente.
- [x] Adicionar limite máximo de 30 segundos ao autosave, além do debounce de inatividade.

## Critério de conclusão

- [x] `dotnet test` aprovado com todos os testes novos (72/72 em 28/10/2026, ver `SMOKE-TESTES-2026-10-28.md`).
- [x] Build Release do app aprovado para pelo menos x64, sem depender de arquivos ignorados.
- [x] Build/publicação com trimming aprovado ou trimming removido deliberadamente: produção fica sem trimming por usar JSON baseado em reflexão (`PublishTrimmed=False` no `.csproj`); CI mantém build trimmed apenas como diagnóstico.
- [x] Testes de persistência, concorrência, contraste e paginação executados.
- [x] Smoke test UI executado em 28–29/10/2026: roteiro de `SMOKE-UI.md` coberto por automação (UIA + SendInput + capturas de tela) e verificação visual por pixel — digitação, cliques, busca, atalhos (incl. numpad), diálogos nativos abrir/cancelar, isolamento de processos, recovery pós-kill determinístico e cinco temas com indicador de foco. Registros completos em `SMOKE-TESTES-2026-10-28.md`.
- [x] Commits separados por grupo funcional, com documentação atualizada e `git status` revisado.
