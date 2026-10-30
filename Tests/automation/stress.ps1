$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ws = New-Object -ComObject WScript.Shell
function Janela {
  $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
  if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
  [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function Eds($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))) }
function Btns($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Button)))) }
function Tabs($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)))) }
function Metrica {
  $p = Get-Process Slinky -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { return 'sem processo' }
  $wmi = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter "IDProcess = $($p.Id)"
  "WS=$([Math]::Round($p.WorkingSet64/1MB,0))MB PWS=$([Math]::Round($wmi.WorkingSetPrivate/1MB,0))MB PB=$([Math]::Round($p.PrivateMemorySize64/1MB,0))MB handles=$($p.HandleCount) threads=$($p.Threads.Count)"
}
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
$ls = Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState'
Remove-Item (Join-Path $ls 'Recovery\*') -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ls 'session-*.json') -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
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
"stress inicio: $(Metrica)"

"=== A: 100 abas (Ctrl+T, texto, Ctrl+W com descarte) ==="
$amostras = @()
for ($i = 1; $i -le 100; $i++) {
  $ws.SendKeys('^t'); Start-Sleep -Milliseconds 250
  $ws.SendKeys("stress $i")
  Start-Sleep -Milliseconds 150
  $ws.SendKeys('^w')
  Start-Sleep -Milliseconds 350
  $r = Janela
  if ($r) {
    $nao = Btns $r | Where-Object { $_.Current.Name -eq 'Não salvar' } | Select-Object -First 1
    if ($nao) { $nao.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 300 }
  }
  if ($i % 25 -eq 0) { $amostras += "ciclo $i : $(Metrica)" }
}
$amostras
$r = Janela
"abas restantes: $(@($r | ForEach-Object { Tabs $_ }).Count)"

"=== B: 300 ciclos de edicao e undo ==="
$r = Janela
$ed = (Eds $r)[0]; $ed.SetFocus(); Start-Sleep -Milliseconds 300
$antesB = Metrica
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 1; $i -le 300; $i++) {
  $ws.SendKeys("x$i ")
  if ($i % 3 -eq 0) { $ws.SendKeys('^z') }
  Start-Sleep -Milliseconds 60
  if ($i % 100 -eq 0) { "ciclo $i : $(Metrica) | $($sw.ElapsedMilliseconds)ms" }
}
"tempo total B: $($sw.ElapsedMilliseconds)ms | antes: $antesB | depois: $(Metrica)"
# undo de tudo e limpar
for ($i = 1; $i -le 30; $i++) { $ws.SendKeys('^z'); Start-Sleep -Milliseconds 40 }
$ws.SendKeys('^a{DELETE}')
Start-Sleep -Milliseconds 400

"=== C: busca repetida (20x) ==="
$r = Janela
$ed = (Eds $r)[0]
$ed.SetFocus(); Start-Sleep -Milliseconds 200
$ws.SendKeys('palavra de busca repetida para performance de busca com volume razoavel de texto')
Start-Sleep -Milliseconds 300
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 1; $i -le 20; $i++) {
  $ws.SendKeys('^f'); Start-Sleep -Milliseconds 200
  $ws.SendKeys('busca'); Start-Sleep -Milliseconds 200
  $ws.SendKeys('{ESC}'); Start-Sleep -Milliseconds 200
}
$sw.Stop()
"C: 20 buscas em $($sw.ElapsedMilliseconds)ms | $(Metrica)"

"=== D: alternancia de abas (50x) ==="
$ws.SendKeys('^t'); Start-Sleep -Milliseconds 300
$ws.SendKeys('segunda aba')
Start-Sleep -Milliseconds 300
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 1; $i -le 50; $i++) {
  $ws.SendKeys('^{TAB}')
  Start-Sleep -Milliseconds 120
}
$sw.Stop()
"D: 50 alternancias em $($sw.ElapsedMilliseconds)ms | $(Metrica)"

"=== final: fechar tudo limpo e medir ==="
for ($i = 0; $i -lt 3; $i++) {
  $r = Janela
  if (-not $r) { break }
  $ed = (Eds $r)[0]; $ed.SetFocus(); Start-Sleep -Milliseconds 200
  $ws.SendKeys('^w'); Start-Sleep -Milliseconds 700
  $r = Janela
  if ($r) {
    $nao = Btns $r | Where-Object { $_.Current.Name -eq 'Não salvar' } | Select-Object -First 1
    if ($nao) { $nao.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 600 }
  }
}
Start-Sleep -Seconds 5
"stress fim: $(Metrica)"
$vivo = Get-Process Slinky -ErrorAction SilentlyContinue
"app vivo apos stress: $([bool]$vivo) | responding: $(if ($vivo) { $vivo[0].Responding } else { 'n/a' })"
