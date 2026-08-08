# Slinky

Um editor de texto leve para Windows, feito com WinUI 3. Nasceu da vontade de ter algo do tamanho e da simplicidade do Bloco de Notas clássico, mas com suporte nativo a `.txt` e `.md` — alternando entre texto puro e formatado com um clique — e abas, em vez de uma janela por arquivo.

> _(coloque aqui um screenshot ou GIF do app — a tela com uma nota aberta em Markdown formatado costuma vender bem a ideia)_

## Funcionalidades

- Abas — várias notas na mesma janela, sem multiplicar processos.
- `.txt`/`.md` nativo, com alternância entre texto puro e Markdown renderizado (parser próprio, sem dependência externa).
- 5 temas (Claro Padrão/Amarelado, Escuro Padrão/Azulado/Dracula), todos com contraste WCAG AA verificado.
- Localizar / Localizar e substituir.
- Impressão com margens, orientação e escala configuráveis.
- Recuperação automática de conteúdo não salvo após fechamento inesperado.
- Menu completo (Arquivo / Editar / Formatar) com atalhos de teclado reais.

## Como compilar

1. Instale o [Visual Studio](https://visualstudio.microsoft.com/) (Community serve).
2. No instalador, em **Cargas de trabalho**, marque **Desenvolvimento de aplicativo WinUI**.
3. Se o restore de pacotes reclamar não encontrando `Microsoft.WindowsAppSDK` ou `Microsoft.Windows.SDK.NET.Ref`, adicione o NuGet.org como fonte em **Ferramentas → Opções → Gerenciador de Pacotes NuGet → Fontes de Pacotes** (`https://api.nuget.org/v3/index.json`), caso ainda não esteja lá.
4. Abra `Slinky.slnx` e rode com F5.

Requer Windows 10 versão 1809 (build 17763) ou mais recente.

## Testes

Os testes automatizados (`Slinky.Tests`, xUnit) cobrem a lógica que não depende de WinRT — o parser de Markdown e o cálculo de contraste WCAG dos temas. Abra o **Test Explorer** no Visual Studio depois de compilar a solução.

## Sobre a capability `runFullTrust`

O `Package.appxmanifest` pede a capability `runFullTrust`. Isso é esperado: é o que permite empacotar um app Win32/desktop (não-UWP) como MSIX. Não concede nada além do que um `.exe` comum já teria fora de um pacote.

## Contribuições

Este é um projeto pessoal — código aberto para leitura e uso, mas **não estou aceitando pull requests** no momento. Issues relatando bugs são bem-vindas, só não esperem PR de correção sendo aceito.

## Licença

_(a definir — veja `CHECKLIST.md`)_
