# Smoke de NAO-REGRESSAO: a sequencia exata do relato do usuario (18/11).
# Relato: abrir o Slinky (no claro padrao) com arquivo -> trocar para Azulado ->
# trocar para Escuro padrao -> criar aba "Sem titulo" DEPOIS -> o hover na aba e
# no botao "+" ficava com cores erradas (+ branco com hover preto; hover azul).
# Causa raiz: o botao "+" vive no template do TabView FORA de todo tabItem, e o
# truque RequestedTheme (Default -> ElementTheme) aplicado so nos tabItems nunca
# o alcancava — seus {ThemeResource} ficavam presos na paleta do primeiro tema.
# Correcao: o mesmo truque agora e aplicado ao proprio NotesTabView.
# Asserts por pixel: nada de F3F3F3 (paleta clara) nem de azul-accent no strip.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32V2 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$dirScript = $PSScriptRoot
$failures = @()

function Foreground { foreach ($p in (Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })) { [W32V2]::SetForegroundWindow($p.MainWindowHandle) | Out-Null } }
function Chord([byte]$vk) {
    [W32V2]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32V2]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32V2]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)
    [W32V2]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
}
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}
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
        [W32V2]::PostMessage([IntPtr]$script:janelaTema.Current.NativeWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 600
    }
    return $ok
}
function FotoEAvalia([string]$nome, [int]$hoverX) {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $rect = New-Object W32V2+RECT
    [W32V2]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    $fotosDir = Join-Path $dirScript ('../../docs/smoke-fotos/' + (Get-Date -Format 'yyyy-MM-dd'))
    New-Item -ItemType Directory -Path $fotosDir -Force | Out-Null
    $bmp.Save((Join-Path $fotosDir ($nome + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $f3 = 0; $azuis = 0
    for ($y = 75; $y -lt 118; $y++) {
        for ($x = 8; $x -lt 1100; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.R -eq 243 -and $c.G -eq 243 -and $c.B -eq 243) { $f3++ }
            if ($c.B -gt 100 -and $c.B -gt $c.R + 30 -and $c.B -gt $c.G + 10) { $azuis++ }
        }
    }
    $bmp.Dispose()
    Write-Host ('  ' + $nome + ' (hover x=' + $hoverX + '): F3F3F3=' + $f3 + ' azuis=' + $azuis)
    return @{ f3 = $f3; azuis = $azuis }
}

# ---- limpar ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
$rec = Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\Recovery'
Remove-Item (Join-Path $rec '*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\session-*.json') -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# ---- sequencia EXATA do relato ----
Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
Start-Sleep -Seconds 8
Foreground
Start-Sleep -Seconds 1
Write-Host '1. app aberto (claro padrao inicial)'
if (-not (SelecionarTema 'DarkBluishRadio')) { $failures += 'nao foi para Azulado' }
Write-Host '2. tema: azulado'
if (-not (SelecionarTema 'DarkDefaultRadio')) { $failures += 'nao foi para Escuro padrao' }
Write-Host '3. tema: escuro padrao'
Chord 0x54; Start-Sleep -Seconds 1
Foreground
Start-Sleep -Seconds 1
Write-Host '4. aba sem titulo criada depois; hover em cada area:'

$p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$rect = New-Object W32V2+RECT
[W32V2]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
$areas = @(
    @{ n = 'aba-com-arquivo'; x = 100 },
    @{ n = 'aba-semtitulo-nova'; x = 375 },
    @{ n = 'botao-plus'; x = 760 }
)
foreach ($a in $areas) {
    [W32V2]::SetCursorPos($rect.Left + $a.x, $rect.Top + 93) | Out-Null
    [W32V2]::SetCursorPos($rect.Left + $a.x + 2, $rect.Top + 93) | Out-Null
    [W32V2]::SetCursorPos($rect.Left + $a.x, $rect.Top + 93) | Out-Null
    Start-Sleep -Milliseconds 1200
    $r = FotoEAvalia ('sequencia-' + $a.n) $a.x
    if ($r.f3 -gt 0) { $failures += ($a.n + ': fundo claro F3F3F3 (' + $r.f3 + ' px)') }
    if ($r.azuis -gt 0) { $failures += ($a.n + ': ' + $r.azuis + ' pixels azuis no hover') }
}

# voltar ao escuro padrao (tema do usuario) e limpar
SelecionarTema 'DarkDefaultRadio' | Out-Null
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'SEQUENCIA DO RELATO: SEM AZUL E SEM BRANCO PASS'
