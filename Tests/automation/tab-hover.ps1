# Matriz de TRANSICOES de tema: as 25 combinacoes 5x5 (de -> para), com estados medidos
# APOS cada troca (1 s de assentamento — com pinceis estaveis nao ha re-aplicacao de recursos).
# Para cada transicao, com cursor REAL:
#   1) hover em cada uma das 3 abas (a 1a selecionada, as demais nao);
#   2) hover no "x" da aba 3 e, depois, PÓS-LEAVE (mouse em area neutra): o sintoma
#      relatado era o hover do "x" ficar ativo mesmo sem o mouse em cima;
#   3) fundo do strip onde as abas ficam (x 8-110, fora da primeira aba);
#   4) botao "+" normal e hover;
#   5) residuo: ZERO pixels da paleta do tema ANTERIOR no strip (pincel/recurso velho
#      preso e a assinatura de todas as regressoes de tema ja vistas neste app);
#   6) ZERO pixels de azul-accent do SISTEMA (#0078D4 e vizinhos) no strip.
# Validacao por MODA das cores da regiao (tolerancia +-12) contra ThemePalette.
# Variaveis de ambiente:
#   SO_TRANSICAO = "Origem->Destino" roda uma unica transicao (spike/investigacao)
#   SO_TEMA      = restringe o TEMA DE DESTINO (todas as origens)
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
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
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
    'LightYellowish' = @{ bg = @(244, 236, 216); surface = @(234, 223, 195) }
    'DarkDefault'    = @{ bg = @(30, 30, 30);    surface = @(40, 40, 40) }
    'DarkBluish'     = @{ bg = @(16, 38, 59);    surface = @(22, 48, 72) }
    'DarkDracula'    = @{ bg = @(40, 42, 54);    surface = @(68, 71, 90) }
}
$temas = @('LightDefault', 'LightYellowish', 'DarkDefault', 'DarkBluish', 'DarkDracula')
$soTransicao = $env:SO_TRANSICAO
$soTema = $env:SO_TEMA

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
function CorPertoRGB($c, [int[]]$alvo) {
    return ([Math]::Abs($c.R - $alvo[0]) -le 10 -and [Math]::Abs($c.G - $alvo[1]) -le 10 -and [Math]::Abs($c.B - $alvo[2]) -le 10)
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
function ContaResiduo($bmp, $prev, $cur, [int]$x0, [int]$x1, [int]$y0, [int]$y1) {
    # pixels da paleta (bg/surface) do tema ANTERIOR que nao pertencem a paleta do
    # ATUAL — sobras de pincel/recurso velho materializado apos a troca
    $n = 0
    for ($y = $y0; $y -lt $y1; $y++) {
        for ($x = $x0; $x -lt $x1; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if (-not ((CorPertoRGB $c $prev.bg) -or (CorPertoRGB $c $prev.surface))) { continue }
            if ((CorPertoRGB $c $cur.bg) -or (CorPertoRGB $c $cur.surface)) { continue }
            $n++
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
$neutroX = 850

function MedirEstados([string]$de, [string]$para) {
    $pal = $paletas[$para]
    $tag = $de + '-' + $para

    # hover em cada uma das 3 abas (aba 1 permanece selecionada; 2 e 3 nao)
    for ($i = 0; $i -lt 3; $i++) {
        [W32H3]::SetCursorPos($rect.Left + $abasX[$i], $rect.Top + 93) | Out-Null
        Start-Sleep -Milliseconds 900
        $bmp = Tela $rect
        $moda = ModaHeader $bmp ($abasX[$i] - 60) ($abasX[$i] + 60) 80 112
        $azul = ContaAzulSistema $bmp
        Write-Host ('  hover aba' + ($i + 1) + ': [' + $moda + '] azul=' + $azul)
        if (-not ((CorPerto $moda $pal.bg) -or (CorPerto $moda $pal.surface))) { $failures += ($tag + '/aba' + ($i + 1) + ' hover: [' + $moda + ']') }
        if ($azul -gt 0) { $failures += ($tag + '/aba' + ($i + 1) + ' hover: ' + $azul + ' px azul-sistema') }
        $bmp.Dispose()
    }

    # hover no "x" da aba 3 (close button)
    [W32H3]::SetCursorPos($rect.Left + $fecharX[2], $rect.Top + 93) | Out-Null
    Start-Sleep -Milliseconds 900
    $bmp = Tela $rect
    $modaX = ModaHeader $bmp ($fecharX[2] - 40) ($fecharX[2] + 40) 80 112
    $azulX = ContaAzulSistema $bmp
    Write-Host ('  hover x3: [' + $modaX + '] azul=' + $azulX)
    if (-not ((CorPerto $modaX $pal.bg) -or (CorPerto $modaX $pal.surface))) { $failures += ($tag + '/x3 hover: [' + $modaX + ']') }
    if ($azulX -gt 0) { $failures += ($tag + '/x3 hover: ' + $azulX + ' px azul-sistema') }
    $bmp.Save((Join-Path $PhotosDir ($tag + '-hoverX.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    # PÓS-LEAVE: mouse em area neutra; o "x" deve voltar ao estado normal — o sintoma
    # relatado era o hover do "x" permanecer ativo sem o mouse em cima. Uma unica
    # captura serve strip, aba selecionada, x3 pos-leave e "+" normal.
    [W32H3]::SetCursorPos($rect.Left + $neutroX, $rect.Top + 93) | Out-Null
    Start-Sleep -Milliseconds 1000
    $bmp = Tela $rect
    $modaStrip = ModaHeader $bmp 8 110 80 112
    $modaAba1 = ModaHeader $bmp ($abasX[0] - 60) ($abasX[0] + 60) 80 112
    $modaX3 = ModaHeader $bmp ($fecharX[2] - 40) ($fecharX[2] + 40) 80 112
    $modaPlus = ModaHeader $bmp ($plusX - 30) ($plusX + 30) 80 112
    $azul = ContaAzulSistema $bmp
    $residuo = if ($de -ne $para) { ContaResiduo $bmp $paletas[$de] $pal 8 1100 72 118 } else { 0 }
    Write-Host ('  pos-leave: strip=[' + $modaStrip + '] aba1=[' + $modaAba1 + '] x3=[' + $modaX3 + '] +=[' + $modaPlus + '] azul=' + $azul + ' residuo=' + $residuo)
    foreach ($m in @(@{ n = 'strip'; c = $modaStrip }, @{ n = 'aba1 normal'; c = $modaAba1 }, @{ n = 'x3 pos-leave'; c = $modaX3 }, @{ n = '+ normal'; c = $modaPlus })) {
        if (-not ((CorPerto $m.c $pal.bg) -or (CorPerto $m.c $pal.surface))) { $failures += ($tag + '/' + $m.n + ': [' + $m.c + ']') }
    }
    if ($azul -gt 0) { $failures += ($tag + '/pos-leave: ' + $azul + ' px azul-sistema') }
    if ($residuo -gt 300) { $failures += ($tag + '/pos-leave: ' + $residuo + ' px residuais do tema anterior ' + $de) }
    $bmp.Save((Join-Path $PhotosDir ($tag + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    # hover no "+"
    [W32H3]::SetCursorPos($rect.Left + $plusX, $rect.Top + 93) | Out-Null
    Start-Sleep -Milliseconds 900
    $bmp = Tela $rect
    $modaPlusHover = ModaHeader $bmp ($plusX - 30) ($plusX + 30) 80 112
    $azul = ContaAzulSistema $bmp
    Write-Host ('  + hover: [' + $modaPlusHover + '] azul=' + $azul)
    if (-not ((CorPerto $modaPlusHover $pal.bg) -or (CorPerto $modaPlusHover $pal.surface))) { $failures += ($tag + '/+ hover: [' + $modaPlusHover + ']') }
    if ($azul -gt 0) { $failures += ($tag + '/+ hover: ' + $azul + ' px azul-sistema') }
    $bmp.Dispose()
}

# ---- matriz 5x5: para cada origem, medir todos os destinos ----
foreach ($de in $temas) {
    if (-not (SelecionarTema ($de + 'Radio'))) { $failures += ($de + ': radio nao selecionado (origem)'); continue }
    Foreground
    Start-Sleep -Seconds 1
    foreach ($para in $temas) {
        if ($soTema -and $para -ne $soTema) { continue }
        if ($soTransicao -and ($de + '->' + $para) -ne $soTransicao) { continue }
        Write-Host ('== ' + $de + ' -> ' + $para + ' ==')
        if (-not (SelecionarTema ($para + 'Radio'))) { $failures += ($de + '->' + $para + ': radio nao selecionado'); continue }
        Foreground
        # assentamento fixo curto: pinceis estaveis nao exigem o reprocesso dos 3 s antigos
        Start-Sleep -Seconds 1
        MedirEstados $de $para
    }
}

# voltar ao escuro padrao (tema do usuario) e limpar
SelecionarTema 'DarkDefaultRadio' | Out-Null
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'MATRIZ DE TRANSICOES: TODAS PASS'
