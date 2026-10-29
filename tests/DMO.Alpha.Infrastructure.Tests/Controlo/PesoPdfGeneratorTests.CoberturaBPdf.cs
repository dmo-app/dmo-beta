using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O PDF do Peso lido pelo texto: a produção anterior com a média e a moda
/// (DMO-426, DMO-534) e o que a limpeza do PDF retirou (DMO-526).
/// </summary>
public sealed class PesoPdfGeneratorCoberturaBTests
{
    private static PesoPdfGenerator.Input Entrada(PesoProducaoAnteriorReadModel? anterior, PesoSapResumo? resumo)
    {
        var input = new PesoPdfGenerator.Input(
            ProductionNumber: "202602", Reference: "1075", Machine: "B1", CmToolId: "T3", CmReference: "ST100", CmLot: "8",
            Process: "NNPB", WaterTemperatureC: 25, WaterDensity: 0.99603m, GlassDensity: 2.50m,
            VolumeMarisaCm3: 10m, VolumePuncaoCm3: 24.42m, NominalWeightGrams: 138m,
            Readings: new[] { new PesoPdfGenerator.Reading(1, "61", 71.10m, 71.38m, 142.41m) },
            Averages: new PesoAveragesReadModel(71.10m, 71.38m, 142.41m),
            Comparison: null, OutputFilePath: "",
            ProducaoAnterior: anterior);
        return resumo is null ? input : input with { PesoProducaoSap = resumo };
    }

    [Fact]
    public void ProducaoAnterior_ComMediaEModa_OPdfDizAsDuasEOsDiasDaModa()
    {
        var anterior = new PesoProducaoAnteriorReadModel(7, "1075", "202601", "B2", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 19), null);
        var text = PdfText.Of(new PesoPdfGenerator().GenerateBytes(
            Entrada(anterior, new PesoSapResumo(MediaGramas: 140.00m, ModaGramas: 138.50m, Dias: 18, Ordens: 3, ModaDias: 12))));

        // Dois cartões da produção anterior, um com a média e outro com a moda.
        Assert.Equal(2, CountOf(text, "Produção anterior · 202601"));
        Assert.Contains("140.00 g", text);
        Assert.Contains("138.50 g", text);
        // Os dias da moda, no contexto técnico.
        Assert.Contains("média 140.00 g · moda 138.50 g (12 de 18 dias)", text);
        // A diferença à média: 142.41 - 140.00 = +2.41 g.
        Assert.Contains("+2.41 g", text);
    }

    [Fact]
    public void ProducaoAnterior_ComMediaEModa_OsCartoesDizemMediaEModa_EAModaMostraOsDiasSemDiferenca()
    {
        var anterior = new PesoProducaoAnteriorReadModel(7, "1075", "202601", "B2", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 19), null);
        var text = PdfText.Of(new PesoPdfGenerator().GenerateBytes(
            Entrada(anterior, new PesoSapResumo(MediaGramas: 140.00m, ModaGramas: 138.50m, Dias: 18, Ordens: 3, ModaDias: 12))));

        Assert.Contains("Produção anterior · 202601 · Média", text);
        Assert.Contains("Produção anterior · 202601 · Moda", text);
        Assert.Contains("12 de 18 dias", text);
        // Só a média tem diferença: 142.41 - 140.00 = +2.41 g; a da moda (+3.91 g) não aparece.
        Assert.Contains("+2.41 g", text);
        Assert.DoesNotContain("+3.91 g", text);
    }

    [Fact]
    public void ProducaoAnterior_SemMediaNemModa_UsaOPesoGuardado_ESemDizerMedia()
    {
        var anterior = new PesoProducaoAnteriorReadModel(7, "1075", "202601", "B2", null, null, 140.5m);
        var text = PdfText.Of(new PesoPdfGenerator().GenerateBytes(Entrada(anterior, null)));

        Assert.Contains("Produção anterior 202601", text);
        Assert.Contains("140.50 g", text);
        Assert.DoesNotContain("moda", text);
        Assert.DoesNotContain("média 140", text);
    }

    [Fact]
    public void PdfDoPeso_NaoRepeteRotulosNemExplicacoesRetiradas()
    {
        var anterior = new PesoProducaoAnteriorReadModel(7, "1075", "202601", "B2", null, null, null);
        var text = PdfText.Of(new PesoPdfGenerator().GenerateBytes(
            Entrada(anterior, new PesoSapResumo(140.00m, 138.50m, 18, 3, 12))));

        // Aparece o cartão do controlo anterior (a zona onde «Diferença indisponível» saiu)...
        Assert.Contains("Controlo anterior", text);
        Assert.Contains("Sem comparação", text);
        // ... e já não leva rótulos repetidos nem as explicações retiradas (DMO-526).
        Assert.DoesNotContain("Diferença indisponível", text);
        Assert.DoesNotContain("Resultado das leituras deste controlo", text);
        Assert.DoesNotContain("Volume em cm³, grandeza distinta do peso", text);
        Assert.DoesNotContain("Processo de fabrico", text);
        Assert.DoesNotContain("Ferramenta utilizada", text);
        Assert.DoesNotContain("peso usado", text);
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
