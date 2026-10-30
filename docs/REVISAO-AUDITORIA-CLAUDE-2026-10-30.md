# Revisão cruzada da auditoria externa (Claude) — 30/10/2026

Uma auditoria estática externa (modelo Claude) apontou problemas contra um snapshot
**anterior ao plano de correções**. Este documento cruza cada achado com o código
atual (`a589319`+) e registra o veredito: **JÁ CORRIGIDO** (com evidência),
**CORRIGIDO NESTE CICLO**, ou **VÁLIDO — pendente/melhoria futura**.

## Veredito geral

Dos 4 críticos apontados, **3 já estavam corrigidos e validados** pelo plano de
correções/QA; 1 (C3, tema entre processos) tinha resquício de copy no site — corrigido.
Dos 8 altos, 6 já corrigidos, 2 parcialmente válidos (tema dos diálogos → corrigido
agora; resíduo de `.slinky-tmp` → documentado). Dos 15 médios, 11 já corrigidos,
5 válidos → 4 corrigidos agora (M1, M2, M3, M5, M6) e 1 verificado como
comportamento correto (M7).

## Críticos

| ID | Achado | Veredito | Evidência/medida |
|---|---|---|---|
| C1 | Busca incremental rouba o foco do campo | **JÁ CORRIGIDO** | `FindTextBox_TextChanged` usa `focusEditor: false` (`NotePage.xaml.cs`); smoke 29/10: "foco permanece na busca incremental — PASS" |
| C2 | `\r` corrompe arquivo, nota vira suja ao abrir, impressão sem parágrafos | **JÁ CORRIGIDO** | invariante LF no modelo + estilo preservado (`TextFileCodec`); bateria QA 30/10: 9/9 round-trips byte-exatos (CRLF/1252/BOM preservados) |
| C3 | Processos brigam por estado (recovery, sessão, tema) | **JÁ CORRIGIDO** (maioria) | sessão e recovery isolados por PID (`session-{PID}.json`, `OwnerProcessId` + `IsOwnedByLiveProcess`); validado com 2 processos reais (28–29/10). **Resquício corrigido agora**: copy do site dizia "sem multiplicar processos" — reescrita ("abra quantas janelas quiser"). **Pendente conhecido**: troca de tema NÃO propaga em tempo real para janelas de outros processos (o ThemeWindow promete isso) — melhoria futura (exigiria IPC) |
| C4 | Corrida: `session.json` zerado antes do restore | **JÁ CORRIGIDO** | aba inicial só é criada depois do restore; flag `startupRestorationInProgress`; validado por restores repetidos |

## Altos

| ID | Achado | Veredito |
|---|---|---|
| H1 | `Tapped` quebra seleção com mouse | **JÁ CORRIGIDO** — roteamento por `EditorTapRouting` (função pura testada); smoke 29/10: duplo clique seleciona palavra, clique posiciona caret — PASS |
| H2 | `PublishTrimmed=True` quebraria JSON reflexivo | **JÁ CORRIGIDO** — `PublishTrimmed=False` explícito no `.csproj`; CI mantém trimmed só diagnóstico |
| H3 | Vazamento de tokens FAL | **JÁ CORRIGIDO** — `SaveOpenFilesAsync` remove tokens antigos a cada reescrita (mapa path→token); limite 1000 nunca alcançado |
| H4 | Encoding + filtro `.txt` | **JÁ CORRIGIDO** — detecção BOM/UTF-8/UTF-16/1252 + preservação no salvar (QA 9/9); filtro já é `*` |
| H5 | Impressão (foreground, margem duplicada, quebra) | **JÁ CORRIGIDO** — item 9 do plano com testes puros (`PrintPagination`); impressão em hardware segue não executável (sem impressora) |
| H6 | Um `ContentDialog` por vez + tema dos diálogos | **PARCIAL** — a regra de diálogo único foi reforçada em 28/10 (flag `contentDialogOpen` + guardas, commit `3a89b63`); **tema dos diálogos corrigido agora**: todos os `ContentDialog` (confirmar fechamento, info, recovery, exclusão, erro) definem `RequestedTheme` da paleta ativa |
| H7 | Perfil Unpackaged quebra | **JÁ CORRIGIDO** — perfil removido do projeto |
| H8 | Exceções engolidas sem avisar | **JÁ CORRIGIDO** — handler global registra em `crash.log` E mostra diálogo ao usuário; guarda de diálogo única implementada. Rotação do log e hooks de `AppDomain`/`TaskScheduler` ficam como melhorias futuras |

## Médios

| ID | Achado | Veredito |
|---|---|---|
| M1 | "Substituir tudo" perde o undo | **VÁLIDO — CORRIGIDO AGORA**: substituições aplicadas de trás para frente via `NoteEditor.SelectedText` (posições de `TextSearch.FindAll`, função pura nova com testes), preservando o histórico do TextBox. Validado na UI: 3 substituições aplicadas e cadeia de Ctrl+Z viva. Antes, `NoteEditor.Text = result` zerava o undo |
| M2 | "Maiúsc/minúsc" escondido no Ctrl+F | **VÁLIDO — CORRIGIDO AGORA**: checkbox movido para a linha principal da barra (visível sempre); validado na UI (PASS) |
| M3 | `ReplaceTextBox` sem Enter/Esc | **VÁLIDO — CORRIGIDO AGORA**: `KeyDown="FindBar_KeyDown"` no campo de substituir |
| M5 | Mesmo arquivo em duas abas | **VÁLIDO — CORRIGIDO AGORA**: abrir (diálogo ou recentes) um arquivo já aberto foca a aba existente (`FindTabByPath`) |
| M6 | "Excluir" em nota sem arquivo descarta sem confirmar | **VÁLIDO — CORRIGIDO AGORA**: `RequestClose` agora passa por `ConfirmCloseNoteAsync`; `DeleteAsync` marca a nota como limpa após o usuário confirmar a exclusão (o descarte já foi decidido no diálogo) |
| M7 | Exclusão permanente vs lixeira | **COMPORTAMENTO CONFIRMADO**: `StorageFile.DeleteAsync` sem opção é permanente — o diálogo já diz "definitivamente" (coerente) |
| M8 | Save não atômico | **CORRIGIDO NO QA (30/10)** — `WriteBytesSafeAsync` (tmp GUID + `MoveAndReplaceAsync`, fallback direto) |
| M9 | Atalhos OemPlus/OemMinus/Numpad0/Ctrl+Shift+S/Ctrl+W | **JÁ CORRIGIDO** — aceleradores OEM em runtime + numpad, validados por smoke; `Ctrl+Y` e zoom por roda do mouse seguem na lista de melhorias futuras |
| M10 | Contraste com opacidade/surface/accent | **JÁ CORRIGIDO** — item 10 do plano: testes de contraste cobrem opacidade efetiva, surface e accent |
| M11 | Regra de tema duplicada | **VÁLIDO — CORRIGIDO AGORA**: `ThemePalette.IsDark` é a fonte única; `ThemeService.ElementTheme` e `ThemeTabState.For` a consultam (teste novo) |
| M12 | Corridas em `RecentFilesService`/sessão | **JÁ CORRIGIDO** — leitura/escrita sob `SemaphoreSlim` (item 8 do plano) |
| M13 | Autosave sem teto | **JÁ CORRIGIDO** — teto de 30 s além do debounce |
| M14 | Recovery apaga cedo / sem vínculo ao arquivo | **CORRIGIDO (29/10)** — reconexão via token FAL; janela de 2 s entre restaurar e o novo snapshot segue aceitável (o conteúdo está em memória) |
| M15 | Arquivos grandes | **MEDIDO E DOCUMENTADO (QA 30/10)** — limites reais por tamanho; freeze de save corrigido (off-thread) |

## Baixos/manutenção

| Achado | Veredito |
|---|---|
| `Slinky/gitignore` sem ponto | **JÁ CORRIGIDO** (mesclado e removido) |
| `*.pubxml` ignorado | **JÁ CORRIGIDO** (versionados + exceção; validado em clone limpo) |
| Entradas mortas no `.csproj` (`NotePAge` etc.) | **JÁ CORRIGIDO** (limpeza de 28/10) |
| Assets do manifesto | **JÁ CORRIGIDO** (auditoria de 28/10: todos os logos presentes, lixo removido) |
| Comentários desatualizados | **JÁ CORRIGIDO** (item do plano) |
| Dead code `CreatedAt`/`SavedAt` | **VÁLIDO (higiene)** — sem uso; remoção trivial fica registrada |
| Traits faltantes em `ContrastCalculatorTests` | Válido, cosmético |
| Teste "EndToEnd" que não é | **JÁ CORRIGIDO** (renomeado `ThemeSelectionWorkflowTests`, trait Unit) |
| CI não compila o app | **JÁ CORRIGIDO** — CI cobre builds x86/x64/ARM64, MSIX, instalação e trimming |

## O que do relatório externo segue VÁLIDO e como está sendo tratado

1. **`.NET 8 sai de suporte em 10/11/2026` — o achado mais importante e atual.**
   Viabilidade medida agora nesta máquina: **76/76 testes passam com `net10.0`** e o
   **app compila com `net10.0-windows10.0.19041.0` + Windows App SDK 1.8** (a
   receita de release já usa `PublishReadyToRun=false`). Migrar é seguro em
   princípio e deve acontecer ANTES de 10/11 — resta validar o pacote MSIX net10
   instalado e o smoke completo, então trocar o TFM e o `setup-dotnet` da CI. Não
   feito neste ciclo para não misturar migração de runtime com correções de bugs.
2. **Ícone na barra de tarefas**: desatualizado — os assets `targetsize-16/32/48/256`
   e `altform-unplated` correspondentes JÁ EXISTEM em `Assets/`.
3. **Distribuição/README**: desatualizado — README tem seção de instalação com
   certificado e script; `docs/INSTALACAO-MSIX.md` documenta causas raiz; release
   workflow existe e foi verificado (v1.0.3/v1.0.4).
4. **Dependabot, rotação do crash.log, contadores de busca, associação de arquivo,
   barra de status, Ctrl+Y/roda**: melhorias futuras legítimas — mantidas na lista
   de próximos passos (não implementadas: o escopo deste ciclo é QA/correção).
5. **Tema entre processos (C3 parcial)**: o texto do ThemeWindow promete atualização
   instantânea em "todas as janelas", mas processos não compartilham o
   `ThemeService`. Sincronizar exigiria IPC — melhoria futura.

## Correções aplicadas neste ciclo

- `TextSearch.FindAll` (+2 testes) e `ReplaceAll_Click` reescrito para preservar undo (M1).
- Checkbox "Maiúsc/minúsc" na linha principal da busca (M2) e teclas Enter/Esc no campo "Substituir por" (M3).
- `AutomationProperties.Name` em "Localizar" e "Substituir por" (acessibilidade — mesmo pedido da seção de melhorias do relatório externo).
- Abrir arquivo já aberto foca a aba existente, via diálogo ou recentes (M5).
- Fechamento via "Excluir arquivo" passa pela confirmação de alterações não salvas (M6).
- Todos os `ContentDialog` herdam o tema ativo (H6).
- `ThemePalette.IsDark` como fonte única da regra claro/escuro (M11, +1 teste).
- Copy do site alinhada ao modelo de janelas (C3).

## Próximos passos sugeridos (prioridade × esforço)

1. **Migrar para `net10.0-windows` antes de 10/11/2026** (EOL do .NET 8) — impacto alto, esforço baixo/médio: TFM + CI + smoke completo no pacote net10 (compilação e testes já validados).
2. Versionar as baterias de QA em `Tests/automation/` — esforço baixo, valor alto.
3. Limpeza de `.slinky-tmp` órfãos no startup — trivial.
4. Associação de arquivos + abertura por linha de comando — impacto alto para substituto do Bloco de Notas, esforço médio.
5. Barra de status (linha/coluna/encoding/EOL) e Ctrl+Y — médio.
6. Dependabot (NuGet + Actions) — trivial, só configuração.
7. Sincronização de tema entre processos (IPC) — esforço alto, impacto baixo.
