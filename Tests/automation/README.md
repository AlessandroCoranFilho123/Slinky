# Automação de interface — Slinky

Scripts de validação de interface executados contra o **pacote de desenvolvimento
instalado** (família `46cebc95-…_1xsa4dc8b90qj`). Requisitos: Windows com o pacote
instalado (`Add-AppxPackage` — ver `docs/INSTALACAO-MSIX.md`), PowerShell 5.1
(nativo do Windows), resolução com a janela visível.

## Isolamento — o que cada script toca de fato

- **Nenhum script altera documentos do usuário em disco.** Os testes usam arquivos
  criados em `%TEMP%` e descartáveis.
- Antes de cada suíte, o script limpa APENAS o estado do aplicativo
  (`%LOCALAPPDATA%\Packages\…\LocalState\Recovery\*` e `session-*.json`) — snapshots
  de recovery e sessão, que são dados derivados (o que está em aberto e cópias não
  salvas dos testes anteriores). Documentos salvos, recentes reais e configurações
  do usuário não são apagados.
- **Efeito prático**: rodar a suíte descarta conteúdo não salvo que por acaso
  estivesse aberto no Slinky no momento (os próprios scripts fecham as abas antes,
  quando possível). Se você tiver trabalho não salvo no app, salve antes de rodar.

## Scripts

| Script | O que valida | Execução |
|---|---|---|
| `run-all.ps1` | **Comando único**: testes headless (xUnit) + build Release x64 | `powershell -ExecutionPolicy Bypass -File Tests\automation\run-all.ps1` |
| `smoke-e2e.ps1` | Recovery E2E: digitar → autosave → crash → Restaurar → conferir conteúdo (2 ciclos) | **manual** — instável em CI (UIA por sleep + cooldown MSIX) |
| `encodings.ps1` | Round-trip byte-exato de UTF-8/±BOM/UTF-16 LE/BE/1252/emoji/mistos/vazio | **manual** |
| `stress.ps1` | 100 abas, 300 edições/undos, buscas e alternâncias, com amostragem de memória/handles | **manual** |

**Por que os scripts de UI são manuais**: dois problemas conhecidos, nenhum do
aplicativo — (1) a reativação MSIX logo após um `Stop-Process -Force` falha
silenciosamente de forma intermitente (cooldown não determinístico; 2 s e 10 s
ambos já falharam, e o mesmo launch manual funciona em seguida); (2) diálogos
nativos não expõem campos via UIA e a ordem da árvore varia. O fluxo de recovery
em si está validado por diagnósticos controlados e pelos testes de integração
(`Slinky.IntegrationTests`), que executam os mesmos serviços de forma
determinística contra pasta real isolada.
| `run-all.ps1` | Comando único: testes headless + smoke E2E |

Uso: `powershell -ExecutionPolicy Bypass -File Tests\automation\run-all.ps1`
