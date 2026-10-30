# Smoke E2E: digitar -> autosave -> encerrar a forca -> reabrir -> Restaurar -> conferir.
# Exercita o caminho critico de nao-perda-de-dado (autosave + recovery) sem dialogos.
# Requisitos: pacote de desenvolvimento instalado. Nao usa documentos do usuario.
# Arquivo 100% ASCII de proposito (PowerShell 5.1 + UTF-8 sem BOM quebram acentos).
# Nota: dialogos nativos (Abrir/Salvar como) nao sao automatizaveis de forma
# estavel via UIA; ficam no roteiro manual docs/SMOKE-UI.md.
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ws = New-Object -ComObject WScript.Shell
$failures = @()

function Janela {
  $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
  if (-not $p -or $p.MainWindowHandle -eq 0) { return $null }
  [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}
function Eds($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)))) }
function Btns($r) { @($r.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Button)))) }
function ValorEditor { $r = Janela; $eds = Eds $r; if (@($eds).Count -gt 0) { $eds[0].GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } else { '' } }
function Esperar($condicao, $segundos) {
  for ($i = 0; $i -lt ($segundos * 2); $i++) {
    Start-Sleep -Milliseconds 500
    if (& $condicao) { return $true }
  }
  return $false
}
function TemEditor { $r = Janela; $r -and @(Eds $r).Count -gt 0 }
function LimparEstado {
  Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
  $ls = Join-Path $env:LOCALAPPDATA 'Packages\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj\LocalState'
  Remove-Item (Join-Path $ls 'Recovery\*') -Force -Recurse -ErrorAction SilentlyContinue
  Remove-Item (Join-Path $ls 'session-*.json') -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 1
}
function Lancar {
  Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
  $ok = Esperar { TemEditor -or ((Janela) -and ((Btns (Janela)) | Where-Object { $_.Current.Name -eq 'Restaurar' })) } 30
  if (-not $ok) { return $false }
  # clicar Restaurar UMA vez e aguardar (cliques repetidos re-invocam o restore e
  # criam notas vazias); se o clique inicial nao surtir efeito, um segundo apos 5s
  $r = Janela
  $res = Btns $r | Where-Object { $_.Current.Name -eq 'Restaurar' } | Select-Object -First 1
  if ($res) { try { $res.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() } catch {} }
  if (Esperar { TemEditor } 20) { return $true }
  $r = Janela
  $res2 = Btns $r | Where-Object { $_.Current.Name -eq 'Restaurar' } | Select-Object -First 1
  if ($res2) { try { $res2.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() } catch {} }
  return (Esperar { TemEditor } 15)
}

LimparEstado
if (-not (Lancar)) { 'FALHA: app nao abriu'; exit 1 }
'1. app aberto'

# digitar conteudo marcado
$r = Janela
$ed = (Eds $r)[0]; $ed.SetFocus(); Start-Sleep -Milliseconds 250
$ws.SendKeys('e2e recovery marcador 30-10')
Start-Sleep -Milliseconds 400
$valor = ValorEditor
if (-not ($valor -match 'recovery marcador')) { $failures += "digitacao nao chegou ao editor [$valor]" }
"2. digitado: [$valor]"

# autosave (debounce 2s + margem)
"3. aguardando autosave (6s)..."
Start-Sleep -Seconds 6

# encerrar a forca (simula crash); cooldown de 10s e obrigatorio para a
# reativacao MSIX apos um Stop-Process forcar (2s falha silenciosamente)
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 10

# reabrir: prompt de recovery -> Restaurar (com retry: apos kill, a reativacao
# MSIX precisa de cooldown; 2s falha silenciosamente, 10s vai sempre)
$reabriu = $false
foreach ($tentativa in 1..3) {
  if (Lancar) { $reabriu = $true; break }
  Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 10
}
if (-not $reabriu) {
  $p = Get-Process Slinky -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
  if ($p) {
    $r3 = Janela
    if ($r3) {
      $btns3 = (Btns $r3 | ForEach-Object { $_.Current.Name }) -join ','
      $edits3 = @(Eds $r3).Count
      $txts3 = (@($r3.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Text)))) | ForEach-Object { $_.Current.Name }) -join ' / '
      "diagnostico: edits=$edits3 botoes=[$btns3] textos=[$txts3]"
    } else { 'diagnostico: processo vivo sem janela UIA' }
  } else { 'diagnostico: sem processo Slinky apos 3 tentativas' }
  $failures += 'app nao reabriu apos crash (3 tentativas)'
  'SMOKE-E2E: FAIL'
  exit 1
}
$r = Janela
$chegou = Esperar { (ValorEditor) -match 'recovery marcador' } 15
$conteudo = ValorEditor
"4. reaberto; conteudo restaurado: [$conteudo]"
if (-not ($conteudo -match 'recovery marcador')) { $failures += 'recovery nao restaurou o conteudo digitado' }

# repetir o ciclo: a nota restaurada segue suja, entao um novo snapshot e oferecido
# (consumo unico do snapshot ANTIGO ja validado no disco); restaure e confirme de novo
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 10
Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
$segundaVez = Esperar { TemEditor } 30
Start-Sleep -Seconds 3
$r = Janela
$res = Btns $r | Where-Object { $_.Current.Name -eq 'Restaurar' } | Select-Object -First 1
if ($res) {
  $res.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
  Start-Sleep -Seconds 3
  $r = Janela
  $conteudo2 = (Eds $r)[0].GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value
  "5. segundo ciclo de recovery: [$conteudo2]"
  if (-not ($conteudo2 -match 'recovery marcador')) { $failures += 'segundo ciclo de recovery nao restaurou o conteudo' }
} else { $failures += 'segundo ciclo: prompt de recovery nao apareceu' }

# limpar
Get-Process Slinky -ErrorAction SilentlyContinue | Stop-Process -Force
LimparEstado

if (@($failures).Count -eq 0) { 'SMOKE-E2E: PASS' } else { "SMOKE-E2E: FAIL - $($failures -join '; ')" }
exit $(if (@($failures).Count -eq 0) { 0 } else { 1 })
