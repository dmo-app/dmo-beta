using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O gerador do PDF da folha de pesagem (o alvo 22): gera o documento em
/// memória (nunca grava no servidor) — um PDF real (a cabeça %PDF e o fim
/// %%EOF), com a estrutura do alvo (as leituras pela posição e a
/// Comparação_B).
/// </summary>
public sealed class PesoPdfGeneratorTests
{
    private static PesoPdfGenerator.Reading Reading(int pos, string cm, decimal water, decimal capacity, decimal glass) =>
        new(pos, cm, water, capacity, glass);

    [Fact]
    public void GenerateBytes_ProducesARealPdf_InMemory()
    {
        var generator = new PesoPdfGenerator();

        // O exemplo do alvo: as 4 leituras pela posição e o diferencial
        // com o último controlo da mesma referência.
        var bytes = generator.GenerateBytes(new PesoPdfGenerator.Input(
            ProductionNumber: "202602",
            Reference: "1075",
            Machine: "B1",
            CmToolId: "T3b9f709c99",
            CmReference: "ST100",
            CmLot: "8",
            Process: "NNPB",
            WaterTemperatureC: 25,
            WaterDensity: 0.99603m,
            GlassDensity: 2.50m,
            VolumeMarisaCm3: 10m,
            VolumePuncaoCm3: 24.42m,
            NominalWeightGrams: 138m,
            Readings: new[]
            {
                Reading(1, "61", 71.10m, 71.38m, 142.41m),
                Reading(2, "95", 71.40m, 71.68m, 143.16m),
                Reading(3, "63", 71.20m, 71.48m, 142.66m),
                Reading(4, "36", 71.80m, 72.09m, 144.17m)
            },
            Averages: new PesoAveragesReadModel(
                AverageWaterWeightGrams: 71.375m,
                AverageCapacityCm3: 71.6575m,
                AverageGlassWeightGrams: 143.10m),
            Comparison: null,
            OutputFilePath: ""));

        Assert.True(bytes.Length > 1000, "o PDF tem corpo");
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        var tail = System.Text.Encoding.ASCII.GetString(bytes[^32..]);
        Assert.Contains("%%EOF", tail);

        // O que está escrito: a produção, as leituras por CM (com a massa de
        // água, numa só tabela desde 20/10/2026), a média, as
        // referências técnicas e a diferença para o nominal.
        var text = PdfText.Of(bytes);
        Assert.Contains("1075 · 202602 · B1", text);
        Assert.Contains("61 71.10 71.380 142.41", text);
        Assert.Contains("36 71.80 72.090 144.17", text);
        Assert.Contains("Média 71.38 71.658 143.10", text);
        Assert.Contains("NNPB ST100 · 8 25 °C", text);
        Assert.Contains("138.00 g", text);
        Assert.Contains("+5.10 g · +3.70 %", text);
        Assert.Contains("Sem comparação", text);
    }

    [Fact]
    public void GenerateBytes_WithoutComparison_StillProducesTheSheet()
    {
        // O primeiro controlo de uma referência não tem comparação: a
        // folha imprime as leituras e as médias sem o bloco do diferencial.
        var bytes = new PesoPdfGenerator().GenerateBytes(new PesoPdfGenerator.Input(
            "202601", "5447T173", "B1", "T0", null, null, "PS", 25, 0.99603m, 2.52m,
            10m, 24.42m, 125m,
            new[] { Reading(1, "12", 71.8m, 72.09m, 145.0m) },
            null,
            null,
            ""));

        Assert.True(bytes.Length > 1000);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        var text = PdfText.Of(bytes);
        Assert.Contains("5447T173 · 202601 · B1", text);
        Assert.Contains("12 71.80 72.090 145.00", text);
    }

    [Fact]
    public void GenerateBytes_ShowsTheGlassDensityWithAllItsDecimals()
    {
        // A densidade do vidro sai como no ecrã (e como a da água): 2,4027
        // não pode aparecer arredondada a 2,40.
        var bytes = new PesoPdfGenerator().GenerateBytes(new PesoPdfGenerator.Input(
            "202601", "5447T173", "B1", "T0", null, null, "PS", 25, 0.99603m, 2.4027m,
            10m, 24.42m, 125m,
            new[] { Reading(1, "12", 71.8m, 72.09m, 145.0m) },
            null,
            null,
            ""));

        var text = PdfText.Of(bytes);
        Assert.Contains("VOLUME DO TAMPÃO 2.4027 10.00", text);
    }
}
