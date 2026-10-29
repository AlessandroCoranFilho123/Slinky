# Release notes — Slinky

Este arquivo é a fonte única das notas de release. O workflow `release.yml` o usa
como `body_path` (com `generate_release_notes: true`, que anexa o changelog
automático). **Atualize a seção da nova release aqui antes de criar a tag.**

## v1.0.4 (em preparação)

**Novidades**

- **Recuperação reconectada ao arquivo original**: notas recuperadas após um
  encerramento inesperado voltam vinculadas ao arquivo (aba com o nome do arquivo;
  `Ctrl+S` grava no documento original, sem "Salvar como"). Tokens da
  FutureAccessList são capturados nos snapshots; token expirado/ausente mantém o
  comportamento anterior (nota sem título).
- Comportamentos alinhados ao Bloco de Notas clássico (decisões documentadas no
  §15 da auditoria): flag de modificado "pegajoso" após undo, sobrescrita
  silenciosa de arquivo alterado externamente e Windows-1252 com round-trip oficial.

**Melhorias**

- Pacote usa referências granulares do Windows App SDK (sem payloads de IA/ML/Widgets)
  e apenas idiomas en/pt-BR: ~6 MB menos instalados.
- Relatório completo de desempenho e tamanho em `docs/DESEMPENHO-E-TAMANHO.md`
  (CPU ociosa 0%, ~51 MB privados em repouso, partida aquecida ~0,4 s, piso do
  framework medido com app vazio).

**Correções**

- Indicador de foco por teclado visível nos cinco temas (era sobrescrito pelos
  visual states da `TextBox`).
- `Ctrl+W` com o diálogo de confirmação aberto não abre um segundo diálogo
  (COMException 0x80000019); atalhos `Ctrl+S`/`Ctrl+Shift+S`/`Ctrl+W` aguardam o
  diálogo fechar.

## v1.0.3

- Auditoria de desempenho e tamanho; pacotes granulares do Windows App SDK;
  correção do indicador de foco e do diálogo duplo; smoke UI completo (28–29/10)
  com 20+ cenários validados; instalação MSIX com causas raiz documentadas
  (`docs/INSTALACAO-MSIX.md`).

## v1.0.2

- Instalador de release com assets resolvidos ao lado do script; certificado de
  release instalado nas stores de máquina.

## v1.0.1

- Certificado de release nas stores de máquina (correção de 0x800B0109).

## v1.0.0

- Primeira release pública: editor WinUI 3 com abas, cinco temas WCAG AA,
  busca/substituição, impressão e recovery.
