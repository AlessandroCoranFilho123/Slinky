# Matriz de TRANSICOES de tema: as 25 combinacoes 5x5 (de -> para), com estados medidos
# APOS cada troca (1 s de assentamento — com pinceis estaveis nao ha re-aplicacao de recursos).
# GEOMETRIA VIA UIA: posicoes das abas, do "x" de cada aba e do botao "+" vem dos
# BoundingRectangle reais (as coordenadas fixas antigas eram fantasma — a matriz chegou
# a rodar com 1 aba e os hovers em abas 2/3 e "+" caiam na strip vazia, passando vazio).
# O script GARANTE 3 abas via UIA antes de medir.
# Para cada transicao, com cursor REAL:
#   1) hover na aba NAO selecionada: header = cor de hover da paleta e o "x" mistura
#      com o header (estado PointerOver pinta header E close button com a mesma chave);
#   2) hover no "x" da nao selecionada: cor de hover;
#   3) hover na aba SELECIONADA: o header NAO muda de cor (o estado Selected mantem
#      TabViewItemHeaderBackground; o visual vem do "pill" SelectedBackgroundPath) e o
#      "x" continua misturando com o header;
#   4) hover no "x" da SELECIONADA: cor de hover VISIVEL sobre o header (o sintoma
#      relatado era o oposto: a caixa de hover PERMANECIA sem o mouse);
#   5) PÓS-LEAVE: mouse em area neutra — o "x" volta ao fundo da aba (igual ao header,
#      tolerancia ±5) em ambas as abas; fundo do strip e "+" normais;
#   6) ZERO pixels de azul-accent do SISTEMA e ZERO residuo da paleta do tema ANTERIOR.
# Variaveis de ambiente:
#   SO_TRANSICAO = "Origem-Destino" roda uma unica transicao (spike/investigacao)
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
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex);
    public struct RECT { public int Left, Top, Right, Bottom; }
    public struct POINT { public int X, Y; }
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

# MOVIMENTO EM PASSOS via mouse_event ABSOLUTE: o WinUI 3 consome a pilha de entrada
# WM_POINTER — SetCursorPos move o CURSOR sem gerar evento de entrada (o app nunca
# ve o movimento e os estados de hover nao aplicam); mouse_event injeta eventos
# reais, iguais aos de um mouse fisico. Passo de ~40 ms por segmento.
function MoverPara([int]$x, [int]$y) {
    $ponto = New-Object W32H3+POINT
    [W32H3]::GetCursorPos([ref]$ponto) | Out-Null
    $telaW = [W32H3]::GetSystemMetrics(0)
    $telaH = [W32H3]::GetSystemMetrics(1)
    for ($s = 1; $s -le 10; $s++) {
        $nx = [int]($ponto.X + ($x - $ponto.X) * $s / 10)
        $ny = [int]($ponto.Y + ($y - $ponto.Y) * $s / 10)
        $ax = [int][Math]::Round($nx * 65535.0 / ($telaW - 1))
        $ay = [int][Math]::Round($ny * 65535.0 / ($telaH - 1))
        [W32H3]::mouse_event(0x0001 -bor 0x8000, $ax, $ay, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 40
    }
    $ax = [int][Math]::Round($x * 65535.0 / ($telaW - 1))
    $ay = [int][Math]::Round($y * 65535.0 / ($telaH - 1))
    [W32H3]::mouse_event(0x0001 -bor 0x8000, $ax, $ay, 0, [UIntPtr]::Zero)
}

# Estados de PointerOver nao aplicam de forma confiavel sem a janela ATIVA, e o
# SetForegroundWindow puro perde a disputa do foreground lock do Windows. Garante
# ativacao com clique neutro (strip a direita do "+", ou centro da janela antes das
# abas existirem — nenhum dos dois tem handler no app).
function AtivarJanela {
    Foreground
    Start-Sleep -Milliseconds 200
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $p -or [W32H3]::GetForegroundWindow() -eq $p.MainWindowHandle) { return }
    $g = ColetarGeometria
    if ($g -and $g.Plus) {
        $x = [int]($g.Plus.X + $g.Plus.Width + 80)
        $y = [int]($g.Plus.Y + $g.Plus.Height / 2)
    } else {
        $x = 600
        $y = 400
    }
    MoverPara $x $y
    [W32H3]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [W32H3]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    Foreground
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
# moda das cores de um box (coords do BITMAP: screen - rect.Left/Top)
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
# moda de um box CENTRADO em coords de TELA (meio-largura dx, meio-altura dy)
function ModaBox($bmp, [double]$sx, [double]$sy, [int]$dx, [int]$dy) {
    $cx = [int]$sx - $rect.Left
    $cy = [int]$sy - $rect.Top
    return ModaHeader $bmp ($cx - $dx) ($cx + $dx) ($cy - $dy) ($cy + $dy)
}
function CorPerto([string]$moda, [int[]]$esperada, [int]$tol = 12) {
    $p = $moda.Split(',')
    $dr = [Math]::Abs([int]$p[0] - $esperada[0])
    $dg = [Math]::Abs([int]$p[1] - $esperada[1])
    $db = [Math]::Abs([int]$p[2] - $esperada[2])
    return ($dr -le $tol -and $dg -le $tol -and $db -le $tol)
}
# duas medicoes pertencem a mesma cor (ex.: "x" igual ao header da propria aba)
function MesmaCor([string]$a, [string]$b, [int]$tol) {
    $pa = $a.Split(',')
    $pb = $b.Split(',')
    return ([Math]::Abs([int]$pa[0] - [int]$pb[0]) -le $tol -and [Math]::Abs([int]$pa[1] - [int]$pb[1]) -le $tol -and [Math]::Abs([int]$pa[2] - [int]$pb[2]) -le $tol)
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

# ---- geometria via UIA: abas (rect, rect do "x", selecionada) e botao "+" ----
function ColetarGeometria {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $p) { return $null }
    $r = [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
    $condTab = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)
    $tabsUi = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condTab))
    if ($tabsUi.Count -eq 0) { return $null }
    $condBtn = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Button)
    $botoes = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condBtn))
    $tabs = @()
    $maxDir = 0
    foreach ($t in $tabsUi) {
        $tr = $t.Current.BoundingRectangle
        if ($tr.Width -le 0) { continue }
        $closeRect = $null
        foreach ($b in $botoes) {
            $br = $b.Current.BoundingRectangle
            if ($br.Width -le 0) { continue }
            if ($br.X -ge $tr.X - 1 -and ($br.X + $br.Width) -le ($tr.X + $tr.Width + 1) -and $br.Y -ge $tr.Y - 1 -and ($br.Y + $br.Height) -le ($tr.Y + $tr.Height + 1)) { $closeRect = $br; break }
        }
        $sel = $false
        try { $sel = $t.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected } catch { }
        $tabs += [pscustomobject]@{ Rect = $tr; CloseRect = $closeRect; Selected = $sel }
        $dir = $tr.X + $tr.Width
        if ($dir -gt $maxDir) { $maxDir = $dir }
    }
    # botao "+": Button na MESMA faixa vertical das abas, FORA de todo TabItem,
    # a direita do ultimo (os caption buttons ficam acima; MenuBar e MenuItem)
    $faixaY = $tabs[0].Rect.Y
    $plus = $null
    foreach ($b in $botoes) {
        $br = $b.Current.BoundingRectangle
        if ($br.Width -le 0 -or $br.Height -le 0) { continue }
        if ([Math]::Abs($br.Y - $faixaY) -gt 15) { continue }
        if ($br.X -lt $maxDir - 5) { continue }
        $plus = $br
        break
    }
    return [pscustomobject]@{ Tabs = $tabs; Plus = $plus }
}

function GarantirAbas([int]$n) {
    for ($i = 0; $i -lt 8; $i++) {
        $g = ColetarGeometria
        if ($g -and $g.Tabs.Count -ge $n) { return $true }
        AtivarJanela
        Chord 0x54   # Ctrl+T
        Start-Sleep -Seconds 1
    }
    $g = ColetarGeometria
    return ($g -and $g.Tabs.Count -ge $n)
}

function MedirEstados([string]$de, [string]$para) {
    $pal = $paletas[$para]
    $tag = $de + '-' + $para

    $g = ColetarGeometria
    if (-not $g -or $g.Tabs.Count -lt 2 -or -not $g.Plus) {
        $n = $(if ($g) { $g.Tabs.Count } else { -1 })
        $script:failures += ($tag + ': geometria UIA incompleta (abas=' + $n + ', plus=' + [bool]($g -and $g.Plus) + ')')
        return
    }
    $sel = $g.Tabs | Where-Object { $_.Selected } | Select-Object -First 1
    $unsel = $g.Tabs | Where-Object { -not $_.Selected } | Select-Object -First 1
    if (-not $sel -or -not $unsel -or -not $sel.CloseRect -or -not $unsel.CloseRect) {
        $script:failures += ($tag + ': aba selecionada/nao selecionada sem rect de close button')
        return
    }
    $cxSelX = [int]($sel.CloseRect.X + $sel.CloseRect.Width / 2)
    $cxSelY = [int]($sel.CloseRect.Y + $sel.CloseRect.Height / 2)
    $cxUnX = [int]($unsel.CloseRect.X + $unsel.CloseRect.Width / 2)
    $cxUnY = [int]($unsel.CloseRect.Y + $unsel.CloseRect.Height / 2)
    $cPlusX = [int]($g.Plus.X + $g.Plus.Width / 2)
    $cPlusY = [int]($g.Plus.Y + $g.Plus.Height / 2)
    $neutroX = [int]($g.Plus.X + $g.Plus.Width + 80)
    # probe do header: caixa entre o texto e o "x", dentro do TabContainer
    $hSelX = [int]$sel.CloseRect.X - 16
    $hUnX = [int]$unsel.CloseRect.X - 16

    AtivarJanela

    # ---- (a) hover na aba NAO selecionada: header vira cor de hover e o "x" mistura ----
    MoverPara ([int]($unsel.Rect.X + $unsel.Rect.Width * 0.45)) $cxUnY
    Start-Sleep -Milliseconds 900
    $bmp = Tela $rect
    $mHeader = ModaBox $bmp ($unsel.Rect.X + $unsel.Rect.Width * 0.45) $cxUnY 10 8
    $mX = ModaBox $bmp $cxUnX $cxUnY 10 8
    $azul = ContaAzulSistema $bmp
    Write-Host ('  hover nao-sel: header=[' + $mHeader + '] x=[' + $mX + '] azul=' + $azul)
    if (-not (CorPerto $mHeader $pal.bg 5)) { $script:failures += ($tag + '/header nao-sel hover deveria ser cor de hover: [' + $mHeader + ']') }
    if (-not (MesmaCor $mX $mHeader 5)) { $script:failures += ($tag + '/x nao-sel (tab hover) deveria misturar com o header: x=[' + $mX + '] header=[' + $mHeader + ']') }
    if ($azul -gt 0) { $script:failures += ($tag + '/hover nao-sel: ' + $azul + ' px azul-sistema') }
    $bmp.Dispose()

    # ---- (b) hover no "x" da NAO selecionada: cor de hover ----
    MoverPara $cxUnX $cxUnY
    Start-Sleep -Milliseconds 900
    $bmp = Tela $rect
    $mXHover = ModaBox $bmp $cxUnX $cxUnY 10 8
    $azul = ContaAzulSistema $bmp
    Write-Host ('  hover x nao-sel: [' + $mXHover + '] azul=' + $azul)
    if (-not (CorPerto $mXHover $pal.bg 5)) { $script:failures += ($tag + '/x nao-sel hover deveria ser cor de hover: [' + $mXHover + ']') }
    if ($azul -gt 0) { $script:failures += ($tag + '/x nao-sel hover: ' + $azul + ' px azul-sistema') }
    $bmp.Dispose()

    # ---- (c) hover na aba SELECIONADA: header NAO muda e o "x" continua misturando ----
    MoverPara ([int]($sel.Rect.X + $sel.Rect.Width * 0.45)) $cxSelY
    Start-Sleep -Milliseconds 900
    $bmp = Tela $rect
    $mHeaderSel = ModaBox $bmp ($sel.Rect.X + $sel.Rect.Width * 0.45) $cxSelY 10 8
    $mXSel = ModaBox $bmp $cxSelX $cxSelY 10 8
    $azul = ContaAzulSistema $bmp
    Write-Host ('  hover sel: header=[' + $mHeaderSel + '] x=[' + $mXSel + '] azul=' + $azul)
    if (-not ((CorPerto $mHeaderSel $pal.bg) -or (CorPerto $mHeaderSel $pal.surface))) { $script:failures += ($tag + '/header sel hover: [' + $mHeaderSel + ']') }
    if (-not (MesmaCor $mXSel $mHeaderSel 5)) { $script:failures += ($tag + '/x sel (tab hover) deveria misturar com o header: x=[' + $mXSel + '] header=[' + $mHeaderSel + ']') }
    if ($azul -gt 0) { $script:failures += ($tag + '/hover sel: ' + $azul + ' px azul-sistema') }
    $bmp.Dispose()

    # ---- (d) hover no "x" da SELECIONADA: cor de hover VISIVEL sobre o header ----
    MoverPara $cxSelX $cxSelY
    Start-Sleep -Milliseconds 900
    $bmp = Tela $rect
    $mXSelHover = ModaBox $bmp $cxSelX $cxSelY 10 8
    $azul = ContaAzulSistema $bmp
    Write-Host ('  hover x sel: [' + $mXSelHover + '] azul=' + $azul)
    if (-not (CorPerto $mXSelHover $pal.bg 5)) { $script:failures += ($tag + '/x sel hover deveria ser cor de hover: [' + $mXSelHover + ']') }
    if ($azul -gt 0) { $script:failures += ($tag + '/x sel hover: ' + $azul + ' px azul-sistema') }
    $bmp.Save((Join-Path $PhotosDir ($tag + '-hoverX.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    # ---- (e) PÓS-LEAVE: o "x" volta ao fundo da aba (o sintoma relatado era ficar preso) ----
    MoverPara $neutroX $cxSelY
    Start-Sleep -Milliseconds 1000
    $bmp = Tela $rect
    $mHeaderSel2 = ModaBox $bmp $hSelX $cxSelY 6 6
    $mXSelPost = ModaBox $bmp $cxSelX $cxSelY 10 8
    $mHeaderUn2 = ModaBox $bmp $hUnX $cxUnY 6 6
    $mXUnPost = ModaBox $bmp $cxUnX $cxUnY 10 8
    $mStrip = ModaBox $bmp ($neutroX + 40) $cxSelY 40 8
    $mPlus = ModaBox $bmp $cPlusX $cPlusY 10 8
    $azul = ContaAzulSistema $bmp
    $residuo = if ($de -ne $para) { ContaResiduo $bmp $paletas[$de] $pal 8 1100 72 118 } else { 0 }
    Write-Host ('  pos-leave: headerSel=[' + $mHeaderSel2 + '] xSel=[' + $mXSelPost + '] headerUn=[' + $mHeaderUn2 + '] xUn=[' + $mXUnPost + '] strip=[' + $mStrip + '] +=[' + $mPlus + '] azul=' + $azul + ' residuo=' + $residuo)
    if (-not (MesmaCor $mXSelPost $mHeaderSel2 5)) { $script:failures += ($tag + '/x sel pos-leave deveria voltar ao fundo da aba: x=[' + $mXSelPost + '] header=[' + $mHeaderSel2 + ']') }
    if (-not (MesmaCor $mXUnPost $mHeaderUn2 5)) { $script:failures += ($tag + '/x nao-sel pos-leave deveria voltar ao fundo da aba: x=[' + $mXUnPost + '] header=[' + $mHeaderUn2 + ']') }
    foreach ($m in @(@{ n = 'header sel'; c = $mHeaderSel2 }, @{ n = 'header nao-sel'; c = $mHeaderUn2 }, @{ n = 'strip'; c = $mStrip }, @{ n = '+ normal'; c = $mPlus })) {
        if (-not ((CorPerto $m.c $pal.bg) -or (CorPerto $m.c $pal.surface))) { $script:failures += ($tag + '/' + $m.n + ' pos-leave: [' + $m.c + ']') }
    }
    if ($azul -gt 0) { $script:failures += ($tag + '/pos-leave: ' + $azul + ' px azul-sistema') }
    if ($residuo -gt 300) { $script:failures += ($tag + '/pos-leave: ' + $residuo + ' px residuais do tema anterior ' + $de) }
    $bmp.Save((Join-Path $PhotosDir ($tag + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    # ---- (f) hover no "+" ----
    MoverPara $cPlusX $cPlusY
    Start-Sleep -Milliseconds 900
    $bmp = Tela $rect
    $mPlusHover = ModaBox $bmp $cPlusX $cPlusY 10 8
    $azul = ContaAzulSistema $bmp
    Write-Host ('  + hover: [' + $mPlusHover + '] azul=' + $azul)
    if (-not ((CorPerto $mPlusHover $pal.bg) -or (CorPerto $mPlusHover $pal.surface))) { $script:failures += ($tag + '/+ hover: [' + $mPlusHover + ']') }
    if ($azul -gt 0) { $script:failures += ($tag + '/+ hover: ' + $azul + ' px azul-sistema') }
    $bmp.Dispose()
}

# ---- limpar, abrir, garantir 3 abas ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
$rec = Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\Recovery'
Remove-Item (Join-Path $rec '*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState\session-*.json') -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
Start-Sleep -Seconds 8
if (-not (GarantirAbas 3)) {
    Write-Host 'FALHA: nao consegui garantir 3 abas via UIA (Ctrl+T)'
    Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
    exit 1
}
$p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$rect = New-Object W32H3+RECT
[W32H3]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null

# ---- matriz 5x5: para cada origem, medir todos os destinos ----
foreach ($de in $temas) {
    if (-not (SelecionarTema ($de + 'Radio'))) { $script:failures += ($de + ': radio nao selecionado (origem)'); continue }
    Foreground
    Start-Sleep -Seconds 1
    foreach ($para in $temas) {
        if ($soTema -and $para -ne $soTema) { continue }
        if ($soTransicao -and ($de + '-' + $para) -ne $soTransicao) { continue }
        Write-Host ('== ' + $de + ' -> ' + $para + ' ==')
        if (-not (SelecionarTema ($para + 'Radio'))) { $script:failures += ($de + '->' + $para + ': radio nao selecionado'); continue }
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
