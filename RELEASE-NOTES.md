# Release notes — Slinky

Este arquivo é a fonte única das notas de release. O workflow `release.yml` o usa
como `body_path` (com `generate_release_notes: false` — a action v2 substituía o
corpo curado pelo changelog automático). **Atualize a seção da nova release aqui
antes de criar a tag.**

## v1.0.6

**Novidades**

- **Refazer (Ctrl+Y)** no menu Editar.
- **Abrir arquivos por linha de comando**: `Slinky.exe "caminho\arquivo.txt"` abre o
  documento na janela (com foco na aba se já estiver aberto).
- **Drag & drop**: arraste um arquivo para a janela para abri-lo em nova aba.
- **Título da janela** acompanha a aba ativa (nome do arquivo + marcador de não salvo).
- Editor sem correção automática/previsão: um editor de texto não reescreve o
  texto do usuário silenciosamente.
- `crash.log` rotaciona automaticamente acima de 512 KB (`crash-prev.log`).
- Dependabot semanal para NuGet e GitHub Actions.

**Notas**

- A associação de arquivos por duplo clique (abrir .txt do Explorer) segue
  pendente: o empacotador self-contained do Windows App SDK reescreve o
  manifesto e descarta extensões `uap:` (validação de schema falha no
  MakeAppx) — investigação de empacotamento registrada.

## v1.0.5

**Migração de runtime**

- **.NET 10** em todos os projetos (app, testes headless e de integração) e na CI —
  feito antes do fim do suporte do .NET 8 (10/11/2026). O pacote self-contained
  agora embarca o runtime .NET 10; Windows App SDK 1.8 permanece.
- `PublishReadyToRun` desligado por padrão no projeto (a decisão de envio já era
  non-R2R; com .NET 10 um build R2R sem RID explícito falha no local).

**Confiabilidade (auditoria de concorrência)**

- **Corrida R1/R2**: um autosave em voo podia ressuscitar o snapshot de recovery
  depois de o usuário salvar/fechar/excluir a nota — o próximo launch oferecia
  texto obsoleto como "não salvo". Snapshots agora carregam uma geração e escritas
  obsoletas são recusadas e limpas.
- **Corrida R5**: arquivos temporários órfãos (processo morto no meio de uma
  gravação) agora são removidos no startup.
- Salvar é **atômico** (temporário + replace): um encerramento no meio da gravação
  nunca mais trunca o arquivo.
- Serialização do recovery fora da UI thread (salvar 50 MB: 2–3 min → ~14 s).

**Usabilidade (auditoria externa + QA)**

- "Substituir tudo" preserva o histórico de desfazer (Ctrl+Z volta ao original).
- "Maiúsc/minúsc" visível também no Ctrl+F; Enter/Esc funcionam no campo de substituição.
- Abrir um arquivo já aberto foca a aba existente em vez de duplicar.
- Todos os diálogos seguem o tema ativo; menu de contexto do editor em pt-BR;
  hover dos botões da barra de título com contraste correto em tema claro e escuro.
- "Excluir arquivo" passa pela confirmação de alterações não salvas.
- Recuperação reconectada ao arquivo original (notas voltam vinculadas ao documento).

**Qualidade**

- Novo projeto de testes de integração (`Slinky.IntegrationTests`): 8 testes
  determinísticos de corrida/sessão/recovery contra pasta de estado real isolada
  (76 testes headless + 8 de integração, todos verdes no .NET 10).
- Auditoria de codificações, arquivos grandes, stress e segurança registrada em
  `docs/QA-2026-10-30.md`.

## v1.0.4

- Auditoria de desempenho, memória e tamanho; pacotes granulares do Windows App SDK
  (sem payloads de IA/ML/Widgets, ~6 MB a menos); correção do indicador de foco e do
  diálogo duplo; smoke UI completo automatizado.

## v1.0.3

- Correção do indicador de foco por teclado (era sobrescrito pelos visual states) e
  do diálogo duplo; smoke UI completo (28–29/10) com 20+ cenários validados;
  instalação MSIX com causas raiz documentadas (`docs/INSTALACAO-MSIX.md`).

## v1.0.2

- Instalador de release com assets resolvidos ao lado do script; certificado de
  release instalado nas stores de máquina.

## v1.0.1

- Certificado de release nas stores de máquina (correção de 0x800B0109).

## v1.0.0

- Primeira release pública: editor WinUI 3 com abas, cinco temas WCAG AA,
