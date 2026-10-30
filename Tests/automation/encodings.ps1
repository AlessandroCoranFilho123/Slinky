$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ws = New-Object -ComObject WScript.Shell
$dir = 'C:\Users\Alessandro\AppData\Local\Temp\qa-corpus'
function Janela {
  $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
  if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
  [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function Eds($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))) }
function Btns($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Button)))) }

# estado limpo
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$ls = Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState'
Remove-Item (Join-Path $ls 'Recovery\*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ls 'session-*.json') -Force -ErrorAction SilentlyContinue
Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
$r = $null
for ($i = 0; $i -lt 60; $i++) {
  Start-Sleep -Milliseconds 500
  $r = Janela
  if (-not $r) { continue }
  $res = Btns $r | Where-Object { $_.Current.Name -eq 'Restaurar' } | Select-Object -First 1
  if ($res) { $res.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Seconds 2; continue }
  if (@(Eds $r).Count -gt 0) { break }
}
if (-not $r -or @(Eds $r).Count -eq 0) { 'app nao subiu'; exit 1 }

# casos: nome, decodificador .NET, BOM esperado no salvar, eol esperado no salvar
# texto base = decodificacao do arquivo original (sem BOM)
$casos = @(
  @{ f = 'utf8-lf.txt';       encName = 'utf8';  bom = $false; eol = "`n";    tail = ' 999' },
  @{ f = 'utf8bom-crlf.txt';  encName = 'utf8';  bom = $true;  eol = "`r`n";  tail = ' 999' },
  @{ f = 'utf16le.txt';       encName = 'u16le'; bom = $true;  eol = "`n";    tail = ' 999' },
  @{ f = 'utf16be.txt';       encName = 'u16be'; bom = $true;  eol = "`n";    tail = ' 999' },
  @{ f = 'ansi1252.txt';      encName = 'w1252'; bom = $false; eol = "`n";    tail = ' 999' },
  @{ f = 'emoji-unicode.txt'; encName = 'utf8';  bom = $true;  eol = "`n";    tail = ' 999' },
  @{ f = 'mixed-eol.txt';     encName = 'ascii'; bom = $false; eol = "`r`n";  tail = ' 999'; normalizado = $true },
  @{ f = 'vazio.txt';         encName = 'utf8';  bom = $false; eol = "`r`n";  tail = ' 999' },
  @{ f = 'so-brancos.txt';    encName = 'ascii'; bom = $false; eol = "`r`n";  tail = ' 999'; normalizado = $true }
)

foreach ($c in $casos) {
  $caminho = Join-Path $dir $c.f
  # abrir via Ctrl+O
  $ed = (Eds (Janela))[0]; $ed.SetFocus(); Start-Sleep -Milliseconds 250
  $ws.SendKeys('^o'); Start-Sleep -Milliseconds 1700
  $r = Janela
  $nomeBox = @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))) | Where-Object { $_.Current.Name -match 'Nome' } | Select-Object -First 1
  if ($nomeBox) { try { $nomeBox.SetFocus(); Start-Sleep -Milliseconds 250 } catch {} }
  $ws.SendKeys($caminho)
  Start-Sleep -Milliseconds 350
  $ws.SendKeys('{ENTER}')
  Start-Sleep -Milliseconds 1500
  $r = Janela
  $ed = (Eds $r)[0]
  $valor = $ed.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value

  # comparacao de conteudo exibido: reconstruir esperado a partir do arquivo
  $bytes = [System.IO.File]::ReadAllBytes($caminho)
  $textoDisco = ''
  switch ($c.encName) {
    'utf8'  { $textoDisco = [Text.Encoding]::UTF8.GetString($bytes) }
    'u16le' { $textoDisco = [Text.Encoding]::Unicode.GetString($bytes) }
    'u16be' { $textoDisco = [Text.Encoding]::BigEndianUnicode.GetString($bytes) }
    'w1252' { $textoDisco = [Text.Encoding]::GetEncoding(1252).GetString($bytes) }
    default { $textoDisco = [Text.Encoding]::ASCII.GetString($bytes) }
  }
  $textoDisco = $textoDisco.TrimStart([char]0xFEFF) -replace "`r`n", "`n"
  if ($c.f -eq 'mixed-eol.txt' -or $c.f -eq 'so-brancos.txt') { $textoDisco = $textoDisco }  # ja normalizado
  $esperadoDisplay = ($textoDisco -replace "`n", "`r")
  $displayOk = $valor -eq $esperadoDisplay
  if ($displayOk) {
    "[$($c.f)] display=OK"
  } else {
    $vMostrado = $valor -replace [char]13, '<CR>'
    $eMostrado = $esperadoDisplay -replace [char]13, '<CR>'
    "[$($c.f)] display=DIF valor=[$vMostrado] esperado=[$eMostrado]"
  }

  # editar (End + ' 999') e salvar
  $ed.SetFocus(); Start-Sleep -Milliseconds 250
  $ws.SendKeys('^({END})')
  Start-Sleep -Milliseconds 150
  $ws.SendKeys(' 999')
  Start-Sleep -Milliseconds 350
  $ws.SendKeys('^s')
  Start-Sleep -Milliseconds 1000

  # bytes esperados: texto final = original (normalizado p/ eol do caso) + tail, com eol do caso; BOM do caso
  $final = $textoDisco + $c.tail
  if ($c.normalizado) { $final = ($textoDisco -replace "`n", $c.eol) + $c.tail }
  elseif ($c.eol -ne "`n") { $final = ($textoDisco -replace "`n", "`r`n") + $c.tail }
  $final = $final -replace "`n", $c.eol
  $encFinal = switch ($c.encName) {
    'utf8'  { [Text.Encoding]::UTF8 }
    'u16le' { [Text.Encoding]::Unicode }
    'u16be' { [Text.Encoding]::BigEndianUnicode }
    'w1252' { [Text.Encoding]::GetEncoding(1252) }
    default { [Text.Encoding]::ASCII }
  }
  $esperadoBytes = $encFinal.GetBytes($final)
  if ($c.bom) {
    $pre = switch ($c.encName) { 'u16le' { [byte[]](0xFF,0xFE) } 'u16be' { [byte[]](0xFE,0xFF) } default { [byte[]](0xEF,0xBB,0xBF) } }
    $esperadoBytes = $pre + $esperadoBytes
  }
  $reais = [System.IO.File]::ReadAllBytes($caminho)
  $bytesOk = ($reais.Length -eq $esperadoBytes.Length)
  if ($bytesOk) { for ($i = 0; $i -lt $reais.Length; $i++) { if ($reais[$i] -ne $esperadoBytes[$i]) { $bytesOk = $false; break } } }
  if ($bytesOk) {
    "[$($c.f)] roundtrip_bytes=OK"
  } else {
    $hexR = ($reais[0..8] | ForEach-Object { $_.ToString('X2') }) -join ' '
    $hexE = ($esperadoBytes[0..8] | ForEach-Object { $_.ToString('X2') }) -join ' '
    "[$($c.f)] roundtrip_bytes=DIF reais=$($reais.Length)b esperados=$($esperadoBytes.Length)b hexR=[$hexR] hexE=[$hexE]"
  }

  # fechar aba (limpo apos salvar)
  $ed.SetFocus(); Start-Sleep -Milliseconds 200
  $ws.SendKeys('^w')
  Start-Sleep -Milliseconds 900
  $r = Janela
  $nao = Btns $r | Where-Object { $_.Current.Name -eq 'Não salvar' } | Select-Object -First 1
  if ($nao) { "[$($c.f)] AVISO: dialogo de fechamento apareceu apos salvar"; $nao.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 700 }
}
'teste de codificacoes concluido'
