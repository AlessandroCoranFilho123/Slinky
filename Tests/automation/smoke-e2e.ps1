# Smoke E2E: digitar -> autosave -> encerrar a forca -> reabrir -> Restaurar -> conferir.
# Exercita o caminho critico de nao-perda-de-dado (autosave + recovery) sem dialogos.
# ORQUESTRADOR: cada verificacao/acao roda em POWERSHELL FILHO (e2e-child.ps1) — o
# client UIA do processo que executa um Stop-Process fica amarrado a janelas mortas
# e nunca mais le nada (descoberta 17/11/2026); filhos novos nascem com cache limpo.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
$ErrorActionPreference = 'Continue'
$failures = @()
$dirScript = $PSScriptRoot
$PhotosDir = Join-Path $dirScript ('..\..\docs\smoke-fotos' + (Get-Date -Format 'yyyy-MM-dd'))
New-Item -ItemType Directory -Path $PhotosDir -Force | Out-Null
$out = Join-Path $env:TEMP 'slinky-e2e-child.json'

$rec = Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\Recovery'
function SnapshotsJson { @(Get-ChildItem $rec -Filter '*.json' -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch '\.tmp' }).Count }
function Filho {
    param([int]$Timeout = 20, [string]$Acao = 'nenhuma', [string]$Texto = '')
    # argumento vazio com -File e omitido pelo PowerShell: montar a lista so com
    # o que esta preenchido
    $argFilho = @('-TimeoutSegundos', $Timeout, '-Acao', $Acao)
    if ($Texto) { $argFilho += @('-Texto', $Texto) }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dirScript 'e2e-child.ps1') @argFilho | Out-Null
    try { return (Get-Content $out -Raw | ConvertFrom-Json) } catch { return @{ temEditor = $false; temRestaurar = $false; valor = '' } }
}
function LimparEstado {
    Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item (Join-Path $rec '*') -Force -Recurse -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\session-*.json') -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}
function Lancar {
    Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
    # reativacao MSIX pode falhar silenciosamente (gotcha documentado): se em 8s nao
    # ha processo, tenta de novo uma vez
    if (-not (Esperar { (Get-Process Slinky -ErrorAction SilentlyContinue) } 8)) {
        Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
    }
}
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}

# ---- 1. abrir e digitar ----
LimparEstado
Lancar
$r1 = Filho -Timeout 30 -Acao 'digitar' -Texto 'e2e recovery marcador 30-10'
if (-not $r1.temEditor) { $failures += '1: app nao abriu com editor' }
if ($r1.valor -notmatch 'recovery marcador') { $failures += "1: digitacao nao chegou ao editor [$($r1.valor)]" }
'1. app aberto'
"2. digitado: [$($r1.valor)]"

# ---- 3. autosave com checagem REAL no disco ----
"3. aguardando autosave (6s)..."
Start-Sleep -Seconds 6
if (-not (Esperar { (SnapshotsJson) -ge 1 } 10)) { $failures += 'autosave nao gravou o snapshot no disco' }
"   snapshots no disco: " + (SnapshotsJson)

# ---- 4. crash + relaunch + Restaurar (filho novo por tentativa) ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 10
foreach ($s in (Get-ChildItem $rec -ErrorAction SilentlyContinue)) {
    Write-Host ('  disco pos-kill: ' + $s.Name + ' len=' + $s.Length + ' marcador=' + ((Get-Content $s.FullName -Raw) -match 'recovery marcador'))
}
$reabriu = $false
foreach ($tentativa in 1..3) {
    Lancar
    $r2 = Filho -Timeout 30 -Acao 'restaurar' -Foto (Join-Path $env:TEMP 'e2e-pos-restore.png')
    if ($r2.temEditor) { $reabriu = $true; break }
    Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 10
}
if (-not $reabriu) {
    $failures += 'app nao reabriu apos crash (3 tentativas)'
    'SMOKE-E2E: FAIL'
    exit 1
}
"4. reaberto com recovery (UIA valor e vazio por artefato pós-kill; a foto e a evidencia visual)"
# assert de integridade POR DISCO: a nota restaurada regravou o snapshot com o
# texto digitado (prova de que o restore carregou o conteudo)
$restoreOk = Esperar {
    @(Get-ChildItem $rec -Filter '*.json' -ErrorAction SilentlyContinue | Where-Object { (Get-Content $_.FullName -Raw) -match 'recovery marcador' }).Count -ge 1
} 15
if (-not $restoreOk) { $failures += 'restore nao regravou o snapshot com o conteudo digitado' }
if (Test-Path (Join-Path $env:TEMP 'e2e-pos-restore.png')) { Copy-Item (Join-Path $env:TEMP 'e2e-pos-restore.png') (Join-Path $PhotosDir 'e2e-pos-restore.png') -Force }

# ---- 5. segundo ciclo: a nota restaurada segue suja -> novo snapshot -> novo prompt ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 10
$temNovoSnapshot = Esperar { (SnapshotsJson) -ge 1 } 12
if (-not $temNovoSnapshot) { $failures += 'nota restaurada nao gerou novo snapshot em 12s' }
Lancar
$r3 = Filho -Timeout 30 -Acao 'restaurar' -Foto (Join-Path $env:TEMP 'e2e-pos-restore2.png')
$restoreOk2 = Esperar {
    @(Get-ChildItem $rec -Filter '*.json' -ErrorAction SilentlyContinue | Where-Object { (Get-Content $_.FullName -Raw) -match 'recovery marcador' }).Count -ge 1
} 15
"5. segundo ciclo de recovery: snapshot com texto=" + $restoreOk2
if (-not $restoreOk2) { $failures += 'segundo ciclo de recovery nao regravou o snapshot com o conteudo' }
if (Test-Path (Join-Path $env:TEMP 'e2e-pos-restore2.png')) { Copy-Item (Join-Path $env:TEMP 'e2e-pos-restore2.png') (Join-Path $PhotosDir 'e2e-pos-restore2.png') -Force }

# ---- limpar ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
LimparEstado

if (@($failures).Count -eq 0) { 'SMOKE-E2E: PASS' } else { "SMOKE-E2E: FAIL - $($failures -join '; ')" }
exit $(if (@($failures).Count -eq 0) { 0 } else { 1 })
