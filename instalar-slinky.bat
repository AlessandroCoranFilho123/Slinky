@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Instala o Slinky a partir do codigo-fonte usando um pacote MSIX local.
rem Por padrao, o script usa um certificado de desenvolvimento local.

set "ROOT=%~dp0"
set "PROJECT=%ROOT%Slinky\Slinky.csproj"
set "APP_PACKAGES=%ROOT%Slinky\AppPackages"
set "CONFIGURATION=Release"
set "PLATFORM=x64"
set "RID=win-x64"
set "BUILD_ONLY=0"
set "SIGN_PACKAGE=1"

if /I "%PROCESSOR_ARCHITEW6432%"=="ARM64" (
    set "PLATFORM=ARM64"
    set "RID=win-arm64"
) else if /I "%PROCESSOR_ARCHITECTURE%"=="ARM64" (
    set "PLATFORM=ARM64"
    set "RID=win-arm64"
) else if /I "%PROCESSOR_ARCHITECTURE%"=="x86" (
    set "PLATFORM=x86"
    set "RID=win-x86"
)

:parse_arguments
if "%~1"=="" goto validate
if /I "%~1"=="--build-only" (
    set "BUILD_ONLY=1"
    shift
    goto parse_arguments
)
if /I "%~1"=="--unsigned" (
    set "SIGN_PACKAGE=0"
    shift
    goto parse_arguments
)
if /I "%~1"=="--x64" (
    set "PLATFORM=x64"
    set "RID=win-x64"
    shift
    goto parse_arguments
)
if /I "%~1"=="--x86" (
    set "PLATFORM=x86"
    set "RID=win-x86"
    shift
    goto parse_arguments
)
if /I "%~1"=="--arm64" (
    set "PLATFORM=ARM64"
    set "RID=win-arm64"
    shift
    goto parse_arguments
)
if /I "%~1"=="--help" goto help
if /I "%~1"=="/?" goto help

echo Opcao desconhecida: %~1
goto help_error

:validate
if not exist "%PROJECT%" (
    echo ERRO: projeto nao encontrado em "%PROJECT%".
    exit /b 1
)

where.exe dotnet >nul 2>&1
if errorlevel 1 (
    echo ERRO: o .NET SDK nao foi encontrado no PATH.
    echo Instale o SDK .NET 8 ou abra o script em um Developer Command Prompt.
    exit /b 1
)

set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"

if "%SIGN_PACKAGE%"=="1" call :prepare_certificate
if errorlevel 1 goto failed

echo.
echo Slinky - instalacao local
echo Arquitetura: %PLATFORM% (%RID%)
echo.
echo [1/3] Restaurando dependencias NuGet e o runtime %RID%...
dotnet restore "%PROJECT%" --runtime "%RID%" --property:Platform=%PLATFORM% --property:PublishTrimmed=false --property:PublishReadyToRun=false --property:WindowsAppSDKSelfContained=true
if errorlevel 1 goto failed

echo.
echo [2/3] Gerando o pacote MSIX self-contained...
if "%SIGN_PACKAGE%"=="1" (
    dotnet build "%PROJECT%" --configuration %CONFIGURATION% --no-restore --property:Platform=%PLATFORM% --property:RuntimeIdentifier=%RID% --property:GenerateAppxPackageOnBuild=true --property:PublishReadyToRun=false --property:PublishTrimmed=false --property:WindowsAppSDKSelfContained=true --property:AppxPackageSigningEnabled=true --property:PackageCertificateThumbprint=%CERT_THUMBPRINT%
) else (
    dotnet build "%PROJECT%" --configuration %CONFIGURATION% --no-restore --property:Platform=%PLATFORM% --property:RuntimeIdentifier=%RID% --property:GenerateAppxPackageOnBuild=true --property:PublishReadyToRun=false --property:PublishTrimmed=false --property:WindowsAppSDKSelfContained=true --property:AppxPackageSigningEnabled=false
)
if errorlevel 1 goto failed

set "SLINKY_APP_PACKAGES=%APP_PACKAGES%"
set "PACKAGE="
for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$best = $null; foreach($item in (Get-ChildItem -LiteralPath $env:SLINKY_APP_PACKAGES -Filter '*.msixbundle' -Recurse -File -ErrorAction SilentlyContinue)){if($null -eq $best -or $item.LastWriteTime -gt $best.LastWriteTime){$best = $item}}; if($best){$best.FullName}"`) do set "PACKAGE=%%P"

if not defined PACKAGE for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$best = $null; foreach($item in (Get-ChildItem -LiteralPath $env:SLINKY_APP_PACKAGES -Filter '*.msix' -Recurse -File -ErrorAction SilentlyContinue)){if($item.Name -notmatch 'scale-' -and ($null -eq $best -or $item.LastWriteTime -gt $best.LastWriteTime)){$best = $item}}; if($best){$best.FullName}"`) do set "PACKAGE=%%P"

if not defined PACKAGE (
    echo ERRO: o build terminou, mas nenhum pacote MSIX foi encontrado.
    exit /b 1
)

echo Pacote gerado: %PACKAGE%
if "%BUILD_ONLY%"=="1" goto success_build_only

echo.
echo [3/3] Instalando o pacote para o usuario atual...
set "SLINKY_PACKAGE=%PACKAGE%"
if "%SIGN_PACKAGE%"=="1" (
    for %%D in ("%PACKAGE%") do set "PACKAGE_DIR=%%~dpD"
    set "INSTALL_SCRIPT=!PACKAGE_DIR!Add-AppDevPackage.ps1"
    if exist "!INSTALL_SCRIPT!" (
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File "!INSTALL_SCRIPT!" -Force -SkipLoggingTelemetry
    ) else (
        set "SLINKY_CERT_PUBLIC_FILE=%CERT_PUBLIC_FILE%"
        powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $null = Import-Certificate -FilePath $env:SLINKY_CERT_PUBLIC_FILE -CertStoreLocation 'Cert:\CurrentUser\TrustedPeople'; Add-AppxPackage -Path $env:SLINKY_PACKAGE -ForceApplicationShutdown -ForceUpdateFromAnyVersion"
    )
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $command = Get-Command Add-AppxPackage -ErrorAction Stop; if(-not $command.Parameters.ContainsKey('AllowUnsigned')){throw 'Esta versao do Windows nao oferece instalacao de pacotes unsigned. Use o modo padrao, que assina o pacote com um certificado de desenvolvimento.'}; Add-AppxPackage -Path $env:SLINKY_PACKAGE -AllowUnsigned -ForceApplicationShutdown -ForceUpdateFromAnyVersion"
)
if errorlevel 1 goto failed

call :cleanup_certificate
echo.
echo Slinky instalado com sucesso.
echo Abra-o pelo menu Iniciar procurando por Slinky.
goto end

:success_build_only
call :cleanup_certificate
echo.
echo Pacote gerado com sucesso. A instalacao foi ignorada por causa de --build-only.
goto end

:failed
call :cleanup_certificate
echo.
echo A instalacao nao foi concluida. Verifique a mensagem acima.
exit /b 1

:help
echo Uso: %~nx0 [opcao]
echo.
echo Sem opcao       Restaura, gera e instala o Slinky.
echo --build-only    Apenas gera o pacote MSIX, sem instalar.
echo --unsigned      Gera pacote unsigned; recomendado apenas no Windows 11.
echo --x64           Forca a arquitetura x64.
echo --x86           Forca a arquitetura x86.
echo --arm64         Forca a arquitetura ARM64.
echo --help          Mostra esta ajuda.
exit /b 0

:help_error
echo.
goto help

:prepare_certificate
set "CERT_DIR=%TEMP%\SlinkyInstaller-%RANDOM%%RANDOM%"
set "CERT_PUBLIC_FILE=%CERT_DIR%\Slinky.cer"
set "CERT_THUMBPRINT="
if not exist "%CERT_DIR%" md "%CERT_DIR%"
set "SLINKY_CERT_DIR=%CERT_DIR%"
set "SLINKY_CERT_PUBLIC_FILE=%CERT_PUBLIC_FILE%"
for /f "usebackq delims=" %%T in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $cert = $null; foreach($candidate in (Get-ChildItem -Path 'Cert:\CurrentUser\My' -ErrorAction SilentlyContinue)){ $valid = $candidate.Subject -eq 'CN=Alessandro' -and $candidate.HasPrivateKey -and $candidate.NotAfter -gt (Get-Date); $basic = $false; $eku = $false; if($valid){foreach($extension in $candidate.Extensions){if($extension.Oid.Value -eq '2.5.29.19'){$basic = $true; if($extension.CertificateAuthority){$valid = $false}} elseif($extension.Oid.Value -eq '2.5.29.15' -and $extension.KeyUsages -ne 'DigitalSignature'){$valid = $false} elseif($extension.Oid.Value -eq '2.5.29.37'){$ekus = $extension.EnhancedKeyUsages.Value; if($ekus -contains '1.3.6.1.5.5.7.3.3'){$eku = $true} else {$valid = $false}; foreach($ekuItem in $ekus){if($ekuItem -ne '1.3.6.1.5.5.7.3.3' -and $ekuItem -ne '1.3.6.1.4.1.311.10.3.13' -and $ekuItem -ne '1.3.6.1.4.1.311.84.3.1' -and $ekuItem -ne '1.3.6.1.4.1.311.84.3.2'){$valid = $false}}}}}; if($valid -and $basic -and $eku -and $null -eq $cert){$cert = $candidate}}; if($null -eq $cert){$cert = New-SelfSignedCertificate -Type Custom -KeyUsage DigitalSignature -Subject 'CN=Alessandro' -FriendlyName 'Slinky Development Certificate' -CertStoreLocation 'Cert:\CurrentUser\My' -HashAlgorithm SHA256 -KeyExportPolicy Exportable -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')}; $null = Export-Certificate -Cert $cert -FilePath $env:SLINKY_CERT_PUBLIC_FILE; $cert.Thumbprint"`) do set "CERT_THUMBPRINT=%%T"
if errorlevel 1 (
    echo ERRO: nao foi possivel criar o certificado de desenvolvimento.
    exit /b 1
)
if not defined CERT_THUMBPRINT (
    echo ERRO: o certificado foi criado, mas o thumbprint nao foi encontrado.
    exit /b 1
)
exit /b 0

:cleanup_certificate
if "%SIGN_PACKAGE%"=="1" if defined CERT_DIR powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Remove-Item -LiteralPath $env:SLINKY_CERT_DIR -Recurse -Force -ErrorAction SilentlyContinue"
exit /b 0

:end
endlocal
exit /b 0
