# Smoke: atalhos globais e tooltip de acelerador.
# 1) Ctrl+T / Ctrl+N / Ctrl+F precisam continuar funcionando (atalhos vivos).
# 2) Nenhuma tooltip de acelerador ("Ctrl+N" flutuando) pode aparecer ao deixar
#    o mouse parado sobre a barra de titulo (area nua do Grid raiz, onde os
#    aceleradores estao registrados). A tooltip NAO e elemento UIA ToolTip e
#    sai quase toda acima do topo da janela — deteccao por pixel-diff de
#    captura de tela com margem ao redor da janela.
#    Causa raiz e correcao: docs/ATALHOS-TOOLTIP-2026-11-11.md
#    (KeyboardAcceleratorPlacementMode="Hidden" no Grid raiz).
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32K {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

$family = '46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj'
$aumid = "$family!App"
$failures = @()

function Janela {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
    if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
    [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function Contar($r, [Windows.Automation.ControlType]$tipo) {
    $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, $tipo)
    @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $cond)).Count
}
function Abas { Contar (Janela) ([Windows.Automation.ControlType]::TabItem) }
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}
function Lancar {
    Start-Process "shell:AppsFolder\$aumid"
    return (Esperar { $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1; $p -and $p.MainWindowHandle -ne 0 -and (Contar ([Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)) ([Windows.Automation.ControlType]::Edit)) -gt 0 } 30)
}
function Chord([byte]$vk) {
    [W32K]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)        # Ctrl down
    Start-Sleep -Milliseconds 60
    [W32K]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)         # tecla down
    Start-Sleep -Milliseconds 60
    [W32K]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)         # tecla up
    [W32K]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)        # Ctrl up
}
# Captura de tela com margem de 120px ao redor da janela: a tooltip de
# acelerador sai quase toda acima do topo da janela (fora do rect).
function FrameComMargem($hwnd) {
    $rect = New-Object W32K+RECT
    [W32K]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $m = 120
    $x0 = [Math]::Max(0, $rect.Left - $m); $y0 = [Math]::Max(0, $rect.Top - $m)
    $cw = [Math]::Min([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, $rect.Right - $rect.Left + 2 * $m)
    $ch = [Math]::Min([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height, $rect.Bottom - $rect.Top + 2 * $m)
    $bmp = New-Object System.Drawing.Bitmap($cw, $ch)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x0, $y0, 0, 0, $bmp.Size)
    $g.Dispose()
    return $bmp
}
# Conta pixels diferentes, mascarando a borda esquerda da janela (artefato de
# renderizacao observado nas capturas) e o cursor (posicionado na barra de titulo).
function DifsTooltip($base, $frame) {
    $difs = 0
    $w = $base.Width; $h = $base.Height
    for ($y = 0; $y -lt $h; $y += 2) {
        for ($x = 0; $x -lt $w; $x += 2) {
            if ($x -ge 118 -and $x -le 128) { continue }   # borda esquerda da janela
            $p1 = $base.GetPixel($x, $y); $p2 = $frame.GetPixel($x, $y)
            if ([Math]::Abs($p1.R - $p2.R) -gt 12 -or [Math]::Abs($p1.G - $p2.G) -gt 12 -or [Math]::Abs($p1.B - $p2.B) -gt 12) { $difs++ }
        }
    }
    return $difs
}

# ---- 1. limpeza e app aberto ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
$ls = Join-Path $env:LOCALAPPDATA "Packages\$family\LocalState"
Remove-Item (Join-Path $ls 'Recovery\*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ls 'session-*.json') -Force -Recurse -ErrorAction SilentlyContinue
Start-Sleep -Seconds 10
if (-not (Lancar)) { 'FALHA: app nao abriu'; exit 1 }
$p = Get-Process Slinky | Sort-Object StartTime | Select-Object -First 1
$hwnd = [IntPtr]$p.MainWindowHandle
[W32K]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Seconds 3
'1. app aberto; abas=' + (Abas)

# ---- 2. Ctrl+T cria aba (com retry: reativacao MSIX intermitente) ----
Chord 0x54
if (-not (Esperar { (Abas) -eq 2 } 10)) {
    [W32K]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Seconds 2
    Chord 0x54
}
if (-not (Esperar { (Abas) -eq 2 } 10)) { $failures += 'Ctrl+T nao criou aba' }
'2. apos Ctrl+T: abas=' + (Abas)

# ---- 3. Ctrl+N com aba suja abre a confirmacao de alteracoes (roteamento vivo) ----
$r = Janela
$condEdit = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)
$editor = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condEdit))[$(@($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condEdit)).Count - 1)]
$editor.SetFocus() | Out-Null
Start-Sleep -Milliseconds 400
$ws = New-Object -ComObject WScript.Shell
$ws.SendKeys('x')   # torna a aba suja
Start-Sleep -Milliseconds 300
Chord 0x4E
$botaoCancelar = $null
if (-not (Esperar {
    $condBtn = New-Object Windows.Automation.AndCondition(
        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Button)),
        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Cancelar')))
    $script:botaoCancelar = (Janela).FindFirst([Windows.Automation.TreeScope]::Descendants, $condBtn)
    $null -ne $script:botaoCancelar
} 10)) { $failures += 'Ctrl+N nao abriu a confirmacao de alteracoes nao salvas (roteamento morto?)' }
'3. apos Ctrl+N com aba suja: dialogo de confirmacao=' + ($null -ne $botaoCancelar)
if ($botaoCancelar) {
    $botaoCancelar.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600
    # apaga o 'x' de volta (aba limpa de novo)
    $editor.SetFocus() | Out-Null
    Start-Sleep -Milliseconds 300
    $ws.SendKeys('{BS}')
    Start-Sleep -Milliseconds 400
}

# ---- 4. Ctrl+F abre a busca; Esc fecha ----
$editsAntes = Contar (Janela) ([Windows.Automation.ControlType]::Edit)
Chord 0x46
if (-not (Esperar { (Contar (Janela) ([Windows.Automation.ControlType]::Edit)) -gt $editsAntes } 10)) { $failures += 'Ctrl+F nao abriu a busca' }
'4. apos Ctrl+F: edits=' + (Contar (Janela) ([Windows.Automation.ControlType]::Edit)) + ' (antes=' + $editsAntes + ')'
$ws.SendKeys('{ESC}')
if (-not (Esperar { (Contar (Janela) ([Windows.Automation.ControlType]::Edit)) -le $editsAntes } 8)) { 'nota: busca nao fechou com Esc (cosmetico)' }
[W32K]::SetForegroundWindow($hwnd) | Out-Null

# ---- 5. tooltip de acelerador: mouse parado na barra de titulo (area nua do Grid) ----
$rect = New-Object W32K+RECT
[W32K]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
[W32K]::SetCursorPos([int](($rect.Left + $rect.Right) * 0.50), [int]($rect.Top + 20)) | Out-Null
Start-Sleep -Milliseconds 500
$base = FrameComMargem $hwnd
$tooltipPixels = 0
for ($i = 1; $i -le 8; $i++) {
    Start-Sleep -Milliseconds 700
    $f = FrameComMargem $hwnd
    $tooltipPixels = DifsTooltip $base $f
    $f.Dispose()
    if ($tooltipPixels -gt 100) { break }
}
'5. pixels alterados com mouse parado na barra de titulo: ' + $tooltipPixels
if ($tooltipPixels -gt 100) { $failures += 'tooltip de acelerador apareceu no hover da barra de titulo (' + $tooltipPixels + ' pixels)' } else { '6. sem tooltip de acelerador' }
$base.Dispose()

Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'ATALHOS+SEM-TOOLTIP PASS'
