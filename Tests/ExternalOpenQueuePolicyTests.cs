using Slinky;
using Xunit;

namespace Slinky.Tests
{
    public class ExternalOpenQueuePolicyTests
    {
        [Fact]
        public void Merge_SobreJsonVazio_CriaListaComUmItem()
        {
            var json = ExternalOpenQueuePolicy.Merge(null, "C:/a.txt");

            Assert.Equal("[\"C:/a.txt\"]", json);
        }

        [Fact]
        public void Merge_SobreJsonInvalido_DescartaEAdiciona()
        {
            var json = ExternalOpenQueuePolicy.Merge("isto nao e json {", "C:/a.txt");

            Assert.Equal("[\"C:/a.txt\"]", json);
        }

        [Fact]
        public void Merge_AcumulaVariosCaminhos()
        {
            var json = ExternalOpenQueuePolicy.Merge(null, "C:/a.txt");
            json = ExternalOpenQueuePolicy.Merge(json, "C:/b.txt");
            json = ExternalOpenQueuePolicy.Merge(json, "C:/c.txt");

            Assert.Equal("[\"C:/a.txt\",\"C:/b.txt\",\"C:/c.txt\"]", json);
        }

        [Fact]
        public void TakeAll_PreservaOrdemDeChegada()
        {
            var lista = ExternalOpenQueuePolicy.TakeAll("[\"C:/a.txt\",\"C:/b.txt\"]");

            Assert.Equal(new[] { "C:/a.txt", "C:/b.txt" }, lista);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("json quebrado [")]
        public void TakeAll_EntradaAusenteOuInvalida_RetornaListaVazia(string? json)
        {
            Assert.Empty(ExternalOpenQueuePolicy.TakeAll(json));
        }
    }
}
