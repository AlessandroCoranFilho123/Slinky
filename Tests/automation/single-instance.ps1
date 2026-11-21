# Smoke: single-instancing — arquivos abertos pelo shell concentram em abas na
# MESMA janela; "Nova janela" continua criando janelas de propósito.
# Cenarios:
#   1) abrir A via ProgId (FUA)          -> 1 processo, aba de A presente
#   2) abrir B via ProgId (FUA)          -> AINDA 1 processo (redirect), aba de B na mesma janela
#   3) menu Arquivo -> Nova janela       -> 2 processos (multi-janela intencional preservado)
# Foto final da janela principal em docs/smoke-fotos/<data>/.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
param([string]$PhotosDir)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32N {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ShellExecuteInfo2
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
public class ShellExec3
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ShellExecuteEx(ref ShellExecuteInfo2 info);
    public static void OpenWithProgId(string progId, string filePath)
    {
        ShellExecuteInfo2 info = new ShellExecuteInfo2();
        info.cbSize = Marshal.SizeOf(typeof(ShellExecuteInfo2));
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
$failures = @()

function Procs { @(Get-Process Slinky -ErrorAction SilentlyContinue).Count }
function JanelaPrincipal {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
    if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
    [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function NomesAbas {
    $r = JanelaPrincipal
    if (-not $r) { return @() }
    $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)
    @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $cond)) | ForEach-Object { $_.Current.Name }
}
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}
function AbrirViaProgId($arquivo) {
    $progids = (Get-ItemProperty 'HKCU:\SOFTWARE\Classes\.txt\OpenWithProgids' -ErrorAction Stop).PSObject.Properties.Name | Where-Object { $_ -like 'AppX*' }
    $progid = $progids | Select-Object -First 1
    if (-not $progid) { throw 'ProgId AppX do .txt nao encontrado' }
    [ShellExec3]::OpenWithProgId($progid, $arquivo)
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
function Foto($nome) {
    Add-Type -AssemblyName System.Drawing
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
    if (-not $p -or $p.MainWindowHandle -eq 0) { return }
    $rect = New-Object W32N+RECT
    [W32N]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [W32N]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $caminho = Join-Path $PhotosDir ($nome + '.png')
    $bmp.Save($caminho, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ('foto: ' + $caminho)
}

# ---- limpeza e arquivos de teste ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
$ls = Join-Path $env:LOCALAPPDATA "Packages\$family\LocalState"
Remove-Item (Join-Path $ls 'Recovery\*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ls 'session-*.json') -Force -Recurse -ErrorAction SilentlyContinue
Start-Sleep -Seconds 10
$a = Join-Path $env:TEMP 'slinky-si-a.txt'
$b = Join-Path $env:TEMP 'slinky-si-b.txt'
Set-Content -Path $a -Value 'conteudo do arquivo a' -Encoding UTF8
Set-Content -Path $b -Value 'conteudo do arquivo b' -Encoding UTF8

# ---- 1. abrir A (o FUA LANCA o app com o arquivo: estado limpo, sem aba vazia) ----
AbrirViaProgId $a
if (-not (Esperar { (Procs) -ge 1 -and @((NomesAbas) | Where-Object { $_ -match 'si-a\.txt' }).Count -ge 1 } 30)) { $failures += 'arquivo A nao abriu como aba' }
'1. apos abrir A: processos=' + (Procs) + ' abas=[' + ((NomesAbas) -join ' | ') + ']'
if ((Procs) -ne 1) { $failures += 'apos abrir A: processos=' + (Procs) + ' (esperado 1)' }
$abasA = @(NomesAbas)
if ($abasA.Count -ne 1) { $failures += ('apos abrir A do startup: ' + $abasA.Count + ' abas (esperado 1 — sem aba vazia): [' + ($abasA -join ' | ') + ']') }
elseif ($abasA[0] -notmatch 'si-a\.txt') { $failures += ('apos abrir A do startup: a unica aba nao e o arquivo [' + $abasA[0] + ']') }

# ---- 2. abrir B: deve virar ABA na MESMA janela ----
AbrirViaProgId $b
$okB = Esperar { (Procs) -eq 1 -and @((NomesAbas) | Where-Object { $_ -match 'si-b\.txt' }).Count -ge 1 } 25
'2. apos abrir B: processos=' + (Procs) + ' abas=[' + ((NomesAbas) -join ' | ') + ']'
if (-not $okB) { $failures += 'arquivo B nao virou aba na mesma janela (processos=' + (Procs) + ', abas=[' + ((NomesAbas) -join ' | ') + '])' }
$abasB = @(NomesAbas)
if ($abasB.Count -ne 2) { $failures += ('apos abrir B com app rodando: ' + $abasB.Count + ' abas (esperado 2): [' + ($abasB -join ' | ') + ']') }

# ---- 3. Nova janela: multi-janela intencional preservado ----
if (MenuArquivoItem (JanelaPrincipal) 'Nova janela') {
    if (-not (Esperar { (Procs) -ge 2 } 25)) { $failures += 'Nova janela nao criou segundo processo' }
} else { $failures += 'menu Nova janela nao invocado' }
'3. apos Nova janela: processos=' + (Procs)
if ((Procs) -lt 2) { $failures += 'multi-janela quebrou: processos=' + (Procs) }

Foto 'single-instancia-abas'

Remove-Item $a, $b -Force -ErrorAction SilentlyContinue
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
'fotos em: ' + $PhotosDir
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'SINGLE-INSTANCE PASS'
