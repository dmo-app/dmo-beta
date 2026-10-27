using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Pages.JobOn;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O sidepanel do Job On («o que vai entrar») mostra as produções em curso, uma
/// por linha, e depois as que entram de hoje até uma semana em avanço (Diogo,
/// 27/10/2026: «limite uma semana em avanço»).
/// </summary>
public sealed class JobOnSidepanelWeekTests
{
    private static ProductionContextReadModel Entrada(int id, DateOnly? inicio, DateOnly? fim = null, string maquina = "B1") =>
        new(id, "REF", $"P{id}", maquina, null, null, null, null, null, null, InicioEm: inicio, FimEm: fim);

    [Fact]
    public void Mostra_so_entradas_de_hoje_ate_uma_semana()
    {
        var quinta = new DateOnly(2026, 10, 15);
        var jobOns = new[]
        {
            Entrada(1, quinta.AddDays(-1), maquina: "B2"), // ontem, sem fim: em curso
            Entrada(2, quinta),                        // hoje
            Entrada(3, new DateOnly(2026, 10, 18)),    // domingo
            Entrada(4, new DateOnly(2026, 10, 22)),    // quinta seguinte: uma semana
            Entrada(5, null),                          // sem data
            Entrada(6, new DateOnly(2026, 10, 17)),    // sábado
            Entrada(7, new DateOnly(2026, 10, 23)),    // mais de uma semana
        };

        var ids = IndexModel.ProximasEntradasDaSemana(jobOns, quinta).Select(j => j.JobOnId);

        // A em curso vem primeiro (Diogo, 22/10/2026).
        Assert.Equal(new[] { 1, 2, 6, 3, 4 }, ids);
    }

    [Fact]
    public void Em_curso_primeiro_e_as_que_ja_acabaram_ficam_de_fora()
    {
        // Diogo, 22/10/2026: um Job On lançado hoje com início no passado
        // (a produção já estava a correr) tem de aparecer na lista.
        var quinta = new DateOnly(2026, 10, 15);
        var jobOns = new[]
        {
            Entrada(1, quinta.AddDays(-30), maquina: "B1"),                    // em curso há um mês, sem fim
            Entrada(2, quinta.AddDays(-5), quinta.AddDays(2), maquina: "B2"),  // em curso, acaba sábado
            Entrada(3, quinta.AddDays(-5), quinta.AddDays(-1), maquina: "B3"), // acabou ontem
            Entrada(4, quinta.AddDays(-3), quinta, maquina: "C1"),             // acaba hoje: ainda em curso
            Entrada(5, quinta.AddDays(1), maquina: "C2"),                      // entra amanhã
        };

        var ids = IndexModel.ProximasEntradasDaSemana(jobOns, quinta).Select(j => j.JobOnId);

        Assert.Equal(new[] { 1, 2, 4, 5 }, ids);
    }

    [Fact]
    public void Uma_linha_so_tem_uma_producao_em_curso()
    {
        // Diogo, 27/10/2026: dois C3 em curso, «isto não deve acontecer». A
        // produção que entrou depois acaba a anterior, mesmo sem fim.
        var terca = new DateOnly(2026, 10, 27);
        var jobOns = new[]
        {
            Entrada(1, new DateOnly(2026, 10, 22), maquina: "C3"), // 5538T324, sem fim
            Entrada(2, terca, maquina: "C3"),                      // 9262T288, entra hoje
            Entrada(3, new DateOnly(2026, 10, 1), maquina: "B1"),  // sem fim
            Entrada(4, new DateOnly(2026, 10, 20), maquina: "B1"), // entrou depois: é esta
        };

        var ids = IndexModel.ProximasEntradasDaSemana(jobOns, terca).Select(j => j.JobOnId);

        Assert.Equal(new[] { 4, 2 }, ids);
    }
}
