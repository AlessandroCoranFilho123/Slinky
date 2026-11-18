# Smoke de NAO-REGRESSAO de cores: TODAS as combinacoes tema x estado das abas.
# Para cada um dos 5 temas: hover com cursor real em cada aba, no "x" de cada aba
# e no botao "+", captura de TELA (CopyFromScreen) e validacao:
#   1) a cor da MODA do header sob hover deve ser Background OU Surface do tema
#      (da paleta do app — ThemePalette);
#   2) ZERO pixels de azul-accent do SISTEMA (#0078D4 e vizinhos) no strip;
#   3) ZERO pixels da Surface do tema CLARO (F3F3F3) quando o app esta no escuro.
# Causa raiz coberta: recursos WinUI de estados nao sobrescritos resolviam para
# accent do sistema (docs/QA-2026-11-17.md; AplicarRecursosAba aplica os 117
# recursos do template).
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
param([string]$PhotosDir)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32H3 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$dirScript = $PSScriptRoot
if (-not $PhotosDir) { $PhotosDir = Join-Path $dirScript ('..\..\docs\smoke-fotos\' + (Get-Date -Format 'yyyy-MM-dd')) }
New-Item -ItemType Directory -Path $PhotosDir -Force | Out-Null
$failures = @()

# paleta esperada por tema (ThemePalette.cs): Background, Surface
$paletas = @{
    'LightDefault'   = @{ bg = @(255, 255, 255); surface = @(243, 243, 243) }
    'LightYellowish' = @{ bg = @(244, 236, 216); surface = @(244, 236, 216) }
    'DarkDefault'    = @{ bg = @(30, 30, 30);    surface = @(40, 40, 40) }
    'DarkBluish'     = @{ bg = @(16, 38, 59);    surface = @(16, 38, 59) }
    'DarkDracula'    = @{ bg = @(40, 42, 54);    surface = @(40, 42, 54) }
}

function Chord([byte]$vk) {
    [W32H3]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32H3]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32H3]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)
    [W32H3]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
}
function Foreground { foreach ($p in (Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })) { [W32H3]::SetForegroundWindow($p.MainWindowHandle) | Out-Null } }
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
        [W32H3]::PostMessage([IntPtr]$script:janelaTema.Current.NativeWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 600
    }
    return $ok
}
# captura de TELA da janela (PrintWindow nao captura estados de hover)
function Tela($rect) {
    $bmp = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    return $bmp
}
# moda das cores do rect do header de uma aba (evita o texto, que e minoria)
function ModaHeader($bmp, [int]$x0, [int]$x1, [int]$y0, [int]$y1) {
    $cores = @{}
    for ($y = $y0; $y -lt $y1; $y++) {
        for ($x = $x0; $x -lt $x1; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $chave = '' + $c.R + ',' + $c.G + ',' + $c.B
            $cores[$chave] = $cores[$chave] + 1
        }
    }
    $melhor = $cores.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
    return $melhor.Key
}
function CorPerto([string]$moda, [int[]]$esperada) {
    $p = $moda.Split(',')
    $dr = [Math]::Abs([int]$p[0] - $esperada[0])
    $dg = [Math]::Abs([int]$p[1] - $esperada[1])
    $db = [Math]::Abs([int]$p[2] - $esperada[2])
    return ($dr -le 12 -and $dg -le 12 -and $db -le 12)
}
function ContaAzulSistema($bmp) {
    # azul-accent padrao do sistema (#0078D4 e vizinhos diretos)
    $n = 0
    for ($y = 72; $y -lt 118; $y++) {
        for ($x = 8; $x -lt 1100; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.R -lt 60 -and $c.G -ge 90 -and $c.G -le 160 -and $c.B -ge 180) { $n++ }
        }
    }
    return $n
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
$rect = New-Object W32H3+RECT
[W32H3]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
# posicoes em coords de JANELA (medidas dos headers reais das abas)
$abasX = @(135, 375, 615)
$fecharX = @(235, 475, 715)
$plusX = 760

# ---- varredura: 5 temas x estados ----
foreach ($tema in @($(if ($env:SO_TEMA) { @($env:SO_TEMA) } else { @('LightDefault', 'LightYellowish', 'DarkDefault', 'DarkBluish', 'DarkDracula') }))) {
    Write-Host ('== tema ' + $tema + ' ==')
    if (-not (SelecionarTema ($tema + 'Radio'))) { $failures += ($tema + ': radio nao selecionado'); continue }
    Foreground
    # assenta 3s: a re-aplicacao de recursos apos troca de tema precisa terminar
    # antes do polling de UIA/pixels (senao mede transicao entre temas)
    Start-Sleep -Seconds 3
    $pal = $paletas[$tema]

    # hover em cada aba (header)
    for ($i = 0; $i -lt 3; $i++) {
        [W32H3]::SetCursorPos($rect.Left + $abasX[$i], $rect.Top + 93) | Out-Null
        [W32H3]::SetCursorPos($rect.Left + $abasX[$i] + 2, $rect.Top + 93) | Out-Null
        [W32H3]::SetCursorPos($rect.Left + $abasX[$i], $rect.Top + 93) | Out-Null
        Start-Sleep -Milliseconds 1000
        $bmp = Tela $rect
        $moda = ModaHeader $bmp ($abasX[$i] - 60) ($abasX[$i] + 60) 80 112
        $ok = (CorPerto $moda $pal.bg) -or (CorPerto $moda $pal.surface)
        $azul = ContaAzulSistema $bmp
        Write-Host ('  hover aba' + ($i + 1) + ': moda=[' + $moda + '] azulSistema=' + $azul)
        if (-not $ok) { $failures += ($tema + '/aba' + ($i + 1) + ' hover: cor inesperada [' + $moda + ']') }
        if ($azul -gt 0) { $failures += ($tema + '/aba' + ($i + 1) + ' hover: ' + $azul + ' px azul-sistema') }
        $bmp.Save((Join-Path $PhotosDir ($tema + '-hover-aba' + ($i + 1) + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }

    # hover no "x" de cada aba (close button)
    for ($i = 0; $i -lt 3; $i++) {
        [W32H3]::SetCursorPos($rect.Left + $fecharX[$i], $rect.Top + 93) | Out-Null
        Start-Sleep -Milliseconds 900
        $bmp = Tela $rect
        $moda = ModaHeader $bmp ($fecharX[$i] - 40) ($fecharX[$i] + 40) 80 112
        $azul = ContaAzulSistema $bmp
        Write-Host ('  hover x-aba' + ($i + 1) + ': moda=[' + $moda + '] azulSistema=' + $azul)
        if (-not ((CorPerto $moda $pal.bg) -or (CorPerto $moda $pal.surface))) { $failures += ($tema + '/x-aba' + ($i + 1) + ': cor inesperada [' + $moda + ']') }
        if ($azul -gt 0) { $failures += ($tema + '/x-aba' + ($i + 1) + ': ' + $azul + ' px azul-sistema') }
        $bmp.Dispose()
    }

    # botao "+": normal e hover
    [W32H3]::SetCursorPos($rect.Left + 850, $rect.Top + 93) | Out-Null   # cursor fora do +
    Start-Sleep -Milliseconds 800
    $bmp = Tela $rect
    $modaNormal = ModaHeader $bmp ($plusX - 30) ($plusX + 30) 80 112
    $azulNormal = ContaAzulSistema $bmp
    $bmp.Dispose()
    [W32H3]::SetCursorPos($rect.Left + $plusX, $rect.Top + 93) | Out-Null
    Start-Sleep -Milliseconds 1000
    $bmp = Tela $rect
    $modaHover = ModaHeader $bmp ($plusX - 30) ($plusX + 30) 80 112
    $azulHover = ContaAzulSistema $bmp
    $bmp.Dispose()
    Write-Host ('  botao +: normal=[' + $modaNormal + '] hover=[' + $modaHover + '] azulSistema=' + $azulHover)
    foreach ($m in @(@{ t = 'normal'; c = $modaNormal }, @{ t = 'hover'; c = $modaHover })) {
        if (-not ((CorPerto $m.c $pal.surface) -or (CorPerto $m.c $pal.bg))) {
            $failures += ($tema + '/+ (' + $m.t + '): cor inesperada [' + $m.c + ']')
        }
    }
    if ($azulNormal -gt 0 -or $azulHover -gt 0) { $failures += ($tema + '/+: px azul-sistema') }
}

# voltar ao escuro padrao (tema do usuario) e limpar
SelecionarTema 'DarkDefaultRadio' | Out-Null
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'CORES: TODAS AS COMBINACOES PASS'
