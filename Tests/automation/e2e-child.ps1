# Filho do smoke-e2e: verifica o estado do app e opcionalmente atua.
# Roda em POWERSHELL NOVO por chamada — o client UIA do pai fica amarrado a
# janelas mortas depois de um Stop-Process no mesmo processo (descoberta
# 17/11/2026), e cada filho nasce com cache de UIA limpo.
# Saida: JSON em %TEMP%\slinky-e2e-child.json com
#   { temEditor, temRestaurar, valor }  (valor = texto do editor da aba ativa)
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
param(
    [int]$TimeoutSegundos = 20,
    [ValidateSet('nenhuma', 'restaurar', 'digitar')]
    [string]$Acao = 'nenhuma',
    [string]$Texto = '',
    [string]$Foto = ''
)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32C {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$out = Join-Path $env:TEMP 'slinky-e2e-child.json'
$iterLog = Join-Path $env:TEMP 'slinky-e2e-child-iter.log'
Remove-Item $iterLog -Force -ErrorAction SilentlyContinue
function LogIter([string]$m) { try { Add-Content -Path $iterLog -Value $m -ErrorAction SilentlyContinue } catch { } }
$temEditor = $false
$temRestaurar = $false
$valor = ''
$ws = New-Object -ComObject WScript.Shell

function Janela {
    foreach ($p in (Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })) {
        try {
            $el = [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
            if ($el) { return $el }
        } catch { }
    }
    return $null
}
function Eds($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))) }
function ValorEditor {
    $r = Janela
    if (-not $r) { return '' }
    # o editor da ABA SELECIONADA: com varias abas, o primeiro Edit da arvore pode
    # pertencer a outra aba (nao selecionada)
    $condTab = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)
    $tabs = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condTab))
    $ativa = $tabs | Where-Object {
        try { return $_.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected } catch { return $false }
    } | Select-Object -First 1
    if ($ativa) {
        $condE = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)
        $edits = @($ativa.FindAll([Windows.Automation.TreeScope]::Descendants, $condE))
        if ($edits.Count -gt 0) {
            try { return $edits[$edits.Count - 1].GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { return '' }
        }
    }
    $edits = Eds $r
    if ($edits.Count -eq 0) { return '' }
    try { return $edits[$edits.Count - 1].GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { return '' }
}

for ($i = 0; $i -lt ($TimeoutSegundos * 2); $i++) {
    Start-Sleep -Milliseconds 500
    $r = Janela
    if (-not $r) { continue }
    $edits = Eds $r
    $temEditor = @($edits).Count -gt 0
    $condB = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Button)
    $btns = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, $condB))
    $restaurar = $btns | Where-Object { $_.Current.Name -eq 'Restaurar' } | Select-Object -First 1
    $temRestaurar = ($null -ne $restaurar)
    LogIter ('t' + $i + ' procs=' + @(Get-Process Slinky -ErrorAction SilentlyContinue).Count + ' temEditor=' + $temEditor + ' temRestaurar=' + $temRestaurar + ' btns=[' + (($btns | ForEach-Object { $_.Current.Name }) -join ',') + ']')

    if ($temEditor -and $Acao -eq 'digitar' -and $Texto) {
        $valor = ValorEditor
        if ($valor -notmatch [regex]::Escape($Texto)) {
            [W32C]::SetForegroundWindow((Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle) | Out-Null
            Start-Sleep -Milliseconds 300
            try { $edits[0].SetFocus() | Out-Null } catch { }
            Start-Sleep -Milliseconds 200
            $ws.SendKeys($Texto)
            Start-Sleep -Milliseconds 500
            $valor = ValorEditor
        }
    }

    if (-not $temEditor -and $temRestaurar -and $Acao -eq 'restaurar') {
        try { $restaurar.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { }
        # o restore e assincrono (reattach ao arquivo + texto na UI): aguarda
        # estabilizar antes de ler o valor
        Start-Sleep -Seconds 5
    }

    if ($temEditor -and $Acao -ne 'restaurar') { break }
    if ($temEditor -and $Acao -eq 'restaurar' -and $temRestaurar) { continue }
    if ($temEditor) { break }
}

if ($Acao -eq 'digitar') { $valor = ValorEditor }
if ($temEditor -and $Foto) {
    Start-Sleep -Seconds 3
    $valor = ValorEditor
}
$fotoErro = ''
if ($temEditor -and $Foto) {
    try {
        $pf = Get-Process Slinky -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
        if ($pf) {
            $rectF = New-Object W32C+RECT
            [W32C]::GetWindowRect($pf.MainWindowHandle, [ref]$rectF) | Out-Null
            $bmpF = New-Object System.Drawing.Bitmap(($rectF.Right - $rectF.Left), ($rectF.Bottom - $rectF.Top))
            $gF = [System.Drawing.Graphics]::FromImage($bmpF)
            $hdcF = $gF.GetHdc()
            [W32C]::PrintWindow($pf.MainWindowHandle, $hdcF, 2) | Out-Null
            $gF.ReleaseHdc($hdcF); $gF.Dispose()
            $bmpF.Save($Foto, [System.Drawing.Imaging.ImageFormat]::Png)
            $bmpF.Dispose()
        }
    } catch { $fotoErro = $_.Exception.Message }
}
@{ temEditor = $temEditor; temRestaurar = $temRestaurar; valor = $valor; fotoErro = $fotoErro } |
    ConvertTo-Json -Compress | Set-Content -Path $out -Encoding UTF8
exit 0
