# Auditoria de arquitetura, implementação e plano do Slinky

## 1. Escopo

Este documento consolida:

- a auditoria inicial do repositório;
- a inspeção direta do código realizada durante a implementação;
- as decisões e correções aplicadas;
- os testes executados;
- o estado atual do CI;
- as funcionalidades ainda pendentes.

O objetivo original foi preparar, sem reescrever componentes funcionais, as seguintes áreas:

1. finais de linha e codificação;
2. restauração de sessão;
3. múltiplas instâncias e janelas;
4. busca e substituição;
5. recovery de documentos não salvos;
6. impressão;
7. consolidação de testes e CI;
8. limpeza da documentação.

## 2. Arquitetura encontrada

O fluxo principal atual é:

```text
App
└── MainWindow
    └── NotesTabView
        └── TabViewItem
            └── NotePage
                └── TextBox NoteEditor
                    └── Note
```

### 2.1 Aplicação, janela e abas

`App.xaml.cs` cria a `MainWindow`, mantém uma referência estática em `App.Window`, registra exceções não tratadas e ativa a janela.

`MainWindow.xaml.cs` coordena:

- abas;
- abertura, salvamento e fechamento;
- restauração da sessão;
- recovery;
- arquivos recentes;
- impressão;
- temas.

Cada aba é um `TabViewItem` cujo `Tag` aponta para um `Note` e cujo `Content` é um `NotePage`. Essa representação foi preservada por ser adequada e simples.

### 2.2 Documento e editor

`Note.cs` representa o documento e implementa `INotifyPropertyChanged`. Ele mantém:

- arquivo associado;
- texto;
- estado `IsDirty`;
- metadados de codificação;
- metadados de BOM;
- estilo de final de linha;
- identificador de recovery;
- temporizador de autosave.

`NotePage.xaml.cs` contém o `TextBox` nativo e sincroniza as alterações do editor com o modelo.

### 2.3 Persistência

Existem quatro áreas distintas:

| Área | Implementação |
|---|---|
| Sessão | `SessionService` + `session.json` + `FutureAccessList` |
| Recovery | `RecoveryService` + snapshots JSON em `ApplicationData` |
| Arquivos recentes | `RecentFilesService` + JSON + `MostRecentlyUsedList` |
| Preferências | `ApplicationData.Current.LocalSettings` |

As responsabilidades continuam separadas. Foram adicionados codecs e políticas puras para evitar duplicação de regras sem transformar todos os serviços em uma camada genérica única.

## 3. Fluxos de conteúdo

### 3.1 Abertura

O fluxo anterior usava `FileIO.ReadTextAsync`, sem controle explícito de codificação ou finais de linha, e atribuía o texto pela propriedade pública, o que podia marcar um arquivo recém-aberto como modificado.

O fluxo atual é:

```text
StorageFile
  ↓ ReadBufferAsync
bytes
  ↓ TextFileCodec.Decode
texto interno com LF + metadados
  ↓ Note
documento limpo
```

A criação visual da aba também suprime o primeiro `TextChanged` do `TextBox`, evitando que o carregamento inicial marque o documento como sujo.

### 3.2 Modificação

Alterações reais no `TextBox` continuam atualizando `Note.Text`, marcando `IsDirty` e agendando recovery.

O modelo ainda não possui uma comparação completa com um baseline salvo. Portanto, desfazer manualmente uma alteração pode não limpar automaticamente `IsDirty`.

### 3.3 Salvamento

O salvamento atual usa `TextFileCodec.Encode` e grava bytes, preservando:

- codificação;
- BOM;
- estilo de final de linha.

O salvamento remove o snapshot atual de recovery sem desativar recovery para futuras edições.

## 4. Codificação e finais de linha

Foi criado `TextFileCodec.cs` com as seguintes regras:

- representação interna sempre com `LF`;
- detecção de `CRLF`, `LF`, `CR` e finais mistos;
- UTF-8;
- UTF-8 com BOM;
- UTF-16 little-endian;
- UTF-16 big-endian;
- fallback Windows-1252 para bytes que não formam UTF-8 válido;
- preservação do BOM;
- preservação do estilo detectado ao salvar;
- novos documentos usam UTF-8 sem BOM e CRLF.

Os testes incluem round-trip de bytes e arquivo temporário real.

## 5. Sessão

### 5.1 Problema encontrado

A aplicação criava uma aba vazia no construtor da janela. Como `AddNoteTab` atualizava a sessão imediatamente, essa aba podia gravar `[]` antes da restauração da sessão anterior.

### 5.2 Correção

Agora:

- a restauração ocorre antes da criação da aba vazia;
- a aba vazia só é criada quando não há sessão nem recovery;
- a atualização da sessão fica suspensa durante a restauração;
- tokens antigos da `FutureAccessList` são removidos;
- a sessão é gravada em arquivo temporário e substituída atomicamente;
- tokens duplicados são eliminados;
- a ordem de restauração é preservada;
- arquivos ausentes são ignorados sem interromper a inicialização.

`SessionRestorePolicy` garante que caminhos já restaurados por recovery ou repetidos na sessão não sejam abertos novamente.

## 6. Recovery

### 6.1 Problemas encontrados

- gravação direta no arquivo final;
- possibilidade de JSON parcial;
- concorrência entre autosave e limpeza;
- ausência de metadados de codificação e finais de linha;
- recuperação não distinguia claramente snapshot e sessão.

### 6.2 Correções

- snapshots são gravados em arquivo temporário;
- a substituição final é atômica;
- gravações e exclusões usam `SemaphoreSlim`;
- snapshots armazenam codificação, BOM e finais de linha;
- `RecoveryLifecycle` impede novo autosave depois que a nota foi fechada;
- salvar remove o snapshot sem desativar recovery futuro;
- JSON corrompido é ignorado sem impedir a inicialização.

O recovery ainda restaura o texto como documento não associado ao arquivo original. A reconexão automática ao arquivo continua sendo uma decisão pendente por envolver permissões e conflitos de edição externa.

## 7. Arquivos recentes

Foi extraída a política pura `RecentFileListPolicy`, cobrindo:

- deduplicação por caminho, ignorando maiúsculas/minúsculas;
- promoção do arquivo acessado para o topo;
- limite de dez itens;
- remoção dos tokens descartados do MRU.

A persistência agora usa o mesmo `SemaphoreSlim` para leitura, alteração e gravação.
Isso impede que duas aberturas simultâneas leiam a mesma lista antiga e percam uma entrada.
O menu de recentes é atualizado depois que a operação de abertura/salvamento termina.

A persistência JSON dos recentes agora usa arquivo temporário e substituição atômica.

## 8. Busca e substituição

`TextSearch` já era uma classe pura e foi preservada.

Os testes existentes continuam cobrindo:

- busca para frente;
- busca reversa;
- wrap-around;
- diferenciação de maiúsculas;
- contagem sem sobreposição;
- Replace All;
- substituição vazia.

Foi corrigido o comportamento visual em que digitar no campo de busca fazia o editor receber foco a cada caractere. A busca incremental agora preserva o foco do campo; ações explícitas de próxima ocorrência continuam podendo selecionar no editor.

Ainda falta uma automação UI para validar foco, caret, seleção e undo dentro do WinUI.

## 9. Impressão

Foi criada `PrintPagination.cs`, separando a lógica pura da criação dos controles WinUI.

A paginação agora:

- normaliza finais de linha;
- preserva linhas vazias;
- quebra palavras longas;
- gera uma página para documento vazio;
- limita escala inválida;
- limita margem inválida;
- calcula conteúdo uma única vez;
- evita margem aplicada em duplicidade.

O `TextBlock` impresso usa foreground preto explícito sobre fundo branco, independentemente do tema do editor.

Ainda falta um teste de impressão real usando `PrintManager` e uma impressora virtual ou mock nativo do Windows.

## 10. Fechamento e estado modificado

O fechamento existente continua usando a confirmação da `MainWindow` para documentos modificados.

Foi corrigido o risco de recovery recriado por um autosave pendente depois do fechamento da aba.

O que ainda não foi implementado:

- baseline de conteúdo salvo para limpar `IsDirty` após undo completo;
- detecção de alteração externa do arquivo;
- testes UI para salvar, descartar e cancelar o fechamento;
- isolamento completo do estado de fechamento por janela.

## 11. Múltiplas instâncias e janelas

O código original executa outro processo em “Nova janela” usando `Process.Start(Environment.ProcessPath)`.

Também há dependência de `App.Window` em FilePicker e APIs relacionadas à janela.

A persistência foi isolada por processo. A sessão agora usa `session-{PID}.json` e só
consome arquivos cujo processo dono não está vivo. Snapshots de recovery carregam
`OwnerProcessId` e seguem a mesma regra. Assim, “Nova janela” continua sendo um novo
processo, mas uma janela não apaga a sessão ou o recovery de outra que ainda está aberta.

O lançamento de uma nova janela também captura falhas de `Process.Start` e as apresenta
ao usuário.

### Opção recomendada: várias janelas no mesmo processo

Exigiria:

- contexto por janela;
- HWND correto para cada FilePicker;
- HWND correto para cada PrintManager;
- ownership único da sessão;
- recovery coordenado por aplicação;
- fechamento independente por janela.

O comportamento atual continua sendo uma janela por processo. O tema permanece um estado
compartilhado apenas dentro do processo; sessão e recovery deixaram de compartilhar um
arquivo ativo entre processos.

Essa decisão continua pendente.

## 12. Testes atuais

A suíte `Slinky.Tests` possui 72 testes no estado atual do código-base. A suíte cobre:

- busca e substituição;
- contraste e temas;
- estados de abas;
- roteamento do editor;
- codificação;
- finais de linha;
- round-trip em arquivo temporário;
- codec de sessão;
- política de restauração;
- codec de recovery;
- JSON corrompido;
- ciclo de vida do recovery;
- política de arquivos recentes;
- contraste de texto secundário, superfície e indicador de foco;
- paginação de impressão.

Ainda não existem testes reais de:

- `StorageFile` e `FutureAccessList` em sessão completa;
- `ApplicationData` em recovery completo;
- `MostRecentlyUsedList` real;
- fechamento visual de `MainWindow`;
- `PrintManager` real;
- múltiplos processos;
- múltiplas janelas;
- alterações externas de arquivo.

## 13. CI atual

O workflow `.github/workflows/tests.yml` agora contém:

### Testes e build base

- restore dos testes;
- `dotnet test` em Release;
- restore WinUI x64;
- build WinUI x64.

### Matriz de arquiteturas

- x86;
- x64;
- ARM64.

### MSIX

- geração de MSIX unsigned x64;
- verificação de existência do `.msix` e `.msixbundle`;
- upload dos artefatos.

### Instalação

- execução do `instalar-slinky.bat --x64`;
- verificação via `Get-AppxPackage`;
- remoção do pacote ao final do smoke test.

### Trimming

- restore x64 com trimming parcial;
- build Release com `PublishTrimmed=true`.

### Limitações do CI

Ainda não há uma execução visual automatizada do aplicativo. Portanto, o CI não valida de forma completa:

- foco visual;
- troca real de tema;
- interação de abas;
- diálogo de fechamento;
- busca visual;
- preview real de impressão;
- múltiplas janelas em execução;
- regressões de runtime nativo que só aparecem depois de iniciar a aplicação.

Esses pontos exigem WinAppDriver, Playwright Windows App SDK, MSTest UI ou outra infraestrutura de automação visual ainda não presente no projeto.

## 14. Build e empacotamento

O projeto usa:

- .NET 8;
- WinUI 3;
- Windows App SDK 1.8;
- MSIX single-project;
- Runtime Identifiers `win-x86`, `win-x64` e `win-arm64`.

O script `instalar-slinky.bat`:

- restaura dependências;
- cria ou reutiliza certificado de desenvolvimento;
- gera pacote self-contained;
- seleciona o pacote mais recente;
- instala o pacote;
- suporta `--x86`, `--x64`, `--arm64`, `--build-only` e `--unsigned`.

## 15. Decisões ainda necessárias

> **Fechamento de 29/10/2026 — todos os itens resolvidos.** Os comportamentos de
> referência do Bloco de Notas foram definidos por experimento no `notepad.exe`
> clássico desta máquina (automação real; evidências e medidas em
> `DESEMPENHO-E-TAMANHO.md`, seção "Comportamentos de referência").

1. ~~"Nova janela": novo processo ou múltiplas janelas?~~ **Resolvido: novo processo** (Notepad clássico abre 2 processos para 2 janelas — confirmado por medição; implementado e validado com isolamento de sessão/recovery por PID).
2. ~~Reconexão automática do recovery ao arquivo original.~~ **Resolvido: implementada** (`RecoverySnapshot.FileToken` + `Note.TryReattachSavedFileAsync` via FutureAccessList; validado ponta a ponta — kill → Restaurar → aba volta com o nome do arquivo e Ctrl+S grava no original). Paridade com o modelo de recuperação do Notepad moderno.
3. ~~Política para arquivo modificado externamente.~~ **Resolvido: sobrescrita silenciosa, sem recarga/aviso** (comportamento observado no Notepad: buffer local vence). Sem watcher/polling — preserva CPU ociosa 0%.
4. ~~Suporte oficial a Windows-1252.~~ **Resolvido: round-trip oficial** — `SaveAsync` preserva a codificação detectada (1252 incluído), como o ANSI do Notepad; sem seletor manual de codificação (diferença deliberada de escopo).
5. ~~Desfazer até o conteúdo salvo limpa `IsDirty`?~~ **Resolvido: não — flag pegajoso**, paridade com o Notepad clássico (evidência: após salvar 'abcd', undo para 'abc' e fechar, o título permaneceu `*a.txt`).
6. ~~Infraestrutura de testes UI Windows.~~ **Resolvida: `SMOKE-UI.md` + automação** (UIA + SendInput), com registros completos de 28–29/10.
7. ~~Trimming em produção.~~ **Decisão final deliberada: sem trimming** enquanto os codecs JSON forem reflexivos; build trimmed permanece apenas como diagnóstico na CI.
8. ~~Fixar SDK por `global.json`.~~ **Decisão: não fixar, com justificativa registrada** — as máquinas de desenvolvimento desta utilizam SDKs 10.0.x/11.0.100-rc (não há SDK 8 instalado localmente, e um `global.json` 8.0 quebraria o ambiente local); a CI fixa o SDK via `setup-dotnet` (8.0.x) e o `TargetFramework net8.0-windows10.0.19041.0` já fixa o alvo independentemente do SDK.

## 16. Status consolidado das oito áreas originais

> **Atualização de 29/10/2026**: a tabela abaixo foi concluída pelo plano de
> correções (`PLANO-CORRECOES-E-CHECKLIST.md`, 100% com evidência) e pelos smokes
> de 28–29/10 — incluindo teste multi-processo real (duas janelas, kill e recovery
> determinístico), automação da UI de busca, recovery após encerramento forçado e
> verificação visual dos cinco temas com indicador de foco.

| Área | Estado |
|---|---|
| Finais de linha e codificação | Implementada na camada de documento e testada |
| Restauração de sessão | Implementada com deduplicação, ordem e aba vazia corrigida |
| Múltiplas instâncias/janelas | Implementada e validada com processos reais (28–29/10) |
| Busca e substituição | Implementada com testes puros e UI validada por automação |
| Recovery não salvo | Implementado e validado de ponta a ponta (kill forçado → restauração única) |
| Impressão | Paginação corrigida e testada; diálogo de impressão abre/cancela validado (impressão em hardware segue manual) |
| Testes e CI | Fortalecido com arquiteturas, MSIX, instalação e trimming |
| Documentação | Consolidada neste documento e parcialmente refletida no README |

## 17. Histórico das entregas

- `e2161e2` — codec de documento, codificação e finais de linha.
- `90062c7` — ciclo de vida seguro do recovery.
- `aa86427` — build WinUI no CI e documentação inicial.
- `6af9d43` — codecs e testes de persistência.
- `8280e77` — política de arquivos recentes.
- `ff522ac` — restauração completa da sessão.
- `5280bee` — impressão e busca/substituição.

## 18. Próximo passo após esta auditoria

Depois da validação do quarto estágio, o próximo trabalho recomendado é resolver a decisão de múltiplas janelas/processos. Sem essa decisão, qualquer implementação adicional de sessão, recovery e ownership de recursos poderá precisar ser refeita.
