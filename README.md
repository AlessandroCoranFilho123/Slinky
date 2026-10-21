# Slinky

Um editor de texto leve para Windows, feito com WinUI 3. Nasceu da vontade de ter algo do tamanho e da simplicidade do Bloco de Notas clássico, com suporte nativo a `.txt` e abas, em vez de uma janela por arquivo.

## Funcionalidades

- Abas — várias notas na mesma janela.
- `.txt` nativo, com uma superfície de edição simples e rápida.
- 5 temas (Claro Padrão/Amarelado, Escuro Padrão/Azulado/Dracula), todos com contraste WCAG AA verificado.
- Localizar / Localizar e substituir.
- Impressão com margens, orientação e escala configuráveis.
- Recuperação automática de conteúdo não salvo após fechamento inesperado.
- Menu completo (Arquivo / Editar) com atalhos de teclado reais.

## Como compilar

1. Instale o [Visual Studio](https://visualstudio.microsoft.com/) (Community serve).
2. No instalador, em **Cargas de trabalho**, marque **Desenvolvimento de aplicativo WinUI**.
3. Se o restore de pacotes reclamar não encontrando `Microsoft.WindowsAppSDK` ou `Microsoft.Windows.SDK.NET.Ref`, adicione o NuGet.org como fonte em **Ferramentas → Opções → Gerenciador de Pacotes NuGet → Fontes de Pacotes** (`https://api.nuget.org/v3/index.json`), caso ainda não esteja lá.
4. Abra `Slinky.slnx` e rode com F5.

Requer Windows 10 versão 1809 (build 17763) ou mais recente.

## Como instalar localmente

No Windows, execute `instalar-slinky.bat` na raiz do repositório. O script restaura as dependências, gera um pacote MSIX self-contained para a arquitetura da máquina, assina-o com um certificado de desenvolvimento local e instala o Slinky para o usuário atual.

Para apenas gerar o pacote sem instalar, execute `instalar-slinky.bat --build-only`. O arquivo será criado em `Slinky/AppPackages`.

O certificado de desenvolvimento é criado ou reutilizado no armazenamento do usuário atual (`CN=Alessandro`) e não é enviado para nenhum lugar. O script usa um certificado compatível com assinatura de pacotes, registra-o em `TrustedPeople` por meio do instalador de desenvolvimento do Windows e pode solicitar confirmação do UAC. Se o Windows bloquear sideloading, habilite o **Modo de Desenvolvedor** em **Configurações → Sistema → Para desenvolvedores**.

## Testes

Os testes automatizados (`Slinky.Tests`, xUnit) cobrem busca/substituição, contraste WCAG, estados de abas, transições entre temas e leitura/escrita de texto com diferentes codificações e finais de linha. O workflow do GitHub Actions executa os testes e também compila o aplicativo WinUI x64 em Release.

### Persistência de documentos

O editor mantém o texto internamente com finais de linha `LF`. Ao abrir um arquivo,
o Slinky detecta UTF-8, UTF-8 com BOM, UTF-16 LE/BE e Windows-1252, preservando a
codificação, o BOM e o estilo de final de linha ao salvar. Novos documentos usam
UTF-8 sem BOM e CRLF.

A sessão de arquivos abertos é separada do recovery de conteúdo não salvo. A sessão
usa `FutureAccessList`; o recovery usa snapshots JSON gravados de forma temporária e
substituídos atomicamente. Salvar remove o snapshot atual, mas o recovery continua
disponível para edições futuras. Fechar a aba desativa novos snapshots daquela nota.

### Temas, abas e superfície de edição

Cada aba aplica a paleta completa no momento em que é criada e novamente durante uma
troca de tema. A superfície do editor e os estados `PointerOver`/`Focused` do `TextBox`
usam a cor do tema, portanto passar o mouse sobre a área de texto não introduz uma cor
do sistema nem exige selecionar a aba para atualizar a aparência.

O hover do cabeçalho das abas permanece habilitado, mas seus estados normal, selecionado,
hover e hover selecionado usam as cores da paleta atual. Isso mantém a diferenciação
visual das abas sem voltar ao azul/branco ou preto/branco padrão do sistema.

## Sobre a capability `runFullTrust`

O `Package.appxmanifest` pede a capability `runFullTrust`. Isso é esperado: é o que permite empacotar um app Win32/desktop (não-UWP) como MSIX. Não concede nada além do que um `.exe` comum já teria fora de um pacote.

## Contribuições

Este é um projeto pessoal — código aberto para leitura e uso, mas **não estou aceitando pull requests** no momento. Issues relatando bugs são bem-vindas, só não esperem PR de correção sendo aceito.

## Licença

MIT — veja [`LICENSE`](LICENSE).
