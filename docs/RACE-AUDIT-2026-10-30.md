# Auditoria de race conditions — Slinky (30/10/2026)

Diagnóstico completo de concorrência sobre a master (`24cdd14`). **Nenhuma correção
aplicada ainda** — este documento é o pedido de autorização. Cada item indica:
arquivo/método, fluxo atual, o que acontece num conflito, gravidade, probabilidade
e se o problema é real ou teórico.

## Modelo de concorrência do Slinky (contexto)

- **Uma UI thread por processo**: todo o estado do documento (Note, NotePage,
  TabView, diálogos, flags `contentDialogOpen`/`closePromptOpen`/`startupRestorationInProgress`)
  só é tocado na UI thread (DispatcherTimer, eventos, handlers async void que
  continuam no contexto da UI). Não há races de memória entre threads dentro de
  um processo nesses pontos.
- **Operações de E/S assíncronas** (`FileIO`, `WriteBytesSafeAsync`): o dado é
  capturado na UI thread antes do primeiro `await`; depois rodam fora da thread.
  As corridas reais estão **no agendamento e conclusão dessas E/S**, não na
  memória.
- **Multi-instância**: uma instância por processo; estado por PID
  (`session-{PID}.json`, snapshots com `OwnerProcessId`); FAL e `LocalSettings`
  são compartilhados; arquivos do usuário são compartilhados (política
  last-write-wins deliberada, §15-3).

## Corridas confirmadas (reais)

### R1 — Autosave em voo ressuscita snapshot após salvar/fechar a nota
- **Onde**: `Note.AutosaveTimer_Tick` → `RecoveryService.SaveSnapshotAsync`
  (Serialize off-thread → tmp → `MoveAsync` replace) vs `Note.ClearRecoveryAsync`
  → `DeleteSnapshotAsync` (chamado após salvar com sucesso, após fechar aba e
  após excluir arquivo).
- **Fluxo atual**: o tick dispara (2 s após a última edição), captura o texto,
  serializa (fora da thread — pode levar de ms a segundos em docs grandes),
  grava o `.tmp` e faz o `MoveAsync`. Em paralelo, o usuário salva (`Ctrl+S` →
  `SaveAsync` → `ClearRecoveryAsync` → `DeleteSnapshotAsync`).
- **Conflito**: se o `DeleteSnapshotAsync` executar **entre** a gravação do `.tmp`
  e o `MoveAsync` do autosave, o `MoveAsync` **recria o snapshot depois da
  exclusão** — um snapshot com texto **anterior ao salvamento** fica no disco
  para uma nota que acabou de ser salva (ou fechada). No próximo launch, o
  prompt de recovery oferece texto obsoleto — e, com a reconexão, reanexa ao
  arquivo como se houvesse trabalho não salvo.
- **Gravidade**: média-baixa (confusão, não perda — o arquivo em disco está
  íntegro). **Probabilidade**: baixa mas real — a janela é a duração da
  serialização+escrita (maior em documentos grandes; minutos em 100 MB).
- **Mesma corrida na exclusão**: excluir arquivo → confirmar → entre a exclusão
  e o `ClearRecoveryAsync` da remoção da aba, um tick em voo recria o snapshot
  de uma nota cujo arquivo foi apagado.

### R2 — Nota excluída vs autosave em voo (variante da R1)
- Mesmo par de métodos; o snapshot órfão de uma nota excluída é oferecido no
  próximo launch com o texto pré-exclusão. Coberto pela mesma correção.

## Riscos potenciais (teóricos ou benignos — documentar, não corrigir)

| Ponto | Análise | Veredito |
|---|---|---|
| Duplo `Ctrl+S` na mesma nota | Dois `SaveAsync` intercalam: ambos codificam o mesmo texto lido na UI thread, gravam `.tmp` GUID distintos e `MoveAndReplace` — o último vence com bytes idênticos. Idempotente. | Benigno — documentado |
| `RefreshSessionAsync` fire-and-forget (`_ =` em 386/405/443) | `startupRestorationInProgress` gateia os do startup (384/403); `SaveOpenFilesAsync` reconstrói a lista do estado vigente sob `Gate` — auto-consistente. Corrida residual: usuário abrir manualmente um arquivo que o restore está prestes a abrir → sessão pode registrar os dois por um ciclo | Teórico, baixo — o restore deduplica na leitura (`SessionRestorePolicy`) |
| `RecentFilesService.AddAsync` fire-and-forget | `SemaphoreSlim` cobre o read-modify-write completo (M12 corrigido) | Protegido |
| Tema em duas instâncias | `LocalSettings` é gravação atômica por valor (last-write-wins); instâncias vivas não sincronizam em tempo real | Limitação conhecida (C3) — documentada; correção exigiria IPC |
| Duas instâncias salvando o mesmo arquivo | Last-write-wins, sem lock — política deliberada do §15-3 (paridade Notepad) | Deliberado |
| App encerrando com E/S em voo | `Exit()`/fechamento mata `WriteBytesSafeAsync`/autosave no meio: o alvo é íntegro **porque** a gravação é atômica; resta `.tmp` órfão (`*.slinky-tmp`, `session-*.json.tmp`) | Baixo — dado íntegro; limpeza de órfãos no startup é melhoria trivial |
| `ContentDialog` flag `contentDialogOpen` | Só UI thread | Sem corrida |
| Busca/substituição durante autosave | Ambos na UI thread; autosave só lê `Text` | Sem corrida |
| `FindLeftoverSnapshotsAsync` lendo enquanto outra instância grava | Snapshots são por PID; `IsOwnedByLiveProcess` filtra vivos; tmp com GUID + `MoveAsync` | Protegido por design |
| `Task.Run` do serialize tocando UI | Não — serializa POCO puro | Sem corrida |

## Estratégia de correção proposta (aguardando autorização)

### C-R1/R2 — Geração de snapshot (simples, sem locks novos)
`Note` ganha um contador `snapshotGeneration` (incrementado em
`ClearRecoveryAsync` e a cada `SaveAsync` bem-sucedido). O tick captura a geração
**antes** de `SaveSnapshotAsync` e compara **depois**: se mudou, apaga o snapshot
acabado de escrever. Determinístico, ~15 linhas em `Note.cs`, sem tocar
`RecoveryService` (o Gate já serializa os writes no disco).

### C-R5 — Limpeza de temporários órfãos no startup
`RecoveryService.FindLeftoverSnapshotsAsync` e `SessionService.LoadAndClearAsync`
já varrem as pastas: acrescentar a remoção de `*.tmp` órfãos (recovery e sessão).
~10 linhas, baixo risco.

### C-Testes — por que a injeção da pasta de estado entra AQUI
Os testes determinísticos de R1/R4 exigem `RecoveryService` gravando numa pasta
controlada (`ApplicationData.Current` é inacessível headless; UIA não reproduz
corridas de forma determinística — comprovado nas baterias). É o critério que
você fixou: **necessário e com melhoria grande** — habilita os testes de
integração das corridas E dos fluxos F1–F5 na camada headless. Proposta: extrair
`IStateFolderProvider` (pasta injetada; produção usa `ApplicationData.Current`),
~40 linhas de refactor mecânico em `RecoveryService`/`SessionService`/
`RecentFilesService`, sem mudança de comportamento. **Decisão sua.**

## Arquivos que precisarão ser modificados (com autorização)

1. `Slinky/Models/Note.cs` — geração de snapshot (C-R1/R2).
2. `Slinky/RecoveryService.cs` — limpeza de `*.tmp` (C-R5); injeção de pasta (C-Testes).
3. `Slinky/SessionService.cs` — limpeza de tmp (C-R5); injeção de pasta (C-Testes).
4. `Slinky/RecentFilesService.cs` — injeção de pasta (C-Testes).
5. `Tests/ConcurrencyTests.cs` (novo) — R1/R4 determinísticos: salvar durante
   autosave em voo; excluir durante autosave em voo; snapshot órfão ignorado.
6. `Tests/PersistenceTests.cs` — ajustes se a assinatura dos serviços mudar.

## Testes necessários (após autorização)

- **R1**: nota suja → autosave em voo (serialize atrasado simulado) → save →
  assert: nenhum snapshot restante.
- **R2**: excluir nota com autosave em voo → assert: nenhum snapshot restante.
- **C-R5**: pasta com `.tmp` órfão → startup → assert: tmp removidos, snapshots válidos preservados.
- Regressão: os 76 testes existentes + round-trip de recovery (F3) reexecutado.
