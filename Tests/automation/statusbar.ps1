# Smoke visual da barra de status do editor: cenários reais com assert por UIA
# e FOTO de cada situação (docs/smoke-fotos/<data>/).
# Cenários: 1 novo doc, 2 digitacao/Ln-Col/contagem, 3 arquivo LF, 4 arquivo
# Windows-1252, 5 arquivo >2 MB (sem Ln/Col), 6 tema escuro.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario
# (arquivos criados em %TEMP% e descartados).
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
param([string]$PhotosDir)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32S {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ShellExecuteInfo
{
    public int cbSize;
    public uint fMask;
    public IntPtr hwnd;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
    public int nShow;
    public IntPtr hInstApp;
    public IntPtr lpIDList;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
    public IntPtr hkeyClass;
    public uint dwHotKey;
    public IntPtr hIcon;
    public IntPtr hProcess;
}
public class ShellExec2
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ShellExecuteEx(ref ShellExecuteInfo info);
    public static void OpenWithProgId(string progId, string filePath)
    {
        ShellExecuteInfo info = new ShellExecuteInfo();
        info.cbSize = Marshal.SizeOf(typeof(ShellExecuteInfo));
        info.fMask = 0x00000001; // SEE_MASK_CLASSNAME
        info.lpVerb = "open";
        info.lpFile = filePath;
        info.lpClass = progId;
        info.nShow = 1;
        if (!ShellExecuteEx(ref info))
        {
            throw new Exception("ShellExecuteEx falhou, codigo " + Marshal.GetLastWin32Error());
        }
    }
}
'@

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $PhotosDir) { $PhotosDir = Join-Path $repo ('docs\smoke-fotos\' + (Get-Date -Format 'yyyy-MM-dd')) }
New-Item -ItemType Directory -Path $PhotosDir -Force | Out-Null

$family = '46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj'
$aumid = "$family!App"
$failures = @()

function Janela {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
    if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
    [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}
function TemEditor { $r = Janela; $r -and @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))).Count -gt 0 }
function Lancar([string]$arquivo) {
    Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 10
    if ($arquivo) {
        # Rota FUA ("Abrir com"/duplo clique): exe direto em WindowsApps nao abre
        # por ACL; o ProgId AppX do .txt ativa o pacote e entrega o arquivo via
        # FileActivatedEventArgs.
        $progids = (Get-ItemProperty 'HKCU:\SOFTWARE\Classes\.txt\OpenWithProgids' -ErrorAction Stop).PSObject.Properties.Name | Where-Object { $_ -like 'AppX*' }
        $progid = $progids | Select-Object -First 1
        if (-not $progid) { throw 'ProgId AppX do .txt nao encontrado' }
        [ShellExec2]::OpenWithProgId($progid, $arquivo)
    } else {
        Start-Process "shell:AppsFolder\$aumid"
    }
    if (-not (Esperar { TemEditor } 40)) { return $false }
    [W32S]::SetForegroundWindow((Get-Process Slinky | Select-Object -First 1).MainWindowHandle) | Out-Null
    Start-Sleep -Seconds 2
    return $true
}
# Texto da barra de status: TextBlock cujo nome contem 'caracteres'.
function StatusTexto {
    $r = Janela
    if (-not $r) { return $null }
    $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Text)
    $texts = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $cond))
    $alvo = $texts | Where-Object { $_.Current.Name -match 'caracteres' } | Select-Object -First 1
    if ($alvo) { return $alvo.Current.Name } else { return $null }
}
function Foto([string]$nome) {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p -or $p.MainWindowHandle -eq 0) { return }
    $rect = New-Object W32S+RECT
    [W32S]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [W32S]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $caminho = Join-Path $PhotosDir ($nome + '.png')
    $bmp.Save($caminho, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ('foto: ' + $caminho)
}
function MenuArquivoItem($raiz, [string]$itemTexto) {
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
function SelecionarRadioTema([string]$radioId) {
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
        [W32S]::PostMessage([IntPtr]$script:janelaTema.Current.NativeWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 600
    }
    return $ok
}

$ws = New-Object -ComObject WScript.Shell

# ---- 1. novo documento ----
if (-not (Lancar $null)) { 'FALHA: app nao abriu'; exit 1 }
Esperar { $null -ne (StatusTexto) } 10 | Out-Null
$s1 = StatusTexto
'1. novo doc: [' + $s1 + ']'
if ($s1 -notmatch 'Ln 1, Col 1' -or $s1 -notmatch '0 caracteres') { $failures += 'status inicial inesperado: [' + $s1 + ']' }
Foto '01-novo-documento'

# ---- 2. digitacao: Ln/Col e contagem ----
$r2 = Janela
$condEdit2 = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)
$editor2 = @($r2.FindAll([Windows.Automation.TreeScope]::Descendants, $condEdit2))[$(@($r2.FindAll([Windows.Automation.TreeScope]::Descendants, $condEdit2)).Count - 1)]
$editor2.SetFocus() | Out-Null
Start-Sleep -Milliseconds 400
$ws.SendKeys('ola mundo')
Start-Sleep -Milliseconds 300
$ws.SendKeys('{ENTER}')
Start-Sleep -Milliseconds 300
# reenvio: SendKeys por sleep e intermitente (gotcha de automacao registrado)
for ($t = 0; $t -lt 4; $t++) {
    $ws.SendKeys('abc')
    if (Esperar { $s = StatusTexto; $s -and $s -match '13 caracteres' } 4) { break }
}
Esperar { $s = StatusTexto; $s -and $s -match 'Ln 2' } 8 | Out-Null
$s2 = StatusTexto
'2. apos digitacao: [' + $s2 + ']'
if ($s2 -notmatch 'Ln 2, Col 4') { $failures += 'Ln/Col inesperado apos digitacao: [' + $s2 + ']' }
# 13 = 'ola mundo' (9) + Enter (o TextBox WinUI insere \r, nao \r\n) + 'abc' (3)
if ($s2 -notmatch '13 caracteres') { $failures += 'contagem inesperada: [' + $s2 + '] (esperado 13: 9 + CR + 3)' }
Foto '02-apos-digitacao'

# ---- 3. arquivo com LF ----
$lf = Join-Path $env:TEMP 'slinky-status-lf.txt'
[IO.File]::WriteAllText($lf, "linha um`nlinha dois`nlinha tres`n")
if (-not (Lancar $lf)) { $failures += 'arquivo LF nao abriu' } else {
    Esperar { $s = StatusTexto; $s -and $s -match ' LF ' } 15 | Out-Null
    $s3 = StatusTexto
    '3. arquivo LF: [' + $s3 + ']'
    if ($s3 -notmatch ' LF ') { $failures += 'status nao mostra LF: [' + $s3 + ']' }
    Foto '03-arquivo-lf'
}

# ---- 4. arquivo Windows-1252 ----
$cp = Join-Path $env:TEMP 'slinky-status-1252.txt'
$enc = [System.Text.Encoding]::GetEncoding(1252)
$conteudo = 'a' + [char]0xE7 + [char]0xE3 + 'o caf' + [char]0xE9 + "`r`nsegunda linha" + [char]0xC0 + "`r`n"
[IO.File]::WriteAllText($cp, $conteudo, $enc)
if (-not (Lancar $cp)) { $failures += 'arquivo 1252 nao abriu' } else {
    Esperar { $s = StatusTexto; $s -and $s -match 'Windows-1252' } 15 | Out-Null
    $s4 = StatusTexto
    '4. arquivo 1252: [' + $s4 + ']'
    if ($s4 -notmatch 'Windows-1252') { $failures += 'status nao mostra Windows-1252: [' + $s4 + ']' }
    Foto '04-arquivo-1252'
}

# ---- 5. arquivo >2 MB: sem Ln/Col, com contagem ----
$big = Join-Path $env:TEMP 'slinky-status-big.txt'
$linha = 'linha ' + ('1234567890' * 4) + "`r`n"
$sb = New-Object System.Text.StringBuilder
for ($i = 0; $i -lt 95000; $i++) { [void]$sb.Append($linha) }
[IO.File]::WriteAllText($big, $sb.ToString())
'5. abrindo arquivo de ~2 MB...'
if (-not (Lancar $big)) { $failures += 'arquivo grande nao abriu' } else {
    Esperar { $s = StatusTexto; $s -and $s -match 'caracteres' } 30 | Out-Null
    $s5 = StatusTexto
    '5. arquivo grande: [' + $s5 + ']'
    if ($s5 -match 'Ln ') { $failures += 'status de arquivo grande mostra Ln/Col: [' + $s5 + ']' }
    if ($s5 -notmatch 'caracteres') { $failures += 'status de arquivo grande sem contagem: [' + $s5 + ']' }
    Foto '05-arquivo-grande-2mb'
}

# ---- 6. tema escuro (legibilidade da barra) ----
if (MenuArquivoItem (Janela) 'Configurar...') {
    if (SelecionarRadioTema 'DarkDefaultRadio') {
        Start-Sleep -Milliseconds 800
        Foto '06-tema-escuro'
        $s6 = StatusTexto
        '6. tema escuro: [' + $s6 + ']'
        if (-not (MenuArquivoItem (Janela) 'Configurar...')) { 'nota: nao reabriu Configurar' }
        SelecionarRadioTema 'LightDefaultRadio' | Out-Null
    } else { $failures += 'radio DarkDefault nao selecionado' }
} else { $failures += 'menu Configurar nao invocado' }

# ---- limpeza ----
Remove-Item $lf, $cp, $big -Force -ErrorAction SilentlyContinue
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
'fotos em: ' + $PhotosDir
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'STATUSBAR SMOKE PASS'
