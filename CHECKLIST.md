# Slinky — Checklist para open source e uso diário

Feito olhando o projeto real (código, `Package.appxmanifest`, `Slinky.csproj`), não uma lista genérica. Prioridade: 🔴 alta, 🟡 média, 🟢 baixa/nice-to-have.

## O que já está encaminhado (crédito onde é devido)

- Sem dependências de terceiros — só `Microsoft.WindowsAppSDK`. Zero risco de supply chain de pacote NuGet de terceiro.
- Renderizador de Markdown escrito à mão, sem `eval`/execução de código, sem parsing de HTML — não existe superfície pra injeção do tipo XSS que um renderizador "de verdade" (que processa HTML embutido) teria.
- 5 temas com contraste WCAG calculado (não estimado — os números estão testados de verdade em `Slinky.Tests`).
- Operações de arquivo (`Note.cs`) capturam exceção em vez de derrubar o app.
- Handler global de exceção + `crash.log`, e autosave/recuperação após fechamento indevido.
- Atalhos de teclado reais (não só texto decorativo no menu).
- Parser de Markdown e matemática de contraste extraídos em arquivos puros e cobertos por testes automatizados (`Slinky.Tests`).

## 🔴 Prioridade alta

- [x] `.gitignore`
- [x] Capability não usada removida do manifest (`systemAIModels`)
- [x] **`runFullTrust`**: documentado no README.
- [x] Atalhos de teclado de verdade
- [x] Recuperação após crash/fechamento inesperado
- [x] Handler global de exceção não tratada

Toda a prioridade alta está feita.

## 🟡 Prioridade média

- [x] **Testes automatizados** — parcial, e vale entender o porquê: `MarkdownTokenizer` e a matemática de contraste (`ContrastCalculator`/`ThemePalette`) são lógica pura, sem WinRT, e estão cobertos em `Slinky.Tests`. `Note` e `ThemeService` continuam sem teste automatizado porque dependem de `StorageFile`/`DispatcherTimer` (tipos do WinRT) — pra cobrir isso de verdade precisaria abstrair um `IFileStore` por trás do `Note`, o que é um refactor maior. Fica como próximo passo se quiser encarar.
- [x] **Acessibilidade**: `AutomationProperties.Name` no botão "✕", e foco por Tab agora mostra uma borda (só quando `FocusState.Keyboard` — clique continua sem linha, como você pediu).
- [x] **README.md** criado (falta só você colar um screenshot/GIF de verdade no lugar do placeholder).
- [x] **CI básico**: `.github/workflows/tests.yml` roda `Slinky.Tests` a cada push/PR. Compilar o `Slinky.csproj` (app empacotado) em CI ficou de fora por enquanto — complicações próprias de certificado/assinatura de MSIX; é uma rodada separada quando quiser.
- [x] **Versionamento**: o manifest já está em `1.0.0.0`, que já mapeia certinho pra semver (`1.0.0`) — só seguir subindo Patch/Minor/Major conforme o tamanho da mudança, e criar Releases no GitHub quando publicar.

## 🟢 Prioridade baixa / nice-to-have

- [ ] Paginação de impressão por fluxo de layout real (hoje é estimativa por caracteres-por-linha — funciona, mas não é pixel-perfect).
- [ ] Lista de arquivos recentes (MRU).
- [ ] Restaurar abas abertas ao reabrir o app (hoje só recupera as que ficaram com alteração não salva no fechamento indevido).
- [ ] Autosave incremental de verdade enquanto o app está aberto e fechando normalmente (o que existe hoje é pensado pra recuperação pós-crash, não pra substituir o "Salvar").

## Segurança

Sua superfície de ataque é pequena por natureza: app local, sem rede, sem conta de usuário, sem ingestão de dado de fonte não confiável além do que você mesmo escolhe abrir via picker do Windows. Isso não quer dizer "não precisa pensar nisso":

- [x] Manifest revisado pra pedir só as capabilities realmente usadas.
- [ ] Se algum dia adicionar dependências de terceiros, configurar o Dependabot do GitHub (alerta automático de vulnerabilidade em pacote).
- [ ] Assinatura de código: pra rodar só no seu PC, não precisa de certificado pago — Developer Mode ou um certificado autoassinado (o que o Visual Studio já usa pra deploy local) resolve. Certificado pago só entra em cena se for distribuir pra terceiros sem eles precisarem confiar manualmente no seu certificado.

## Criptografia

Você citou isso, então vale ser direto: **editor de texto puro não costuma criptografar o conteúdo por padrão** — o Bloco de Notas não faz isso, e a resposta mais robusta pra "proteger meus arquivos em repouso" já existe num nível mais fundamental (BitLocker, criptografia de disco do Windows), não dentro de cada aplicativo individualmente.

Se mesmo assim você quiser uma *funcionalidade* de "nota protegida por senha" no Slinky (não é a mesma coisa que "app seguro por padrão"):
- **Nunca** escreva sua própria cifra. Use `System.Security.Cryptography.Aes` (AES-256) com a chave derivada da senha via PBKDF2 (`Rfc2898DeriveBytes`), guardando salt e IV junto do arquivo cifrado.
- Isso protege o *arquivo* se alguém ler os bytes crus no disco. Não protege contra alguém com acesso ao Slinky já aberto e a nota já descriptografada na tela — isso é inerente a qualquer app desse tipo, não é falha de implementação.
- Dá pra tratar isso como feature opcional futura, não como requisito de lançamento.

## Licenciamento (pra abrir o código)

Três opções comuns, resumidas sem viés:
- **MIT**: mais permissiva, qualquer um pode usar/modificar/redistribuir até em projeto fechado, só precisa manter o aviso de copyright. É a que a maior parte do ecossistema WinUI/`Microsoft.WindowsAppSDK` usa.
- **Apache 2.0**: parecida com MIT, mas com cláusula explícita de concessão de patente — mais relevante se você se preocupa com patentes de software.
- **GPLv3**: copyleft — quem distribuir uma versão modificada é obrigado a manter o código aberto também. Mais restritiva, mas garante que forks continuem abertos.

Isso é decisão sua, não vou escolher por você — mas seja qual for, precisa de um arquivo `LICENSE` na raiz do repositório antes de tornar público (repositório sem license é "todos os direitos reservados" por padrão, mesmo que a intenção seja outra).

## Sobre contribuições externas

Decisão registrada: **você não vai aceitar contribuição de terceiros**. É uma escolha válida — "código aberto pra leitura/uso" não é a mesma coisa que "aceito PR de qualquer um", e várias projetos pessoais conhecidos funcionam assim.

Na prática, isso significa que você **não precisa** de `CONTRIBUTING.md`, templates de Issue pra PR, nem regra de branch protection pensada pra revisão externa. Só uma sugestão pequena: vale deixar uma linha clara no README avisando disso —algo como *"Projeto pessoal; código aberto para leitura e uso, mas não aceito pull requests no momento."* Isso evita que alguém invista tempo escrevendo um PR grande e se frustre ao ver que não vai ser aceito; é mais gentil avisar antes do que fechar depois sem explicação.

## Peso e otimização (por último, de propósito)

Isso é literalmente o motivo do projeto ter começado — bloco de notas do Windows 11 gastando 24-25MB pelo que você mostrou no post, contra o TinyRetroPad em ~3KB. Faz sentido isso vir por último: otimizar peso/performance antes de tudo estar correto e testado só dificulta separar "ficou pequeno" de "ficou pequeno e quebrado".

- [ ] **Meça antes de otimizar.** Sem número de referência, não dá pra saber se qualquer mudança abaixo ajudou ou não. Duas medidas simples:
  - Tamanho: gere o pacote (Build → Create App Packages, ou só olhe o tamanho da pasta de saída do build Release) e anota o número de hoje.
  - Tempo de abertura: cronometra manualmente (ou Gerenciador de Tarefas → aba Desempenho) do clique até a janela aparecer usável.
- [ ] **Framework-dependent, não self-contained.** Isso é o que mais pesa na balança: self-contained empacota o runtime do .NET + WindowsAppSDK inteiro dentro do seu pacote (dezenas de MB a mais); framework-dependent assume que o runtime já está instalado na máquina (o que é normal em qualquer Windows com WindowsAppSDK, e é o seu caso rodando localmente). Confirma essa configuração no Gerenciador de Configuração do Visual Studio — pra uso pessoal, framework-dependent é a escolha certa.
- [ ] **Assets/ícones**: o template padrão do WinUI3 gera vários tamanhos de ícone (Square44x44Logo, Square150x150Logo, Wide310x150Logo, StoreLogo, tela de splash) em múltiplas escalas (100/125/150/200/400%) pensados pra listagem na Microsoft Store. Pra uso pessoal, isso é mais do que precisa — dá pra reduzir pro mínimo que a barra de tarefas/menu iniciar realmente usam.
- [ ] **ReadyToRun** (compilação antecipada): deixa a abertura mais rápida, mas aumenta o tamanho do pacote — é troca, não ganho puro. Vale testar e comparar com a medição inicial.
- [ ] **Trimming (remover código não usado pelo linker do .NET)**: pode reduzir bastante o tamanho, mas é a opção de maior risco pra um app WinUI3 — o trimmer às vezes remove algo que só é acessado via reflection (comum em binding de XAML) e quebra em runtime sem erro de compilação. Se for tentar, faça por último e teste manualmente *cada* funcionalidade do app depois (abrir, salvar, temas, impressão, etc.), não só compilar e assumir que funcionou.
- [ ] **Native AOT**: o mais radical (executável nativo, sem JIT, potencialmente menor e mais rápido pra abrir) — mas o suporte pra apps WinUI3 completos (XAML, não só console/API) ainda é uma área mais nova e menos madura do que trimming/R2R. Vale pesquisar o estado atual quando chegar nessa etapa antes de investir tempo, já que isso muda rápido.

O que já ajuda seu "leve" mesmo sem nenhum item acima: zero dependência de terceiros e o parser de Markdown feito à mão em vez de uma lib pesada — isso já está pago.

---

Isso é bastante coisa — não precisa (nem faz sentido) atacar tudo de uma vez. Me diz por qual item você quer começar que a gente vai um de cada vez, do mesmo jeito que fizemos até aqui.
