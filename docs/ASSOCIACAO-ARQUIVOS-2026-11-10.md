# Associação de arquivos (.txt) — causa raiz e correção (10/11/2026)

A tentativa anterior de adicionar `windows.fileTypeAssociation` ao manifesto foi
revertida com um erro de validação MakeAppx (C00CE014) e a hipótese registrada
foi "o empacotador self-contained descarta extensões `uap:`". Essa hipótese era
**incorreta** — a investigação de 10/11 reproduziu o erro isolado do WinAppSDK e
achou a causa real.

## Causa raiz (duas falhas encadeadas)

### 1. Namespace errado do contêiner (C00CE014)

O schema do manifesto **não tem** `<uap:Extensions>` como filho de
`<Application>` — o contêiner nesse nível é `<Extensions>` do namespace
**foundation** (sem prefixo), e os filhos são `<uap:Extension>`:

```xml
<Application Id="App" ...>
  <uap:VisualElements .../>
  <Extensions>
    <uap:Extension Category="windows.fileTypeAssociation">
      <uap:FileTypeAssociation Name="textfile">
        <uap:SupportedFileTypes>
          <uap:FileType>.txt</uap:FileType>
        </uap:SupportedFileTypes>
      </uap:FileTypeAssociation>
    </uap:Extension>
  </Extensions>
</Application>
```

A posição (antes/depois de `VisualElements`) é indiferente — bissecado com
MakeAppx 10.0.26100 (pacote `Microsoft.Windows.SDK.BuildTools`) nas duas ordens:
ambas falham com `uap:Extensions`; com `Extensions` (foundation) o schema passa.
O erro C00CE014 ("elemento uap:Extensions não é esperado de acordo com o modelo
de conteúdo de Application") descrevia exatamente isso — o "descarte pelo
empacotador" nunca existiu.

### 2. Entrega do arquivo não é pela linha de comando

Com o pacote instalado, a ativação pelo shell (duplo clique / "Abrir com")
**não** coloca o caminho do arquivo em `args.Arguments` — o `LaunchArgs`
(que cobre o lançamento explícito `Slinky.exe arquivo.txt`) não vê nada e o
app abria "Sem título". O arquivo chega pelo contrato de ativação empacotada:
`FileActivatedEventArgs` via AppLifecycle.

## Correção

- `Package.appxmanifest`: contêiner `<Extensions>` (foundation) com
  `<uap:Extension Category="windows.fileTypeAssociation">` para `.txt`.
- `App.OnLaunched`: além do parsing de argumentos, consulta
  `Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs()`
  e, se `Data` for `FileActivatedEventArgs`, extrai o primeiro caminho existente
  (`LaunchArgs.ExtractFirstExisting`, função pura testada). Sem comparação de
  `Kind`: a projeção do SDK 19041 não traz `ExtendedActivationKind`, e o cast
  do `Data` já caracteriza a ativação por arquivo.
- `Slinky.csproj`: os pacotes granulares do WinAppSDK projetam só o metadata do
  WinUI — a referência de compilação a
  `Microsoft.Windows.AppLifecycle.Projection.dll` (pacote Foundation) foi
  adicionada explicitamente; manter a versão em sincronia com os pacotes acima.

## Evidência (10/11/2026)

- Build MSIX self-contained 1.0.6.0 assinado: verde; o `AppxManifest.xml` dentro
  do `.msix` x64 contém a associação (os pacotes de recurso `scale-*` não a
  carregam, comportamento normal).
- Instalação na máquina dev (família `1xsa4dc8b90qj`): o shell registrou o ProgId
  `AppXhawrr43jch19gtams7yqgzdz6pqmnds1` (ApplicationName = Slinky,
  `DelegateExecute` de ativação empacotada) em `HKCU\SOFTWARE\Classes\.txt\OpenWithProgids`.
- E2E automatizado: `ShellExecuteEx` com `SEE_MASK_CLASSNAME` + ProgId do Slinky
  sobre um `.txt` de teste (caminho equivalente ao duplo clique) → app ativado e
  título da janela `slinky-fua-teste.txt - Slinky` (aba aberta + título seguindo
  a aba ativa).
- Regressão: 86 testes headless + 8 de integração verdes; CodeQL sem alertas.
- Duplo clique manual no Explorer segue como item do smoke visual
  (`docs/SMOKE-UI.md`) — o E2E acima usa a mesma rota de shell.
