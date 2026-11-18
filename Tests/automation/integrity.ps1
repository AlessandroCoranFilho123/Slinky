# Smoke de INTEGRIDADE de dados da barra/abertura por arquivo (byte a byte).
# Para cada codificacao (UTF-8 CRLF, UTF-8 LF, Windows-1252):
#   hash original -> abrir pelo shell (rota FUA, com e sem redirect) -> conferir
#   o TEXTO exibido no editor via UIA -> Ctrl+S -> hash apos salvar.
# Hash identico = abertura e salvamento byte a byte preservados.
# Alem disso: redirect com fila (abrir arquivo com principal viva) e
# multi-arquivos por argv (exe direto com 2 caminhos).
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32I {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ShellExecuteInfo3
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
public class ShellExec4
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ShellExecuteEx(ref ShellExecuteInfo3 info);
    public static void OpenWithProgId(string progId, string filePath)
    {
        ShellExecuteInfo3 info = new ShellExecuteInfo3();
        info.cbSize = Marshal.SizeOf(typeof(ShellExecuteInfo3));
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

$family = '46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj'
$failures = @()

function Procs { @(Get-Process Slinky -ErrorAction SilentlyContinue).Count }
function Matar { Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Seconds 10 }
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}
function Janela {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
    if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
    [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function TemEditor { $r = Janela; $r -and @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))).Count -gt 0 }
function EditorTexto {
    $r = Janela
    if (-not $r) { return $null }
    $edits = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit))))
    $editor = $edits[$edits.Count - 1]
    try { return $editor.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { return $null }
}
function AbaExiste($nomeParcial) {
    $r = Janela
    if (-not $r) { return $false }
    $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)
    $tabs = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $cond))
    return (@($tabs | Where-Object { $_.Current.Name -match $nomeParcial }).Count -ge 1)
}
function AbrirViaProgId($arquivo) {
    $progids = (Get-ItemProperty 'HKCU:\SOFTWARE\Classes\.txt\OpenWithProgids' -ErrorAction Stop).PSObject.Properties.Name | Where-Object { $_ -like 'AppX*' }
    $progid = $progids | Select-Object -First 1
    if (-not $progid) { throw 'ProgId AppX do .txt nao encontrado' }
    [ShellExec4]::OpenWithProgId($progid, $arquivo)
}
function HashDe($arquivo) { (Get-FileHash -Path $arquivo -Algorithm SHA256).Hash }
function Normalizar([string]$s) { if ($null -eq $s) { return '' }; return ($s -replace "`r`n", "`n" -replace "`r", "`n") }
function Salvar {
    [W32I]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32I]::keybd_event(0x53, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32I]::keybd_event(0x53, 0, 2, [UIntPtr]::Zero)
    [W32I]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Seconds 3
}

# conteúdo rico em UTF-8 (sem BOM), construído por [char] (script ASCII)
$utf8SemBom = New-Object System.Text.UTF8Encoding($false)
$c1 = 'A' + [char]0xE7 + [char]0xE3 + 'o com acentos, linhas longas e ' + ('x' * 200) + "`r`n" +
      'segunda linha: s' + [char]0xED + 'mbolos !@#$% ' + [char]0x2014 + ' travess' + [char]0xE3 + "o`r`n" +
      'terceira linha' + "`r`n"
$f1 = Join-Path $env:TEMP 'slinky-int-utf8.txt'
[IO.File]::WriteAllText($f1, $c1, $utf8SemBom)

# LF (sem CR), UTF-8
$c2 = 'primeira com lf' + "`n" + 'segunda com lf' + "`n" + 'terceira com lf' + "`n"
$f2 = Join-Path $env:TEMP 'slinky-int-lf.txt'
[IO.File]::WriteAllText($f2, $c2, $utf8SemBom)

# Windows-1252
$cp = [System.Text.Encoding]::GetEncoding(1252)
$c3 = 'a' + [char]0xE7 + [char]0xE3 + 'o caf' + [char]0xE9 + "`r`n" + 'linha com ' + [char]0xC0 + [char]0xC7 + [char]0xD5 + "`r`n"
$f3 = Join-Path $env:TEMP 'slinky-int-1252.txt'
[IO.File]::WriteAllText($f3, $c3, $cp)

# ---- S1: UTF-8 CRLF aberto pela principal (sem redirect) ----
Matar
$hash1a = HashDe $f1
AbrirViaProgId $f1
if (-not (Esperar { TemEditor -and (AbaExiste 'slinky-int-utf8') } 30)) { $failures += 'S1: arquivo UTF-8 nao abriu' }
[W32I]::SetForegroundWindow((Get-Process Slinky | Select-Object -First 1).MainWindowHandle) | Out-Null
Start-Sleep -Seconds 2
$exibido = Normalizar (EditorTexto)
if ($exibido -ne (Normalizar $c1)) {
    $failures += 'S1: texto exibido difere do arquivo (len exibido=' + $exibido.Length + ', esperado=' + (Normalizar $c1).Length + ')'
}
Salvar
$hash1b = HashDe $f1
'1. UTF-8 CRLF: hash antes=' + $hash1a.Substring(0, 12) + ' depois=' + $hash1b.Substring(0, 12)
if ($hash1a -ne $hash1b) { $failures += 'S1: hash mudou apos abrir+salvar (corrupcao byte a byte)' }

# ---- S2: UTF-8 LF aberto pela principal ----
Matar
$hash2a = HashDe $f2
AbrirViaProgId $f2
if (-not (Esperar { TemEditor -and (AbaExiste 'slinky-int-lf') } 30)) { $failures += 'S2: arquivo LF nao abriu' }
$exibido2 = Normalizar (EditorTexto)
if ($exibido2 -ne (Normalizar $c2)) { $failures += 'S2: texto LF exibido difere do arquivo' }
Salvar
$hash2b = HashDe $f2
'2. UTF-8 LF: hash antes=' + $hash2a.Substring(0, 12) + ' depois=' + $hash2b.Substring(0, 12)
if ($hash2a -ne $hash2b) { $failures += 'S2: hash mudou apos abrir+salvar LF (EOL nao preservado?)' }

# ---- S3: Windows-1252 aberto pela principal ----
Matar
$hash3a = HashDe $f3
AbrirViaProgId $f3
if (-not (Esperar { TemEditor -and (AbaExiste 'slinky-int-1252') } 30)) { $failures += 'S3: arquivo 1252 nao abriu' }
$exibido3 = Normalizar (EditorTexto)
if ($exibido3 -ne (Normalizar $c3)) { $failures += 'S3: texto 1252 exibido difere do arquivo (decodificacao?)' }
Salvar
$hash3b = HashDe $f3
'3. Windows-1252: hash antes=' + $hash3a.Substring(0, 12) + ' depois=' + $hash3b.Substring(0, 12)
if ($hash3a -ne $hash3b) { $failures += 'S3: hash mudou apos abrir+salvar 1252 (re-encode corrupto?)' }

# ---- S4: redirect com fila: abrir arquivo com principal VIVA ----
Matar
AbrirViaProgId $f1   # principal com a1
Esperar { TemEditor } 30 | Out-Null
$hash4a = HashDe $f3
AbrirViaProgId $f3   # deve REDIRECIONAR para a principal e virar aba
if (-not (Esperar { (AbaExiste 'slinky-int-1252') } 30)) { $failures += 'S4: redirect nao abriu o arquivo como aba' }
'4. redirect: processos=' + (Procs) + ' aba 1252 presente=' + (AbaExiste 'slinky-int-1252')
if ((Procs) -ne 1) { $failures += 'S4: redirect criou segunda janela (processos=' + (Procs) + ')' }
$exibido4 = Normalizar (EditorTexto)
if ($exibido4 -ne (Normalizar $c3)) { $failures += 'S4: conteudo via fila/redirect difere do arquivo' }
Salvar
$hash4b = HashDe $f3
if ($hash4a -ne $hash4b) { $failures += 'S4: hash mudou apos abrir via redirect + salvar' }

# ---- S5: multi-arquivos por argv (exe direto com 2 caminhos) ----
Matar
AbrirViaProgId $f1   # principal
Esperar { TemEditor } 30 | Out-Null
$f2b = Copy-Item $f2 (Join-Path $env:TEMP 'slinky-int-lf2.txt') -PassThru
$pkg = Get-AppxPackage -Name '46cebc95-b2f1-4e6a-819f-0227204d1c62'
$exe = Join-Path $pkg.InstallLocation 'Slinky.exe'
try {
    Start-Process $exe -ArgumentList ('"' + $f2 + '" "' + $f3 + '"') -ErrorAction Stop
} catch {
    $failures += 'S5: Process.Start do exe falhou: ' + $_.Exception.Message
}
$okS5 = Esperar { (AbaExiste 'slinky-int-lf') -and (AbaExiste 'slinky-int-1252') } 30
'5. argv multi-arquivos: abas lf=' + (AbaExiste 'slinky-int-lf') + ' 1252=' + (AbaExiste 'slinky-int-1252') + ' processos=' + (Procs)
if (-not $okS5) { $failures += 'S5: multi-arquivos por argv nao abriu as duas abas' }
if ((Procs) -ne 1) { $failures += 'S5: argv multi criou janelas demais (processos=' + (Procs) + ')' }
Remove-Item $f2b -Force -ErrorAction SilentlyContinue

# ---- limpeza ----
Matar
Remove-Item $f1, $f2, $f3 -Force -ErrorAction SilentlyContinue
'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'INTEGRIDADE BYTE-A-BYTE PASS'
