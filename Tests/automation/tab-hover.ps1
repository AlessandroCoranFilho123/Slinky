# Smoke de NAO-REGRESSAO: cor do hover das abas em claro e escuro.
# O relato original (17/11): no modo escuro padrao, o hover da aba (principalmente
# a primeira) ficava AZUL. Causa: os recursos WinUI de estados nao sobrescritos
# (Pressed, Disabled, close button, botao "+", presenter ListView) resolvem para
# cores de accent do sistema. Correcao: AplicarRecursosAba aplica TODOS os
# recursos que o template consome, no escopo do TabView e de cada tabItem.
# Metodo: cursor real em cada area interativa do strip de abas, captura de TELA
# (CopyFromScreen — PrintWindow nao captura estados de hover) e contagem de
# pixels azul-accent. Qualquer pixel azul = falha.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
param([string]$PhotosDir)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32H2 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$dirScript = $PSScriptRoot
if (-not $PhotosDir) { $PhotosDir = Join-Path $dirScript ('..\..\docs\smoke-fotos\' + (Get-Date -Format 'yyyy-MM-dd')) }
New-Item -ItemType Directory -Path $PhotosDir -Force | Out-Null
$failures = @()

function Chord([byte]$vk) {
    [W32H2]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32H2]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32H2]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)
    [W32H2]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
}
function Foreground { foreach ($p in (Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })) { [W32H2]::SetForegroundWindow($p.MainWindowHandle) | Out-Null } }
function ContarAzuis($bmp) {
    $azuis = 0
    for ($y = 72; $y -lt 118; $y++) {
        for ($x = 8; $x -lt 1100; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.B -gt 100 -and $c.B -gt $c.R + 30 -and $c.B -gt $c.G + 10) { $azuis++ }
        }
    }
    return $azuis
}
function FotoStrip([string]$nome) {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $p) { return 0 }
    $rect = New-Object W32H2+RECT
    [W32H2]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    $azuis = ContarAzuis $bmp
    $bmp.Save((Join-Path $PhotosDir ($nome + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ('  ' + $nome + ': azuis=' + $azuis)
    if ($azuis -gt 0) { return $azuis } else { return 0 }
}
# troca de tema pelo menu Configurar (radios por AutomationId)
function SelecionarTema([string]$radioId) {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $r = [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
    $condBar = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::MenuItem)
    for ($t = 0; $t -lt 10; $t++) {
        $barras = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condBar))
        $arquivo = $barras | Where-Object { $_.Current.Name -eq 'Arquivo' } | Select-Object -First 1
        if (-not $arquivo) { Start-Sleep -Milliseconds 300; continue }
        try { $arquivo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { Start-Sleep -Milliseconds 300; continue }
        Start-Sleep -Milliseconds 700
        $condItem = New-Object Windows.Automation.AndCondition($condBar, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Configurar...')))
        $item = $r.FindFirst([Windows.Automation.TreeScope]::Descendants, $condItem)
        if ($item) {
            try { $item.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { }
            break
        }
        try { $arquivo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch { }
        Start-Sleep -Milliseconds 400
    }
    if (-not (Esperar {
        $condWin = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Window)
        $janelas = @([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, $condWin))
        $script:janelaTema = $janelas | Where-Object { $_.Current.Name -like '*tema*' } | Select-Object -First 1
        $null -ne $script:janelaTema
    } 10)) { return $false }
    $condRadio = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::RadioButton)
    $ok = $false
    for ($t = 0; $t -lt 10 -and -not $ok; $t++) {
        $radios = @($script:janelaTema.FindAll([Windows.Automation.TreeScope]::Descendants, $condRadio))
        $alvo = $radios | Where-Object { $_.Current.AutomationId -eq $radioId } | Select-Object -First 1
        if ($alvo) {
            try { $alvo.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); $ok = $true } catch { Start-Sleep -Milliseconds 300 }
        } else { Start-Sleep -Milliseconds 300 }
    }
    Start-Sleep -Milliseconds 800
    if ($script:janelaTema -and $script:janelaTema.Current.NativeWindowHandle -ne 0) {
        [W32H2]::PostMessage([IntPtr]$script:janelaTema.Current.NativeWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 600
    }
    return $ok
}
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}

# ---- limpar, abrir, 3 abas ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
$rec = Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\Recovery'
Remove-Item (Join-Path $rec '*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\session-*.json') -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
Start-Sleep -Seconds 8
Foreground
Start-Sleep -Seconds 1
Chord 0x54; Start-Sleep -Seconds 1
Chord 0x54; Start-Sleep -Seconds 1
$p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$rect = New-Object W32H2+RECT
[W32H2]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
# posicoes: centro do header de cada aba, cada "x", o "+" e a area vazia a direita
$pontos = @(
    @{ n = 'aba1-centro'; x = 135 }, @{ n = 'aba1-x'; x = 235 },
    @{ n = 'aba2-centro'; x = 375 }, @{ n = 'aba2-x'; x = 475 },
    @{ n = 'aba3-centro'; x = 615 }, @{ n = 'aba3-x'; x = 715 },
    @{ n = 'botao-plus'; x = 760 }, @{ n = 'area-vazia'; x = 850 }
)

# ---- ESCURO PADRAO ----
Write-Host '== tema escuro padrao =='
SelecionarTema 'DarkDefaultRadio' | Out-Null
Start-Sleep -Seconds 1
$idx = 0
foreach ($pt in $pontos) {
    $idx++
    [W32H2]::SetCursorPos($rect.Left + $pt.x, $rect.Top + 93) | Out-Null
    [W32H2]::SetCursorPos($rect.Left + $pt.x + 2, $rect.Top + 93) | Out-Null
    [W32H2]::SetCursorPos($rect.Left + $pt.x, $rect.Top + 93) | Out-Null
    Start-Sleep -Milliseconds 1000
    $azuis = FotoStrip ('escuro-' + $pt.n)
    if ($azuis -gt 0) { $failures += ('escuro/' + $pt.n + ': ' + $azuis + ' pixels azuis no hover') }
}

# ---- CLARO PADRAO ----
Write-Host '== tema claro padrao =='
SelecionarTema 'LightDefaultRadio' | Out-Null
Start-Sleep -Seconds 1
$idx = 0
foreach ($pt in $pontos) {
    $idx++
    [W32H2]::SetCursorPos($rect.Left + $pt.x, $rect.Top + 93) | Out-Null
    [W32H2]::SetCursorPos($rect.Left + $pt.x + 2, $rect.Top + 93) | Out-Null
    [W32H2]::SetCursorPos($rect.Left + $pt.x, $rect.Top + 93) | Out-Null
    Start-Sleep -Milliseconds 1000
    $azuis = FotoStrip ('claro-' + $pt.n)
    if ($azuis -gt 0) { $failures += ('claro/' + $pt.n + ': ' + $azuis + ' pixels azuis no hover') }
}

# ---- voltar ao escuro (tema do usuario) ----
SelecionarTema 'DarkDefaultRadio' | Out-Null
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'TAB-HOVER SEM AZUL PASS'
