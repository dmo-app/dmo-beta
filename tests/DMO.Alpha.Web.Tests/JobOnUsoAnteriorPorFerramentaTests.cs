using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Pages.JobOn;

namespace DMO.Alpha.Web.Tests;

public sealed class JobOnUsoAnteriorPorFerramentaTests
{
    [Theory]
    [InlineData("CM")]
    [InlineData("MF")]
    [InlineData("BQ")]
    public void Antes_EOFimDoUltimoJobOnDaMesmaFerramenta_MesmoNoutraLinhaEReferencia(string tipo)
    {
        var atual = new ProductionContextReadModel(30, "NOVA", "202603", "B1",
            tipo == "CM" ? 103 : null, tipo == "CM" ? "F1" : null,
            tipo == "MF" ? 203 : null, tipo == "MF" ? "F1" : null,
            tipo == "BQ" ? 303 : null, tipo == "BQ" ? "F1" : null,
            new(2026, 11, 10), new(2026, 11, 15));
        IReadOnlyDictionary<string, IReadOnlyList<FerramentaProducao>> producoes =
            new Dictionary<string, IReadOnlyList<FerramentaProducao>>
            {
                ["F1"] = [
                    new(10, "ANTIGA", "202601", "C3", new(2026, 9, 1), new(2026, 9, 5), 100, null, 1, 0m, 5m),
                    // Há leitura de uso no fim, mesmo sem relatório de peças do SAP.
                    new(20, "OUTRA", "202602", "B3", new(2026, 10, 1), new(2026, 10, 5), null, null, 0, 5m, 12m),
                    new(30, "NOVA", "202603", "B1", new(2026, 11, 10), new(2026, 11, 15), 200, null, 1, 25m, 40m),
                    new(40, "FUTURA", "202604", "C1", new(2026, 12, 1), new(2026, 12, 5), 300, null, 1, 40m, 60m)
                ],
                ["OUTRO-LOTE"] = [new(29, "NOVA", "202602", "B1", new(2026, 11, 8), new(2026, 11, 9), 50, null, 1, 70m, 80m)]
            };
        var hoje = new Dictionary<string, decimal> { ["F1"] = 60m };

        Assert.Equal(12m, FolhaModel.UsoNoInicio(atual, producoes, hoje)["F1"]);
    }
}
