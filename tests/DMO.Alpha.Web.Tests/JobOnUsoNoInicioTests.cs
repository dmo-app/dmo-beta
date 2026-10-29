using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Pages.JobOn;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A % de uso no início de cada produção, pelo tool_id (Diogo, 29/10/2026: «a produção 202601 está a associar
/// % do 202602 como anterior»): a 1.ª produção de uma ferramenta nova começa a 0, a que ainda não correu
/// começa na % atual.
/// </summary>
public sealed class JobOnUsoNoInicioTests
{
    private static ProductionContextReadModel J(int id, DateOnly inicio) =>
        new(id, "7080C002", id.ToString(), "C3", 1, "CM1", null, null, null, null, inicio, inicio.AddDays(2));

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<FerramentaProducao>> Producoes =
        new Dictionary<string, IReadOnlyList<FerramentaProducao>>
        {
            ["CM1"] =
            [
                new(1, "7080C002", "202601", "C3", new DateOnly(2026, 5, 18), new DateOnly(2026, 5, 19), 68513, null, 3),
                new(2, "7080C002", "202602", "C3", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 9), null, null, 0),
            ]
        };

    private static readonly IReadOnlyDictionary<string, decimal> Atual = new Dictionary<string, decimal> { ["CM1"] = 6.7m };

    [Fact]
    public void PrimeiraProducao_ComecaAZero()
    {
        Assert.Equal(0m, FolhaModel.UsoNoInicio(J(1, new DateOnly(2026, 5, 18)), Producoes, Atual)["CM1"]);
    }

    [Fact]
    public void ProducaoQueAindaNaoCorreu_ComecaNaPercentagemAtual()
    {
        Assert.Equal(6.7m, FolhaModel.UsoNoInicio(J(2, new DateOnly(2026, 11, 2)), Producoes, Atual)["CM1"]);
    }
}
