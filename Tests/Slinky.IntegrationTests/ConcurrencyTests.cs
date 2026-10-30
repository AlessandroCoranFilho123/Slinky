using Slinky;
using Slinky.Services;
using Windows.Storage;
using Xunit;

namespace Slinky.IntegrationTests
{
    /// <summary>
    /// Testes de integração das corridas R1/R2 (RACE-AUDIT-2026-10-30): serviços de
    /// verdade, pasta de estado real e isolada por teste (nunca dados do usuário),
    /// operações na ordem exata da corrida — sem sleeps.
    /// Uma única classe: o provider de pasta é estático e os testes o redefinem.
    /// </summary>
    [Trait("Category", "Integration")]
    public class ConcurrencyTests : IDisposable
    {
        private readonly string _directory;

        public ConcurrencyTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "slinky-it-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(_directory);
            StateFolders.LocalFolderProvider = () =>
                StorageFolder.GetFolderFromPathAsync(_directory).AsTask();
        }

        public void Dispose()
        {
            StateFolders.LocalFolderProvider = null; // restaura o padrão (ApplicationData)
            try { Directory.Delete(_directory, recursive: true); } catch { }
        }

        private static RecoverySnapshot Snapshot(string text) => new()
        {
            OwnerProcessId = Environment.ProcessId,
            Text = text,
            SavedAt = DateTime.Now
        };

        private static string SnapshotPath(string directory, Guid id) =>
            Path.Combine(directory, "Recovery", $"{id}.json");

        [Fact]
        public async Task SaveThenDeleteThenStaleSave_LeavesNoSnapshot_R1()
        {
            var id = Guid.NewGuid();

            // tick em voo captura a geração 1 e grava
            await RecoveryService.SaveSnapshotAsync(id, Snapshot("texto rascunho"), generation: 1);
            Assert.True(File.Exists(SnapshotPath(_directory, id)));

            // usuário salva: ClearRecoveryAsync incrementa e apaga
            await RecoveryService.DeleteSnapshotAsync(id, generation: 2);
            Assert.False(File.Exists(SnapshotPath(_directory, id)));

            // o tick em voo conclui DEPOIS com a geração antiga: precisa ser recusado
            await RecoveryService.SaveSnapshotAsync(id, Snapshot("texto rascunho"), generation: 1);

            Assert.False(File.Exists(SnapshotPath(_directory, id)), "snapshot obsoleto ressuscitado após o salvar (R1)");
            Assert.Empty(Directory.GetFiles(Path.Combine(_directory, "Recovery"), "*.tmp"));
        }

        [Fact]
        public async Task StaleSaveThenFreshTick_WritesNormally()
        {
            var id = Guid.NewGuid();

            await RecoveryService.SaveSnapshotAsync(id, Snapshot("rascunho"), generation: 1);
            await RecoveryService.DeleteSnapshotAsync(id, generation: 2);
            await RecoveryService.SaveSnapshotAsync(id, Snapshot("rascunho"), generation: 1); // recusado

            // o próximo tick, já com a geração do pós-salvar, grava normalmente
            await RecoveryService.SaveSnapshotAsync(id, Snapshot("novo rascunho"), generation: 2);

            var content = File.ReadAllText(SnapshotPath(_directory, id));
            Assert.Contains("novo rascunho", content);
        }

        [Fact]
        public async Task DeleteWithNewerGenerationNeverInvalidatedByOlderDelete()
        {
            var id = Guid.NewGuid();

            await RecoveryService.SaveSnapshotAsync(id, Snapshot("a"), generation: 5);
            await RecoveryService.DeleteSnapshotAsync(id, generation: 6);   // exclusão confirmada
            await RecoveryService.DeleteSnapshotAsync(id, generation: 3);   // exclusão antiga não rebaixa a marca

            await RecoveryService.SaveSnapshotAsync(id, Snapshot("b"), generation: 6);
            Assert.True(File.Exists(SnapshotPath(_directory, id)));
        }

        [Fact]
        public async Task DeleteTwiceIsIdempotent()
        {
            var id = Guid.NewGuid();
            await RecoveryService.SaveSnapshotAsync(id, Snapshot("a"), generation: 1);
            await RecoveryService.DeleteSnapshotAsync(id, generation: 2);
            await RecoveryService.DeleteSnapshotAsync(id, generation: 2);

            Assert.False(File.Exists(SnapshotPath(_directory, id)));
        }

        [Fact]
        public async Task ConcurrentSavesFromTwoWrites_LastWriteWins_NoTempLeft()
        {
            // R2: dois Ctrl+S intercalados — gravações simultâneas do MESMO conteúdo;
            // resultado determinístico: um arquivo válido e nenhum temporário.
            var id = Guid.NewGuid();
            var folder = Path.Combine(_directory, "Recovery");

            var primeira = RecoveryService.SaveSnapshotAsync(id, Snapshot("conteudo igual"), generation: 1);
            var segunda = RecoveryService.SaveSnapshotAsync(id, Snapshot("conteudo igual"), generation: 1);
            await Task.WhenAll(primeira, segunda);

            Assert.True(File.Exists(SnapshotPath(_directory, id)));
            Assert.Contains("conteudo igual", File.ReadAllText(SnapshotPath(_directory, id)));
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }

        [Fact]
        public async Task SessionSaveAndLoad_ConsumesSessionAndCleansTmp()
        {
            // F4: a sessão grava tmp + Move (atômico); a leitura consome somente
            // sessões de processos mortos — simulado renomeando o arquivo para um
            // PID inexistente (o do teste está vivo de propósito). O token do FAL
            // não existe fora de processo empacotado, então a leitura resolve
            // "arquivo não encontrado" e retorna vazia — o comportamento esperado
            // para token morto; o que se valida é o consumo e a limpeza.
            var folder = await StorageFolder.GetFolderFromPathAsync(_directory);
            var file = await folder.CreateFileAsync("entrada-teste.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, "x");

            await SessionService.SaveOpenFilesAsync(new[] { file! });
            Assert.Single(Directory.GetFiles(_directory, "session-*.json"));
            Assert.Empty(Directory.GetFiles(_directory, "session-*.json.tmp"));

            var vivo = Path.Combine(_directory, $"session-{Environment.ProcessId}.json");
            var morto = Path.Combine(_directory, "session-999999.json");
            File.Move(vivo, morto);

            var lidos = await SessionService.LoadAndClearAsync();

            Assert.Empty(lidos); // token FAL indisponível no processo de teste
            Assert.False(File.Exists(morto)); // sessão consumida
            Assert.Empty(Directory.GetFiles(_directory, "session-*.json"));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }
    }
}
