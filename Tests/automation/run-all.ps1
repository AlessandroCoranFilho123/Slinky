# Comando unico de validacao: testes headless + E2E de fumaca.
# Uso: powershell -ExecutionPolicy Bypass -File Tests\automation\run-all.ps1
$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

"=== 1/2 testes headless (xUnit) ==="
& dotnet test (Join-Path $repo 'Tests\Slinky.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { "FALHA nos testes headless"; exit 1 }

"=== 2/2 build Release x64 ==="
& dotnet build (Join-Path $repo 'Slinky\Slinky.csproj') -c Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { "FALHA no build"; exit 1 }

"TUDO VERDE (headless + build)"
"Nota: smoke-e2e.ps1 e as baterias de UI (encodings/stress) sao execucoes manuais — UIA por sleep e instavel; ver Testsutomation\README.md"
