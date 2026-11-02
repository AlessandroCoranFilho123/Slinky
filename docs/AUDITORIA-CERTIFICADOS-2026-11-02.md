# Auditoria de certificados e instalação — 02/11/2026

Auditoria de segurança dos scripts de certificado/instalação sobre a master
(`dceef5c`), com experimentos no Windows real (Win10 19045) e correções aplicadas
nesta data. Nada foi assumido pelos nomes dos arquivos — todo achado vem do código
e de testes executados na máquina.

## 1. Fluxo real de certificados (como é, não como parece)

Existem **duas identidades de confiança** no projeto, e os nomes dos arquivos
enganam:

| Fluxo | Cert de assinatura | Onde nasce | Package family |
|---|---|---|---|
| Build local (`instalar-slinky.bat`) | `CN=Alessandro` (persistente, `CurrentUser\My`, 307E/DF0B) | `scripts/Prepare-DevelopmentCertificate.ps1` | `…_1xsa4dc8b90qj` |
| Release CI (`release.yml`) | `CN=Slinky Release` (efêmero por run) | gerado no runner; publicado como asset **`Slinky-Release.cer`** (antes: `Slinky-Development.cer` — nome enganoso, corrigido) | `…_b2zjezss9gtqm` |

**Resposta à pergunta central**: o certificado criado por
`Prepare-DevelopmentCertificate.ps1` **NÃO é** o que assina os pacotes instalados
por `Install-ReleasePackage.ps1`. O instalador de release consome o `.cer`
publicado pela CI (assinatário `CN=Slinky Release`); o script de preparo atende
apenas o fluxo local. As assinaturas dos dois fluxos são válidas porque o
empacotamento MSIX **reescreve o `Publisher` do manifesto para o subject do
certificado** em tempo de build (observado nos pacotes instalados: publisher
`CN=Alessandro` no build local e `CN=Slinky Release` no de release, com o
manifesto do repositório dizendo `CN=Alessandro`).

`Install-ReleasePackage.ps1` **participa da distribuição pública**: é publicado
como asset `Install-Slinky.ps1` em cada release. `Prepare-DevelopmentCertificate.ps1`
é exclusivamente local (chamado por `instalar-slinky.bat`).

## 2. Prepare-DevelopmentCertificate.ps1

### A. Identidade (`CN=Alessandro`) — mantida
- O subject **precisa** corresponder ao Publisher efetivo do pacote (o
  empacotador reescreve o Publisher para o subject do cert; o processo de
  assinatura valida essa igualdade). Mudar para `CN=Slinky Development`
  funcionaria tecnicamente, mas: (a) criaria uma terceira família de pacote
  (`…_<hash novo>`), invalidando a atualização in-place das instalações dev
  desta máquina; (b) o nome `CN=Alessandro` já é público (consta no manifesto
  instalado) — não é exposição; (c) a identidade institucional para
  distribuição pública já existe (`CN=Slinky Release`). **Decisão: manter**, com
  esta justificativa registrada.
- Dependências do nome: manifesto (`Publisher`), stores desta máquina
  (`TrustedPeople`/`Root`), e o parâmetro `-Subject` do próprio script. Nenhum
  outro arquivo depende do friendly name.

### B. Chave privada — corrigido (NonExportable em novos certificados)
- A chave nasce em `Cert:\CurrentUser\My` (CNG/CryptoAPI do usuário), **não é
  exportada por nenhum fluxo do repositório** (`Export-Certificate` emite só o
  `.cer` público; não há `Export-PfxCertificate` em lugar nenhum; `.pfx` é
  ignorado pelo git e a CI gera cert efêmero próprio).
- `KeyExportPolicy Exportable` era desnecessário: assinatura local consome a
  chave na store, sem exportar. **Correção**: novos certificados nascem
  `NonExportable` — malware com o contexto do usuário não consegue mais
  exportar a chave para assinar em outro lugar. Certificados existentes
  (307E/DF0B) não foram alterados (regra: não mexer em existentes); se quiser
  restringi-los, o procedimento é criar novo cert e reinstalar os pacotes dev.
- Assinatura em outra máquina: nunca foi um fluxo do projeto — cada máquina
  roda `Prepare-DevelopmentCertificate.ps1` e cria a sua.

### C. Seleção e reutilização — adequada, com um detalhe documentado
A seleção exige subject exato + `HasPrivateKey` + válida + EKU de code signing
(1.3.6.1.5.5.7.3.3) e pega a mais recente. Não seleciona cert de outra
finalidade com essas quatro condições combinadas. Detalhe: não verifica cadeia
de confiança — irrelevante aqui, pois a confiança da máquina é instalada por
outro passo a partir do mesmo certificado exportado.

### D. Exportação — correta
`Export-Certificate` exporta **apenas a parte pública** (não há caminho para
PFX no script). O diretório de destino é criado se faltar; o arquivo de saída é
sobrescrito sem prompt (comportamento aceitável para um nome com GUID/temp).

## 3. Install-ReleasePackage.ps1

### A. Elevação — necessária, agora com resultado propagado
- A importação em `LocalMachine\*` exige admin; a remoção de pacote do usuário
  não. A elevação única no início cobre as duas operações com uma janela de UAC
  (menos prompts que elevar por operação).
- **Correção (P2)**: antes, o exit code do processo elevado era descartado
  (`Start-Process -Wait` sem `-PassThru`) — falha elevada aparecia como
  sucesso. Agora: `-PassThru` + `exit $elevated.ExitCode`, e o cancelamento do
  UAC é capturado com mensagem clara ("Instalação cancelada").

### B. Stores — `Root` removido com prova; `TrustedPeople` mantido
- **Experimento E1 (elevado, 02/11)**: com o cert dev 307E presente em
  `TrustedPeople` e **removido do `Root`**, o MSIX self-contained instalou
  normalmente (status Ok, SignatureKind Developer). **Prova: `TrustedPeople`
  basta para o MSIX self-signed; o import em `Root` não é necessário.**
- **Por que existia**: receita antiga de sideload (era Windows 8.1/10 inicial)
  importava em ambos por segurança; no Win10 19045+ com sideloading habilitado,
  `TrustedPeople` cobre a validação de assinatura de pacote.
- **Motivo da correção ser de segurança real**: o store `Root` é a lista de
  autoridades certificadora da máquina inteira. Importar o cert efêmero de cada
  release como **raiz permanente** fazia cada instalação acumular uma âncora de
  confiança (constatado: 2 certs `CN=Slinky Release` acumulados em
  `LocalMachine\Root` desta máquina). Qualquer certificado futuro assinado por
  essas chaves seria confiável para a máquina em outros contextos.
- **Compatibilidade**: instalações existentes não são afetadas (a confiança já
  estabelecida permanece); novas instalações passam a confiar só via
  `TrustedPeople`. A validação em cadeia não é necessária porque o cert é
  autoassinado e a âncora é ele mesmo.

### C. Remoção de versões anteriores — corrigida
- **Correção (P1)**: a remoção anterior casava `DisplayName -eq 'Slinky'` —
  qualquer aplicativo de terceiro chamado "Slinky" seria removido. Agora
  remove somente pela identidade do manifesto
  (`Name -eq '46cebc95-b2f1-4e6a-819f-0227204d1c62'`), que é única do Slinky
  (dev e release compartilham o `Name`; o publisher muda).
- A remoção prévia **continua necessária**: `-ForceUpdateFromAnyVersion` não
  resolve reinstalação de versão idêntica com conteúdo diferente (0x80073CFB,
  observado e documentado). `-ForceApplicationShutdown` permanece (fecha o app
  em execução para o replace).

### D. Instalação — corrigida (validação pacote↔certificado)
- **Correção (P3)**: antes, o script confiava em qualquer `.cer` fornecido e
  instalava o bundle ao lado — um par divergente (bundle assinado por A, cert
  de B) resultaria em 0x800B0109 depois de poluir os stores. Agora:
  `Get-AuthenticodeSignature` no bundle + comparação do thumbprint do
  signatário com o `.cer` **antes** de importar; divergência aborta sem tocar
  nos stores. Validado: `Get-AuthenticodeSignature` extrai signatário e
  thumbprint de `.msixbundle` (testado nos bundles dev e de release).

## 4. Repositório público

- **Nenhum `.cer`/`.pfx` versionado** — confirmado por `git ls-files`. `.pfx`
  e `.cer` já ignorados.
- **Correção (P7)**: `artifacts/release-1.0.0/` estava **versionado** com
  artefatos de publicação antigos (incluindo 2 `.cer` públicos da v1.0.0).
  Removidos do índice (`git rm --cached`; arquivos permanecem no disco) e
  `artifacts/` adicionado ao `.gitignore`.
- O asset `Slinky-Development.cer` das releases era o cert **de release**
  (`CN=Slinky Release`) com nome de desenvolvimento — **corrigido** para
  `Slinky-Release.cer` no `release.yml` e no instalador (releases a partir da
  v1.0.6; a v1.0.5 mantém o nome antigo, anotado no README).
- `scripts/Slinky-Development.cer` no disco local (export meu durante testes,
  cert público `CN=Alessandro`) colidia com o nome esperado pelo instalador de
  release — removido do disco (público e regenerável).

## 5. Compatibilidade e migração

- Instalações existentes (famílias `…_1xsa4dc8b90qj` dev e `…_b2zjezss9gtqm`
  release) mantêm a confiança já estabelecida; nada foi removido dos stores
  (o experimento removeu e **restaurou** o 307E do `Root`).
- Releases a partir da v1.0.6 publicam `Slinky-Release.cer`; usuários da
  v1.0.5 seguem com `Slinky-Development.cer` documentado no README da época.
- O Subject `CN=Alessandro` permanece; nenhuma família nova foi criada.

## 6. Tabela de findings

| Problema | Arquivo | Risco | Classificação | Solução |
|---|---|---|---|---|
| P1: remoção por `DisplayName` pode pegar app de terceiro | Install-ReleasePackage.ps1 | médio | confirmado → corrigido | remover só pela identidade `Name` |
| P2: exit code elevado descartado; UAC cancelado sem tratamento | Install-ReleasePackage.ps1 | médio | confirmado → corrigido | `-PassThru` + `exit` + catch de cancelamento |
| P3: `.cer` confiado sem validar ser o signatário do pacote | Install-ReleasePackage.ps1 | alto | confirmado → corrigido | `Get-AuthenticodeSignature` + comparação de thumbprint antes de importar |
| P4: asset/cert de release com nome de desenvolvimento | release.yml | médio (confusão de confiança) | confirmado → corrigido | renomeado para `Slinky-Release.cer` |
| P5: chave privada dev Exportável sem necessidade | Prepare-DevelopmentCertificate.ps1 | médio-baixo | confirmado → corrigido (novos certificados) | `KeyExportPolicy NonExportable` |
| P6: 5 jobs da CI sem `permissions` (CodeQL ×5) | tests.yml | médio (supply chain) | confirmado → corrigido | `permissions: contents: read` no workflow |
| P7: `artifacts/release-1.0.0/` versionado no repo público | repo | baixo (higiene) | confirmado → corrigido | `git rm --cached` + `.gitignore` |
| P8: import em `LocalMachine\Root` desnecessário | Install-ReleasePackage.ps1, instalar-slinky.bat | alto (acúmulo de âncoras de confiança) | confirmado → corrigido no instalador de release | somente `TrustedPeople`; experimento E1 anexado |
| P9: stray `scripts/Slinky-Development.cer` (dev) colidia com o nome esperado pelo instalador de release | disco local | baixo | confirmado → corrigido | arquivo público removido do disco |
| — Subject pessoal `CN=Alessandro` | Prepare | nenhum (público no manifesto) | configuração necessária | mantido (mudança criaria 3ª família sem ganho) |
| — `Export-Certificate` emite só o público | Prepare | nenhum | configuração necessária | mantido |
| — Elevação admin | Install-ReleasePackage.ps1 | nenhum (import em LocalMachine exige) | configuração necessária | mantida |
| — Remoção prévia por identidade | Install-ReleasePackage.ps1 | nenhum (0x80073CFB provado) | configuração necessária | mantida |
| — `ForceApplicationShutdown`/`ForceUpdateFromAnyVersion` | Install-ReleasePackage.ps1 | nenhum | configuração necessária | mantidos |

## 7. Validação executada

- `Get-AuthenticodeSignature` extrai signatário/thumbprint de `.msixbundle` (dev
  e release) — base da validação P3.
- Experimento E1 elevado (UAC aprovado): `TrustedPeople` sozinho instala o MSIX
  self-signed — prova do P8; estado dos stores restaurado ao final.
- Instalador corrigido validado ponta a ponta (ver seção de execução no
  relatório da conversa): reinstall real da v1.0.5 com validação de
  pacote↔certificado, remoção por identidade e TrustedPeople-only.
- Sintaxe dos scripts: `PSParser` sem erros após as edições.

## 8. Riscos residuais

- Instalações antigas deixaram certs efêmeros `CN=Slinky Release` em
  `LocalMachine\Root` (2 nesta máquina) — remover é decisão do usuário
  (`certmgr.msc`), não feito automaticamente.
- O caminho sem elevação do `instalar-slinky.bat` usa
  `Cert:\CurrentUser\TrustedPeople` (não testado neste ciclo; fluxo alternativo
  documentado).
- `Cert:\LocalMachine\Root` da máquina ainda contém os 2 certs efêmeros — ver
  risco residual acima.
