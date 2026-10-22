using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Pages.JobOn;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O sidepanel do Job On («o que vai entrar») mostra só produções futuras da
/// semana corrente (Seg..Dom), depois das produções em curso; nada depois de domingo.
/// </summary>
public sealed class JobOnSidepanelWeekTests
{
    private static ProductionContextReadModel Entrada(int id, DateOnly? inicio, DateOnly? fim = null) =>
        new(id, "REF", $"P{id}", "B1", null, null, null, null, null, null, InicioEm: inicio, FimEm: fim);

    [Fact]
    public void Mostra_so_entradas_de_hoje_ate_domingo()
    {
        var quinta = new DateOnly(2026, 10, 15);
        var jobOns = new[]
        {
            Entrada(1, quinta.AddDays(-1)),            // ontem, sem fim: em curso
            Entrada(2, quinta),                        // hoje
            Entrada(3, new DateOnly(2026, 10, 18)),    // domingo
            Entrada(4, new DateOnly(2026, 10, 19)),    // segunda seguinte
            Entrada(5, null),                          // sem data
            Entrada(6, new DateOnly(2026, 10, 17)),    // sábado
        };

        var ids = IndexModel.ProximasEntradasDaSemana(jobOns, quinta).Select(j => j.JobOnId);

        // A em curso vem primeiro (Diogo, 22/10/2026).
        Assert.Equal(new[] { 1, 2, 6, 3 }, ids);
    }

    [Fact]
    public void Em_curso_primeiro_e_as_que_ja_acabaram_ficam_de_fora()
    {
        // Diogo, 22/10/2026: um Job On lançado hoje com início no passado
        // (a produção já estava a correr) tem de aparecer na lista.
        var quinta = new DateOnly(2026, 10, 15);
        var jobOns = new[]
        {
            Entrada(1, quinta.AddDays(-30)),                    // em curso há um mês, sem fim
            Entrada(2, quinta.AddDays(-5), quinta.AddDays(2)),  // em curso, acaba sábado
            Entrada(3, quinta.AddDays(-5), quinta.AddDays(-1)), // acabou ontem
            Entrada(4, quinta.AddDays(-3), quinta),             // acaba hoje: ainda em curso
            Entrada(5, quinta.AddDays(1)),                      // entra amanhã
        };

        var ids = IndexModel.ProximasEntradasDaSemana(jobOns, quinta).Select(j => j.JobOnId);

        Assert.Equal(new[] { 1, 2, 4, 5 }, ids);
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
