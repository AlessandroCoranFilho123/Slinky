[CmdletBinding()]
param(
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\Slinky-v1.0.1-x64.msixbundle'),
    [string]$CertificatePath = (Join-Path $PSScriptRoot '..\Slinky-Development.cer')
)

$ErrorActionPreference = 'Stop'
$windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = @(
        '-NoProfile'
        '-ExecutionPolicy', 'Bypass'
        '-File', $PSCommandPath
        '-PackagePath', $PackagePath
        '-CertificatePath', $CertificatePath
    )
    Start-Process -FilePath $windowsPowerShell -Verb RunAs -ArgumentList $arguments -Wait
    exit $LASTEXITCODE
}

foreach ($path in @($PackagePath, $CertificatePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Arquivo não encontrado: $path"
    }
}

$certificate = Get-PfxCertificate -FilePath $CertificatePath
Write-Host "Confiando no certificado $($certificate.Thumbprint) ($($certificate.Subject))..."
Import-Certificate -FilePath $CertificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Import-Certificate -FilePath $CertificatePath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null

Get-AppxPackage | Where-Object {
    $_.Name -eq '46cebc95-b2f1-4e6a-819f-0227204d1c62' -or $_.DisplayName -eq 'Slinky'
} | Remove-AppxPackage -ErrorAction SilentlyContinue

Add-AppxPackage -Path (Resolve-Path -LiteralPath $PackagePath).Path -ForceApplicationShutdown -ForceUpdateFromAnyVersion
Write-Host 'Slinky instalado com sucesso.'
