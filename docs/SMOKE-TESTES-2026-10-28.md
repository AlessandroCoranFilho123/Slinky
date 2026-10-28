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

## Testes de interface

Continuam pendentes neste ambiente, pelos mesmos motivos registrados em
`SMOKE-TESTES-2026-10-22.md` (WinUI self-contained exige identidade de pacote e o
módulo AppX local não conclui a instalação). O roteiro a executar está em
`SMOKE-UI.md`, incluindo o indicador de foco nos cinco temas e as duas instâncias
simultâneas para recovery.
