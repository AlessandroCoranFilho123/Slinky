# Smoke tests — 22/10/2026

## Ambiente

- Windows com Visual Studio 18 Insiders / MSBuild 18.12.2.
- .NET SDK `11.0.100-rc.2.26504.105` selecionado pelo ambiente.
- Runtime alvo: `win-x64`.

## Resultados executados

| Verificação | Resultado |
|---|---|
| `dotnet test Tests/Slinky.Tests.csproj -c Release --no-restore` | Aprovado: 72/72 |
| Build Release x64 via MSBuild | Aprovado: 0 erros, 0 avisos |
| Geração de MSIX x64 via `dotnet build` | Aprovado: bundle x64 gerado |
| Validação XML do projeto e JSON de launch settings | Aprovado |
| `git diff --check` | Aprovado |

## Testes de interface

Os testes de interface não puderam ser concluídos neste ambiente:

1. O executável unpackaged atual encerra com `0xC0000135`, pois WinUI self-contained
   precisa da identidade/registro de pacote para executar corretamente.
2. O MSIX foi gerado, mas a instalação não pôde ser concluída: o módulo AppX do
   ambiente retorna `Operation is not supported on this platform (0x80131539)` e o
   instalador tentou elevar por um caminho indisponível.
3. A automação de janelas confirmou que nenhuma janela do Slinky estava disponível;
   portanto não foram marcados como aprovados os cenários de foco, busca, tema,
   impressão, atalhos ou duas instâncias.

## Correção descoberta durante a preparação

O XAML compiler rejeitou `OemPlus` e `OemMinus` como valores de `VirtualKey`. Esses
atalhos foram movidos para registro em runtime usando os códigos virtuais OEM `0xBB`
e `0xBD`. Depois da alteração, o build MSBuild e a geração do MSIX passaram.

## Próximo passo para completar a validação UI

Executar o MSIX em uma máquina Windows com suporte funcional ao AppX/Windows App SDK,
instalar o pacote assinado e repetir os cenários definidos no checklist, incluindo
duas instâncias simultâneas e fechamento inesperado para recovery.
