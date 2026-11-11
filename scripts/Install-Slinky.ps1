[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$CertificatePath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $PSScriptRoot 'Slinky-v1.0.9-x64.msixbundle'
}
if ([string]::IsNullOrWhiteSpace($CertificatePath)) {
    $CertificatePath = Join-Path $PSScriptRoot 'Slinky-Release.cer'
}
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
    # Propaga o resultado do processo elevado: sem -PassThru o exit code do filho
    # era descartado e uma instalacao que falhou elevada aparecia como sucesso.
    try {
        $elevated = Start-Process -FilePath $windowsPowerShell -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    } catch {
        Write-Host "Instalacao cancelada: o UAC nao foi aprovado ($($_.Exception.Message))."
        exit 1
    }
    exit $elevated.ExitCode
}

foreach ($path in @($PackagePath, $CertificatePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Arquivo não encontrado: $path"
    }
}

# O pacote e o certificado viajam juntos na release: confiar em um .cer que nao
# assinou o bundle seria adicionar uma raiz de confianca sem relacao com o app.
$signature = Get-AuthenticodeSignature -FilePath $PackagePath
$certificate = Get-PfxCertificate -FilePath $CertificatePath

if ($signature.Status -notin @('Valid', 'UnknownError') -or $null -eq $signature.SignerCertificate) {
    throw "O pacote nao tem assinatura valida (Status: $($signature.Status)). Nao confio nesse pacote."
}
if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw ("O certificado fornecido ({0} / {1}) nao e o signatario do pacote ({2} / {3}). " +
        "Nao instalo com um par pacote/certificado divergente.") -f `
        $certificate.Subject, $certificate.Thumbprint,
        $signature.SignerCertificate.Subject, $signature.SignerCertificate.Thumbprint
}

Write-Host "Confiando no certificado $($certificate.Thumbprint) ($($certificate.Subject))..."
# Somente TrustedPeople: comprovado por experimento (2026-11-02) que o MSIX
# self-signed instala com o certificado apenas em LocalMachine\TrustedPeople —
# importa-lo tambem como Root adicionaria uma autoridade certificadora da
# maquina permanentemente a cada release, sem necessidade tecnica.
Import-Certificate -FilePath $CertificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

# A identidade real do pacote e o Name (GUID) do manifesto — igual para builds
# local e de release. O DisplayName pode coincidir com aplicativos de terceiros
# e nao e usado para remover nada.
Get-AppxPackage | Where-Object {
    $_.Name -eq '46cebc95-b2f1-4e6a-819f-0227204d1c62'
} | Remove-AppxPackage -ErrorAction SilentlyContinue

Add-AppxPackage -Path (Resolve-Path -LiteralPath $PackagePath).Path -ForceApplicationShutdown -ForceUpdateFromAnyVersion
Write-Host 'Slinky instalado com sucesso.'
