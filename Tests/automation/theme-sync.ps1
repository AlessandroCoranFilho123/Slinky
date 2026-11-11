# Smoke: sincronizacao de tema entre duas instancias do app.
# Cenario: janela A e janela B abertas; troca o tema em B; ao ATIVAR A, o tema
# deve acompanhar (re-leitura do LocalSettings compartilhado — ThemeSync).
# Verificacao pixel-exata do fundo do editor contra ThemePalette.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

$family = '46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj'
$aumid = "$family!App"
$failures = @()

# Fundos (editor) de cada tema em ThemePalette.cs
$paletas = @{
    'LightDefault'   = [System.Drawing.Color]::FromArgb(255, 0xFF, 0xFF, 0xFF)
    'LightYellowish' = [System.Drawing.Color]::FromArgb(255, 0xF4, 0xEC, 0xD8)
    'DarkDefault'    = [System.Drawing.Color]::FromArgb(255, 0x1E, 0x1E, 0x1E)
    'DarkBluish'     = [System.Drawing.Color]::FromArgb(255, 0x10, 0x26, 0x3B)
    'DarkDracula'    = [System.Drawing.Color]::FromArgb(255, 0x28, 0x2A, 0x36)
}

function JanelaPorPid([int]$procId) {
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
    [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function TemEditor($r) {
    if (-not $r) { return $false }
    $edits = $r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))
    @($edits).Count -gt 0
}
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}
function Lancar {
    Start-Process "shell:AppsFolder\$aumid"
    return (Esperar { $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -Last 1; $p -and (TemEditor (JanelaPorPid $p.Id)) } 30)
}

# Abre Arquivo -> item do menu via UIA (ExpandCollapse no MenuBarItem).
function ItemDeMenu($raiz, [string]$itemTexto) {
    $condBar = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::MenuItem)
    for ($t = 0; $t -lt 10; $t++) {
        $barras = @($raiz.FindAll([Windows.Automation.TreeScope]::Descendants, $condBar))
        $arquivo = $barras | Where-Object { $_.Current.Name -eq 'Arquivo' } | Select-Object -First 1
        if (-not $arquivo) { Start-Sleep -Milliseconds 300; continue }
        try { $arquivo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { Start-Sleep -Milliseconds 300; continue }
        Start-Sleep -Milliseconds 700
        $condItem = New-Object Windows.Automation.AndCondition($condBar, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $itemTexto)))
        $item = $raiz.FindFirst([Windows.Automation.TreeScope]::Descendants, $condItem)
        if ($item) {
            try { $item.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); return $true } catch {}
        }
        try { $arquivo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch {}
        Start-Sleep -Milliseconds 400
    }
    return $false
}

# Janela "Configurar tema" pertencente ao processo informado.
function JanelaTema([int]$procId) {
    $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Window)
    $janelas = @([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, $cond))
    return $janelas | Where-Object { $_.Current.ProcessId -eq $procId -and $_.Current.Name -like '*tema*' } | Select-Object -First 1
}
function SelecionarTema([int]$procId, [string]$radioId) {
    if (-not (Esperar { $null -ne (JanelaTema $procId) } 10)) { return $false }
    $tw = JanelaTema $procId
    $condRadio = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::RadioButton)
    $ok = $false
    for ($t = 0; $t -lt 10 -and -not $ok; $t++) {
        $radios = @($tw.FindAll([Windows.Automation.TreeScope]::Descendants, $condRadio))
        $alvo = $radios | Where-Object { $_.Current.AutomationId -eq $radioId } | Select-Object -First 1
        if ($alvo) {
            try { $alvo.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); $ok = $true } catch { Start-Sleep -Milliseconds 300 }
        } else { Start-Sleep -Milliseconds 300 }
    }
    Start-Sleep -Milliseconds 600
    if ($tw) { [W32]::PostMessage([IntPtr]$tw.Current.NativeWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
    Start-Sleep -Milliseconds 500
    return $ok
}

# Cor do fundo do editor: PrintWindow flag 2 (renderiza WinUI); probe no canto
# inferior esquerdo (longe da barra de status, que fica a direita).
function Probe($hwnd) {
    $rect = New-Object W32+RECT
    [W32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { return [System.Drawing.Color]::Empty }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [W32]::PrintWindow($hwnd, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $cor = $bmp.GetPixel(40, $h - 40)
    $bmp.Dispose()
    return $cor
}
function NomeDoTema($cor) {
    foreach ($k in $paletas.Keys) {
        if ($cor.R -eq $paletas[$k].R -and $cor.G -eq $paletas[$k].G -and $cor.B -eq $paletas[$k].B) { return $k }
    }
    return $null
}

# ---- 1. limpeza e duas instancias ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
$ls = Join-Path $env:LOCALAPPDATA "Packages\$family\LocalState"
Remove-Item (Join-Path $ls 'Recovery\*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ls 'session-*.json') -Force -Recurse -ErrorAction SilentlyContinue
Start-Sleep -Seconds 10

if (-not (Lancar)) { 'FALHA: instancia A nao abriu'; exit 1 }
$aProc = Get-Process Slinky | Sort-Object StartTime | Select-Object -First 1
'1. instancia A aberta (PID ' + $aProc.Id + ')'

if (-not (ItemDeMenu (JanelaPorPid $aProc.Id) 'Nova janela')) { $failures += 'menu Nova janela nao invocado' }
if (-not (Esperar { @(Get-Process Slinky -ErrorAction SilentlyContinue).Count -ge 2 } 20)) { $failures += 'segunda instancia nao surgiu' }
# Nota: Esperar roda o scriptblock em escopo proprio — atribuicoes dentro dele
# nao propagam. Busca do processo de B fica fora do Esperar.
if (-not (Esperar { $b = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -Last 1; $b -and $b.Id -ne $aProc.Id -and (TemEditor (JanelaPorPid $b.Id)) } 25)) { $failures += 'editor da instancia B nao apareceu' }
$bProc = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -Last 1
if ($bProc -and $bProc.Id -ne $aProc.Id) { '2. instancia B aberta (PID ' + $bProc.Id + ')' } else { 'FALHA: instancia B ausente'; exit 1 }

$aHwnd = [IntPtr]$aProc.MainWindowHandle
$bHwnd = [IntPtr]$bProc.MainWindowHandle

# ---- 2. mesmo tema nas duas ----
$corA1 = Probe $aHwnd
$corB1 = Probe $bHwnd
$temaInicial = NomeDoTema $corA1
'3. fundos iniciais: A=[' + $corA1.R + ',' + $corA1.G + ',' + $corA1.B + '] (' + $temaInicial + ') B=[' + $corB1.R + ',' + $corB1.G + ',' + $corB1.B + '] (' + (NomeDoTema $corB1) + ')'
if (-not $temaInicial) { $failures += 'fundo inicial de A nao casa com nenhuma paleta' }
if ($corA1 -ne $corB1) { $failures += 'janelas iniciaram com temas diferentes' }

# ---- 3. troca para escuro em B ----
if (-not (ItemDeMenu (JanelaPorPid $bProc.Id) 'Configurar...')) { $failures += 'menu Configurar nao invocado em B' }
if (-not (SelecionarTema $bProc.Id 'DarkDefaultRadio')) { $failures += 'radio DarkDefault nao selecionado em B' }
if (-not (Esperar { (Probe $bHwnd) -eq $paletas['DarkDefault'] } 8)) { $failures += 'fundo de B nao ficou DarkDefault' }
$corB2 = Probe $bHwnd
'4. B apos troca: [' + $corB2.R + ',' + $corB2.G + ',' + $corB2.B + ']'
if ($corB2 -ne $paletas['DarkDefault']) { $failures += 'B nao aplicou DarkDefault' }

# ---- 4. ativa A: deve seguir o tema ----
[W32]::SetForegroundWindow($aHwnd) | Out-Null
Start-Sleep -Seconds 2
$corA2 = Probe $aHwnd
'5. A apos ativacao: [' + $corA2.R + ',' + $corA2.G + ',' + $corA2.B + ']'
if ($corA2 -ne $paletas['DarkDefault']) {
    # fallback: clique no editor de A para garantir ativacao
    [W32]::SetForegroundWindow($aHwnd) | Out-Null
    Start-Sleep -Seconds 2
    $corA2 = Probe $aHwnd
}
if ($corA2 -ne $paletas['DarkDefault']) { $failures += 'A nao sincronizou o tema ao ser ativada' } else { '6. A sincronizou o tema (pixel-exato)' }

# ---- 5. restaura tema inicial em A e encerra ----
if ($temaInicial -and $temaInicial -ne 'DarkDefault') {
    if (ItemDeMenu (JanelaPorPid $aProc.Id) 'Configurar...') {
        SelecionarTema $aProc.Id ($temaInicial + 'Radio') | Out-Null
    }
    if (-not (Esperar { (Probe $aHwnd) -eq $paletas[$temaInicial] } 8)) { 'nota: restauracao do tema inicial nao confirmada (cosmetico)' }
}
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force

'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'TEMA-SYNC PASS'
