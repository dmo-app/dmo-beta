using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Pages.JobOn;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O sidepanel do Job On («o que vai entrar») mostra só produções futuras da
/// semana corrente (Seg..Dom): nada antes de hoje, nada depois de domingo.
/// </summary>
public sealed class JobOnSidepanelWeekTests
{
    private static ProductionContextReadModel Entrada(int id, DateOnly? inicio) =>
        new(id, "REF", $"P{id}", "B1", null, null, null, null, null, null, InicioEm: inicio);

    [Fact]
    public void Mostra_so_entradas_de_hoje_ate_domingo()
    {
        var quinta = new DateOnly(2026, 10, 15);
        var jobOns = new[]
        {
            Entrada(1, quinta.AddDays(-1)),            // ontem
            Entrada(2, quinta),                        // hoje
            Entrada(3, new DateOnly(2026, 10, 18)),    // domingo
            Entrada(4, new DateOnly(2026, 10, 19)),    // segunda seguinte
            Entrada(5, null),                          // sem data
            Entrada(6, new DateOnly(2026, 10, 17)),    // sábado
        };

        var ids = IndexModel.ProximasEntradasDaSemana(jobOns, quinta).Select(j => j.JobOnId);

        Assert.Equal(new[] { 2, 6, 3 }, ids);
    }

    [Fact]
    public void Ao_domingo_mostra_so_esse_dia()
    {
        var domingo = new DateOnly(2026, 10, 18);
        var jobOns = new[] { Entrada(1, domingo), Entrada(2, domingo.AddDays(1)) };

        var ids = IndexModel.ProximasEntradasDaSemana(jobOns, domingo).Select(j => j.JobOnId);

        Assert.Equal(new[] { 1 }, ids);
    }
}
