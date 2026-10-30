# AGENTS.md — Guia de trabalho para agentes de IA

Este arquivo é o ponto de entrada para qualquer agente (Codex, Claude, Kimi, GLM/ZCode)
trabalhando neste repositório. Ele resume o essencial e aponta para a documentação
completa. Mantenha-o atualizado ao mudar o ambiente, os comandos ou o estado do projeto.

## O que é o projeto

Slinky é um editor de texto leve para Windows em WinUI 3 (C#/.NET 8), com abas,
cinco temas com contraste WCAG AA, busca/substituição, impressão e recuperação de
conteúdo não salvo. Detalhes: [`README.md`](README.md) e
[`docs/AUDITORIA-ARQUITETURA-E-PLANO.md`](docs/AUDITORIA-ARQUITETURA-E-PLANO.md).

## Mapa do repositório

- `Slinky/` — app WinUI 3 (`Slinky.csproj`, `Package.appxmanifest`, serviços de
  sessão, recovery, arquivos recentes e tema).
- `Tests/` — xUnit headless (`Slinky.Tests.csproj`): persistência, busca, contraste,
  paginação, estados de aba e temas. Nenhum teste instancia janelas WinUI.
- `scripts/` — `Prepare-DevelopmentCertificate.ps1` (cert local `CN=Alessandro`) e
  `Install-ReleasePackage.ps1` (instala bundle de release, exige UAC).
- `instalar-slinky.bat` — fluxo local completo: cert + build assinado self-contained +
  registro do cert nas stores de máquina (UAC) + `Add-AppxPackage`.
- `.github/workflows/tests.yml` — CI: testes, builds x86/x64/ARM64, MSIX x64 assinado
  com cert efêmero, smoke de instalação e build trimmed diagnóstico.
- `.github/workflows/release.yml` — packages assinados por arquitetura
  (cert efêmero `CN=Slinky Release`) publicados como release.

## Ambiente (máquina do Alessandro, verificado em 28/10/2026)

- Windows 10 19045 x64; shell Git Bash.
- .NET SDK `11.0.100-rc.2.26504.105`; alvo `net8.0-windows10.0.19041.0` (mínimo 17763).
- Apenas Windows PowerShell 5.1 (`powershell.exe`). **Não há `pwsh` nem `gh` instalados.**
- Modo de desenvolvedor ativo (`AllowAllTrustedApps=1`); AppXSvc em execução.
- Certificados de assinatura locais: dois `CN=Alessandro` em `CurrentUser\My` com
  chave privada e EKU de assinatura de código, válidos até 14/10/2027:
  - `307E3EC24E852C4588156FDB999433A1CCC11BC0` (usado na instalação de 28/10;
    também está nas stores de máquina `TrustedPeople` e `Root`)
  - `DF0BAEE0AD5C5F59F40443B4148B77D445B7BCB1`
- O `instalar-slinky.bat` reutiliza o certificado existente mais novo, não cria outro.

## Comandos essenciais

```bash
# Testes (devem passar antes de qualquer commit)
dotnet test Tests/Slinky.Tests.csproj -c Release

# Build Release x64
dotnet build Slinky/Slinky.csproj -c Release -p:Platform=x64
```

MSIX assinado self-contained (receita idêntica à da CI; troque o thumbprint se o
certificado mudou):

```bash
dotnet build Slinky/Slinky.csproj -c Release --no-restore \
  -p:Platform=x64 -p:RuntimeIdentifier=win-x64 \
  -p:GenerateAppxPackageOnBuild=true -p:PublishReadyToRun=false \
  -p:PublishTrimmed=false -p:WindowsAppSDKSelfContained=true \
  -p:AppxPackageSigningEnabled=true \
  -p:PackageCertificateThumbprint=307E3EC24E852C4588156FDB999433A1CCC11BC0
# Saída: Slinky/AppPackages/Slinky_1.0.4.0_Test/*.msixbundle
```

Instalação e lançamento (PowerShell) — leia [`docs/INSTALACAO-MSIX.md`](docs/INSTALACAO-MSIX.md)
antes de instalar pela primeira vez:

```powershell
Add-AppxPackage -Path <bundle> -ForceUpdateFromAnyVersion
Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
```

Avisos de build benignos: `mspdbcmf.exe` não encontrado (pacote de símbolos) e
`PRI249 DESKTOP-EVENTLOG-INSTRUMENTATION`.

## Estado atual (29/10/2026)

- Plano de correções (`docs/PLANO-CORRECOES-E-CHECKLIST.md`): **100% concluído**,
  incluindo o smoke UI completo (automação UIA/SendInput) e a correção de dois
  defeitos encontrados por ele (diálogo duplo `Ctrl+W`, indicador de foco).
- Versão atual: **1.0.4.0** (release `v1.0.4` no GitHub; anterior v1.0.3).
- **Release notes**: fonte única em [`RELEASE-NOTES.md`](RELEASE-NOTES.md) — atualize a
  seção da nova release ANTES de criar a tag (`release.yml` usa `body_path` + changelog
  automático). Para editar o corpo de uma release já publicada é preciso token com
  escopo `repo` (a credencial do git local não tem — tentativa de 29/10 falhou com 404).
- Decisões de comportamento fechadas no §15 da auditoria (undo/`IsDirty`, arquivo
  alterado externamente, Windows-1252, janelas/processos) e **recuperação
  reconectada ao arquivo original** (tokens FAL nos snapshots).
- O `.csproj` usa **referências granulares do Windows App SDK** (WinUI + Runtime +
  DWrite), sem AI/ML/Widgets — não voltar ao metapacote (~40 MB de payload não
  usado). Fundamento e números: `docs/DESEMPENHO-E-TAMANHO.md`.
- Linha de base de desempenho (máquina local): CPU ociosa 0%, Private Working Set
  ~51–67 MB conforme sessão (piso do WinUI 3 medido: 44,7 MB com app vazio),
  partida aquecida ~0,4 s até janela / ~0,65 s até editor; 204 MB instalados
  (self-contained).
- Pacote instalado nesta máquina:
  `46cebc95-b2f1-4e6a-819f-0227204d1c62_1.0.4.0_x64__1xsa4dc8b90qj`
  (publisher `CN=Alessandro`, assinatura Developer). O AUMID da seção anterior
  corresponde a essa família.
- Histórico e causas raiz dos erros de instalação: `docs/INSTALACAO-MSIX.md`.

## Regras e convenções

- Commits em inglês no formato conventional (`fix:`, `test:`, `chore:`, `docs:`,
  `ci:`), um grupo funcional por commit.
- Documentação em PT-BR. Um item do checklist só é marcado com evidência (código,
  teste aprovado ou registro de smoke com data).
- Produção permanece **sem trimming** (sessão/recovery usam `System.Text.Json`
  reflexivo); build trimmed existe só como diagnóstico na CI.
- Nunca commitar `.cer`/`.pfx` (cobertos pelo `.gitignore`); a CI gera certificado
  efêmero por execução.
- Ao trocar de tema/paleta, rodar os testes de contraste — AA é requisito.

## Documentação de referência

| Arquivo | Conteúdo |
|---|---|
| [`docs/AUDITORIA-ARQUITETURA-E-PLANO.md`](docs/AUDITORIA-ARQUITETURA-E-PLANO.md) | Auditoria completa da arquitetura e plano original |
| [`docs/PLANO-CORRECOES-E-CHECKLIST.md`](docs/PLANO-CORRECOES-E-CHECKLIST.md) | Checklist de execução com evidências por item |
| [`docs/INSTALACAO-MSIX.md`](docs/INSTALACAO-MSIX.md) | Erros de MSIX: causas raiz, evidências e receita de instalação verificada |
| [`docs/SMOKE-UI.md`](docs/SMOKE-UI.md) | Roteiro de smoke test visual a executar no app instalado |
| [`docs/SMOKE-TESTES-2026-10-22.md`](docs/SMOKE-TESTES-2026-10-22.md) | Registro da tentativa de 22/10 e dos bloqueios da época |
| [`docs/SMOKE-TESTES-2026-10-28.md`](docs/SMOKE-TESTES-2026-10-28.md) | Registro de 28–29/10: testes, build, MSIX, instalação e lançamento |
| [`docs/DESEMPENHO-E-TAMANHO.md`](docs/DESEMPENHO-E-TAMANHO.md) | Medições de CPU/RAM/partida/tamanho, experimentos (FDD, GC) e decisões |
| [`docs/QA-2026-10-30.md`](docs/QA-2026-10-30.md) | QA profundo: codificações, arquivos grandes (limites), stress, integridade de dados, segurança |
