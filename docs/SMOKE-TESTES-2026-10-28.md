# Smoke tests — 28/10/2026

## Ambiente

- Windows com .NET SDK `11.0.100-rc.2.26504.105` (runtime alvo `net8.0-windows10.0.19041.0`).
- Runtime alvo: `win-x64`.

## Resultados executados

| Verificação | Resultado |
|---|---|
| `dotnet test Tests/Slinky.Tests.csproj -c Release` | Aprovado: 72/72 (inclui persistência, concorrência, contraste e paginação) |
| Build Release x64 via `dotnet build` | Aprovado: 0 erros, 0 avisos |
| Geração de MSIX x64 (`GenerateAppxPackageOnBuild`, SideloadOnly) | Aprovado: bundle gerado |
| Auditoria de assets do pacote MSIX | Aprovada: 56 assets; todos os logos referenciados pelo manifesto presentes |
| `git diff --check` | Aprovado |

## Auditoria de assets do manifest

Após remover do `.csproj` as entradas `None Remove` mortas de `BadgeLogo` e excluir
os assets sem referência (`LockScreenLogo.scale-200.png`, `StoreLogo.backup.png`),
o bundle foi recriado e inspecionado:

- Presentes: `StoreLogo`, `BadgeLogo` (escalas 100–400), `LargeTile`, `SmallTile`,
  `Wide310x150Logo`, `SplashScreen`, `Square150x150Logo`, `Square44x44Logo`
  (escalas e variantes unplated) e `AppIcon.ico` (carregado em runtime por
  `WindowIconService`).
- Ausentes, como esperado: `StoreLogo.backup.png` e `LockScreenLogo.scale-200.png`,
  que antes eram empacotados indevidamente pelo globbing do MSIX.

O único aviso do build foi ambiental (`mspdbcmf.exe` ausente impede o pacote de
símbolos) e não afeta o aplicativo.

## Instalação MSIX e lançamento (complemento da mesma data)

O bloqueio registrado em `SMOKE-TESTES-2026-10-22.md` foi resolvido no mesmo dia
28/10, com diagnóstico pelo log de eventos do AppX. Resumo (detalhes e causas raiz
em [`INSTALACAO-MSIX.md`](INSTALACAO-MSIX.md)):

1. Build assinado com a receita da CI (`WindowsAppSDKSelfContained=true` +
   `PackageCertificateThumbprint` do certificado local `CN=Alessandro`).
2. Importação do certificado para `LocalMachine\TrustedPeople` e `Root` com UAC
   aprovado — a confiança só no usuário não basta para o AppXSvc.
3. Remoção da família antiga do release (`…_b2zjezss9gtqm`, publisher
   `CN=Slinky Release`) que conflitava com a família dev (`0x80073CF3`).
4. `Add-AppxPackage` aprovado: pacote
   `46cebc95-…_1.0.0.0_x64__1xsa4dc8b90qj` instalado (assinatura Developer).
5. Lançamento via `shell:AppsFolder\…!App` confirmado: processo em execução
   com janela ativa.

## Testes de interface

Com o app instalado e lançando, resta apenas **executar o roteiro visual** de
`SMOKE-UI.md` (foco nos cinco temas, busca, atalhos, impressão e duas instâncias
simultâneas para recovery) e registrar os resultados aqui.
