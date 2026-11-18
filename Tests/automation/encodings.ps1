# Round-trip byte a byte das codificacoes suportadas, pela rota de abertura real
# (FUA via ProgId — sem dialogo nativo, que nao e automatizavel de forma estavel;
# a versao anterior deste script usava Ctrl+O + FilePicker e era fragil por foco).
# Para cada arquivo do corpus: hash SHA256 original -> abrir -> conferir o TEXTO
# exibido no editor via UIA (normalizando quebras) -> Ctrl+S -> hash final.
# Hash identico = abertura e salvamento preservaram BOM, encoding e EOL byte a byte.
# Excecao documentada: mixed-eol e normalizado para CRLF seguro no salvar (o app
# nao pode reproduzir EOL misto); os demais preservam os bytes originais.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32Q {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ShellExecuteInfo4
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
public class ShellExec5
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ShellExecuteEx(ref ShellExecuteInfo4 info);
    public static void OpenWithProgId(string progId, string filePath)
    {
        ShellExecuteInfo4 info = new ShellExecuteInfo4();
        info.cbSize = Marshal.SizeOf(typeof(ShellExecuteInfo4));
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
$dir = Join-Path $env:TEMP 'qa-corpus'
New-Item -ItemType Directory -Path $dir -Force | Out-Null

function Janela {
    $p = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $p) { return $null }
    try { [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle) } catch { return $null }
}
function Eds($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))) }
function TemEditor { $r = Janela; $r -and @(Eds $r).Count -gt 0 }
function AbaExiste($nomeParcial) {
    $r = Janela
    if (-not $r) { return $false }
    $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)
    $tabs = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $cond))
    return (@($tabs | Where-Object { $_.Current.Name -match $nomeParcial }).Count -ge 1)
}
function Esperar($condicao, $segundos) {
    for ($i = 0; $i -lt ($segundos * 2); $i++) {
        Start-Sleep -Milliseconds 500
        if (& $condicao) { return $true }
    }
    return $false
}
function EditorTexto {
    $r = Janela
    if (-not $r) { return $null }
    # o editor da ABA SELECIONADA: com varias abas abertas, o ultimo Edit da
    # arvore pode pertencer a outra aba
    $condTab = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)
    $tabs = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condTab))
    $ativa = $tabs | Where-Object {
        try { return $_.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected } catch { return $false }
    } | Select-Object -First 1
    if ($ativa) {
        $condE = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)
        $edits = @($ativa.FindAll([Windows.Automation.TreeScope]::Descendants, $condE))
        if ($edits.Count -gt 0) {
            try { return $edits[$edits.Count - 1].GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { return $null }
        }
    }
    $edits = Eds $r
    if ($edits.Count -eq 0) { return $null }
    try { return $edits[$edits.Count - 1].GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { return $null }
}
function Normalizar([string]$s) { if ($null -eq $s) { return '' }; return ($s -replace "`r`n", "`n" -replace "`r", "`n") }
function HashDe($arquivo) { (Get-FileHash -Path $arquivo -Algorithm SHA256).Hash }
function Salvar {
    [W32Q]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32Q]::keybd_event(0x53, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [W32Q]::keybd_event(0x53, 0, 2, [UIntPtr]::Zero)
    [W32Q]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Seconds 3
}
function AbrirViaProgId($arquivo) {
    $progids = (Get-ItemProperty 'HKCU:\SOFTWARE\Classes\.txt\OpenWithProgids' -ErrorAction Stop).PSObject.Properties.Name | Where-Object { $_ -like 'AppX*' }
    $progid = $progids | Select-Object -First 1
    if (-not $progid) { throw 'ProgId AppX do .txt nao encontrado' }
    [ShellExec5]::OpenWithProgId($progid, $arquivo)
}

# corpus: nome, bytes do arquivo, texto DECODIFICADO esperado no editor
$utf8SemBom = New-Object System.Text.UTF8Encoding($false)
$utf8ComBom = New-Object System.Text.UTF8Encoding($true)
$cp = [System.Text.Encoding]::GetEncoding(1252)
$acao = [string][char]0xE7 + [char]0xE3 + 'o'
$texto1252 = 'a' + [char]0xE7 + [char]0xE3 + 'o caf' + [char]0xE9 + "`n" + 'linha com ' + [char]0xC0 + [char]0xC7 + [char]0xD5 + "`n"

$corpus = @(
    @{ f = 'utf8-lf.txt';       bytes = $utf8SemBom.GetBytes('linha um acao coracao' + "`n" + 'linha dois' + "`n" + 'terceira 123 999' + "`n"); texto = 'linha um acao coracao' + "`n" + 'linha dois' + "`n" + 'terceira 123 999' + "`n" }
    @{ f = 'utf8bom-crlf.txt';  bytes = $utf8ComBom.GetBytes('linha um acao coracao' + "`r`n" + 'linha dois' + "`r`n" + 'terceira 123 999' + "`r`n"); texto = 'linha um acao coracao' + "`r`n" + 'linha dois' + "`r`n" + 'terceira 123 999' + "`r`n" }
    @{ f = 'utf16le.txt';       bytes = [System.Text.Encoding]::Unicode.GetBytes('linha um acao coracao' + "`n" + 'linha dois' + "`n" + 'terceira 123 999' + "`n"); texto = 'linha um acao coracao' + "`n" + 'linha dois' + "`n" + 'terceira 123 999' + "`n" }
    @{ f = 'utf16be.txt';       bytes = [System.Text.Encoding]::BigEndianUnicode.GetBytes('linha um acao coracao' + "`n" + 'linha dois' + "`n" + 'terceira 123 999' + "`n"); texto = 'linha um acao coracao' + "`n" + 'linha dois' + "`n" + 'terceira 123 999' + "`n" }
    @{ f = 'ansi1252.txt';      bytes = $cp.GetBytes($texto1252); texto = $texto1252 }
    @{ f = 'emoji-unicode.txt'; bytes = $utf8ComBom.GetBytes('emoji: ' + [char]0x1F600 + ' e acentos ' + $acao + "`n"); texto = 'emoji: ' + [char]0x1F600 + ' e acentos ' + $acao + "`n" }
    @{ f = 'mixed-eol.txt';     bytes = [System.Text.Encoding]::ASCII.GetBytes('linha A' + "`r`n" + 'linha B' + "`n" + 'linha C' + "`r`n"); texto = 'linha A' + "`n" + 'linha B' + "`n" + 'linha C' + "`n"; normalizado = $true }
    @{ f = 'vazio.txt';         bytes = $utf8SemBom.GetBytes(''); texto = '' }
    @{ f = 'so-brancos.txt';    bytes = [System.Text.Encoding]::ASCII.GetBytes("`r`n`r`n`r`n "); texto = "`n`n`n " }
)

# ---- estado limpo e corpus no disco ----
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item (Join-Path $env:LOCALAPPDATA ('Packages\' + $family + '\LocalState\Recovery\*')) -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA ('Packages\' + $family + '\LocalState\session-*.json')) -Force -Recurse -ErrorAction SilentlyContinue
foreach ($c in $corpus) { [IO.File]::WriteAllBytes((Join-Path $dir $c.f), $c.bytes) }
Start-Sleep -Seconds 10

# ---- round-trip de cada caso ----
foreach ($c in $corpus) {
    $caminho = Join-Path $dir $c.f
    $hashAntes = HashDe $caminho
    try { AbrirViaProgId $caminho } catch { $failures += ($c.f + ': abertura falhou: ' + $_.Exception.Message); continue }
    if (-not (Esperar { TemEditor -and (AbaExiste ($c.f -replace '\.txt$', '')) } 30)) { $failures += ($c.f + ': nao abriu'); continue }
    [W32Q]::SetForegroundWindow((Get-Process Slinky | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500

    $exibido = Normalizar (EditorTexto)
    $esperado = Normalizar $c.texto
    if ($exibido -ne $esperado) {
        $failures += ($c.f + ': texto exibido difere (len=' + $exibido.Length + ', esperado=' + $esperado.Length + ')')
    }

    Salvar
    $hashDepois = HashDe $caminho
    if ($hashAntes -ne $hashDepois) {
        $failures += ($c.f + ': bytes mudaram ao salvar (' + $hashAntes.Substring(0, 8) + ' -> ' + $hashDepois.Substring(0, 8) + ')')
    }
    Write-Host ($c.f + ': display=' + ($exibido -eq $esperado) + ' bytes=' + ($hashAntes -eq $hashDepois))
}

Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
'---'
if ($failures.Count -gt 0) { 'FALHAS:'; $failures | ForEach-Object { ' - ' + $_ }; exit 1 }
'ENCODINGS BYTE-A-BYTE PASS'
