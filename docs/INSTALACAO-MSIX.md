# Instalação MSIX: causas raiz e receita verificada

Diagnóstico consolidado em 28/10/2026 a partir do log de eventos
`Microsoft-Windows-AppXDeploymentServer/Operational` (88 eventos do Slinky entre
14/10 e 21/10), do estado das stores de certificados e de reproduções controladas.
Use este documento quando `Add-AppxPackage` falhar ou antes da primeira instalação.

## Resumo dos erros e causas raiz

| Sintoma | Causa raiz | Correção |
|---|---|---|
| `0x800B0109` "certificado raiz deve ser confiável" (muitas vezes embrulhado em `0x87E80034`/`0x87EA0003` "leitura do manifesto falhou" e `0x80073CF0` "não foi possível abrir o pacote") | O certificado de assinatura não está nas stores **de máquina**. O AppXSvc roda como SYSTEM e **não consulta** as stores do usuário — confiança só em `CurrentUser\TrustedPeople` não basta. | Importar o `.cer` para `LocalMachine\TrustedPeople` (e `Root`), o que exige aprovação UAC interativa. |
| `0x80131539` `PlatformNotSupportedException` | Falha **no cliente**, antes de chegar ao AppXSvc (elevação `Start-Process -Verb RunAs` cancelada/indisponível em execução não assistida). O módulo Appx em si funciona — verificado em PowerShell 5.1 x86 e x64. | Rodar a instalação em sessão interativa e aprovar o UAC, ou importar o certificado antes, em passo elevado separado. |
| `0x80073CF3` "pacote diferente com o mesmo nome já está instalado" | Duas famílias com o mesmo `Name` (GUID) e publishers diferentes: dev `CN=Alessandro` (família `…_1xsa4dc8b90qj`) vs release `CN=Slinky Release` da CI (família `…_b2zjezss9gtqm`). São identidades distintas para o Windows. | `Remove-AppxPackage` da família antiga antes de instalar a nova (o `Install-ReleasePackage.ps1` já faz isso). |
| `0xC0000135` no executável unpackaged | VC++ Redistributable clássico (x64/x86) ausente — só havia pacotes UWP `VCLibs`, que não servem para processo sem identidade de pacote. | Instalar o VC++ Redistributable ou usar o app empacotado (o perfil unpackaged foi removido do projeto por design). |

## Linha do tempo das evidências

- **14/10 e manhã de 21/10** — dezenas de instalações falhando: eventos 402/495 com
  `0x87E80034`; o evento 404 da mesma operação revela o erro real: `0x800B0109`
  (raiz não confiável).
- **21/10 20:08** — commit `0fa950d` passa a importar o certificado para as stores
  de máquina. Às **21:11** a instalação de `Slinky-v1.0.2-x64.msixbundle`
  (publisher `CN=Slinky Release`) **succeeds** — eventos 400/478.
- **22/10** — nenhum evento de deployment do Slinky no log: a exceção `0x80131539`
  registrada no smoke nasceu no cliente (caminho de elevação), nunca chegou ao serviço.
- **28/10** — reprodução controlada: certificado apenas em `CurrentUser\TrustedPeople`
  → `0x800B0109` de novo. Após importar para `LocalMachine\TrustedPeople`+`Root`
  (UAC aprovado), a assinatura passou; removida a família antiga, a instalação e o
  lançamento funcionaram.

## Receita verificada de instalação local (28/10/2026)

1. **Certificado**: reutilizar/criar `CN=Alessandro` com
   `scripts/Prepare-DevelopmentCertificate.ps1` (chave privada em `CurrentUser\My`,
   EKU de code signing). Em 28/10 o thumbprint vigente era
   `307E3EC24E852C4588156FDB999433A1CCC11BC0`, válido até 14/10/2027, já presente
   também nas stores de máquina.
2. **Build assinado** (receita da CI, ver `AGENTS.md` para o comando completo):
   `AppxPackageSigningEnabled=true` + `PackageCertificateThumbprint=<thumbprint>` +
   `WindowsAppSDKSelfContained=true`. Saída em `Slinky/AppPackages/…_Test/`.
3. **Confiança de máquina** (uma vez por certificado; exige UAC interativo):
   ```powershell
   Import-Certificate -FilePath <cert.cer> -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
   Import-Certificate -FilePath <cert.cer> -CertStoreLocation 'Cert:\LocalMachine\Root'
   ```
4. **Remover família conflitante**, se houver (identificar por publisher):
   ```powershell
   Get-AppxPackage -Name '46cebc95-b2f1-4e6a-819f-0227204d1c62' | Format-List PackageFullName, Publisher
   Remove-AppxPackage -Package <PackageFullName da família outra>
   ```
5. **Instalar e lançar**:
   ```powershell
   Add-AppxPackage -Path <bundle> -ForceUpdateFromAnyVersion
   Start-Process 'shell:AppsFolder\46cebc95-b2f1-4e6a-819f-0227204d1c62_1xsa4dc8b90qj!App'
   ```

O `instalar-slinky.bat` automatiza os passos 1–5 (usa `certutil -addstore` elevado
para o passo 3) e é o caminho recomendado para máquinas de desenvolvimento.

## Identidades de pacote em jogo

| Origem | Publisher do certificado | Família resultante |
|---|---|---|
| Build local / `instalar-slinky.bat` | `CN=Alessandro` | `…_1xsa4dc8b90qj` |
| CI release (`.github/workflows/release.yml`) | `CN=Slinky Release` | `…_b2zjezss9gtqm` |

O `Name` no `Package.appxmanifest` é o GUID `46cebc95-b2f1-4e6a-819f-0227204d1c62`;
a família muda com o publisher. Instalar um build local por cima de um release (ou
vice-versa) sempre exige remover a outra família antes.

## Diagnóstico rápido

```powershell
# Últimas falhas de deployment do Slinky
Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-AppXDeploymentServer/Operational'; StartTime=(Get-Date).AddDays(-7)} |
  Where-Object { $_.Message -match 'Slinky|46cebc95' -and $_.LevelDisplayName -eq 'Erro' } |
  Select-Object TimeCreated, Id, Message | Format-List

# Log detalhado de uma ActivityId informada num erro do Add-AppxPackage
Get-AppPackageLog -ActivityId <id>
```

Regra de leitura: o evento **404** carrega o HRESULT real; os eventos 402/495
("leitura do manifesto") são ruído de superfície do mesmo fracasso.
