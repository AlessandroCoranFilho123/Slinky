[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputCertificatePath
)

$ErrorActionPreference = 'Stop'

if ($null -eq (Get-PSDrive -Name Cert -ErrorAction SilentlyContinue)) {
    $provider = Get-PSProvider -ErrorAction SilentlyContinue | Where-Object Name -eq 'Certificate'
    if ($null -eq $provider) {
        throw "O provedor de certificados do Windows não está disponível. Provedores: $((Get-PSProvider -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name) -join ',')"
    }
    New-PSDrive -Name Cert -PSProvider $provider -Root '\' | Out-Null
}

$certificate = Get-ChildItem -Path 'Cert:\CurrentUser\My' -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Subject -eq 'CN=Alessandro' -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date) -and
        $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3'
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=Alessandro' `
        -FriendlyName 'Slinky Development Certificate' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable
}

$parent = Split-Path -Parent $OutputCertificatePath
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

Export-Certificate -Cert $certificate -FilePath $OutputCertificatePath -Type CERT | Out-Null
$certificate.Thumbprint
