using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Pages.JobOn;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A FAIXA «PLANEAMENTO» (Diogo, 27/10/2026, DMO-506 e DMO-459): só o que entra, de
/// hoje até uma semana em avanço, uma produção por linha e por dia; o que já entrou
/// sai da faixa. A gaveta «Linhas» da folha continua com as que estão em curso.
/// </summary>
public sealed class CoberturaA1EntradasDaSemanaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 28);

    private static ProductionContextReadModel Producao(
        int id, string linha, DateOnly? inicio, DateOnly? fim = null, bool experiencia = false) =>
        new(id, $"REF{id}", $"20260{id}", linha, null, null, null, null, null, null,
            InicioEm: inicio, FimEm: fim, Experiencia: experiencia);

    private static IEnumerable<int> Ids(IEnumerable<ProductionContextReadModel> producoes) => producoes.Select(p => p.JobOnId);

    [Fact]
    public void Faixa_SoTemOQueEntraDeHojeAteUmaSemana_OQueJaEntrouSai()
    {
        var jaEntrou = Producao(1, "B1", Hoje.AddDays(-1), Hoje.AddDays(10));
        var entraHoje = Producao(2, "B2", Hoje);
        var entraNoUltimoDia = Producao(3, "B3", Hoje.AddDays(7));
        var depoisDaSemana = Producao(4, "C1", Hoje.AddDays(8));

        var faixa = IndexModel.EntradasDaSemana([jaEntrou, entraHoje, entraNoUltimoDia, depoisDaSemana], Hoje);

        Assert.Equal([2, 3], Ids(faixa));
    }

    [Fact]
    public void Faixa_PelaOrdemEmQueEntram()
    {
        var tarde = Producao(1, "B1", Hoje.AddDays(5));
        var cedo = Producao(2, "B2", Hoje.AddDays(1));
        var hoje = Producao(3, "B3", Hoje);

        Assert.Equal([3, 2, 1], Ids(IndexModel.EntradasDaSemana([tarde, cedo, hoje], Hoje)));
    }

    [Fact]
    public void Faixa_VariasAEntrarNaMesmaLinhaNoMesmoDia_SoAUltimaLancada()
    {
        var primeira = Producao(1, "C3", Hoje.AddDays(2));
        var segunda = Producao(2, "C3", Hoje.AddDays(2));
        var outraLinha = Producao(3, "B1", Hoje.AddDays(2));

        Assert.Equal([2, 3], Ids(IndexModel.EntradasDaSemana([primeira, segunda, outraLinha], Hoje)).Order());
    }

    [Fact]
    public void Faixa_ExperienciaQueEntraESaiNoMesmoDia_FicaAoLadoDaProducaoDessaLinha()
    {
        var producao = Producao(1, "C3", Hoje.AddDays(2));
        var experiencia = Producao(2, "C3", Hoje.AddDays(2), Hoje.AddDays(2), experiencia: true);

        Assert.Equal([1, 2], Ids(IndexModel.EntradasDaSemana([producao, experiencia], Hoje)).Order());
    }

    [Fact]
    public void Gaveta_AsEmCurso_UmaPorLinha_VemAntesDasQueEntram()
    {
        var antiga = Producao(1, "C3", Hoje.AddDays(-10), Hoje.AddDays(20));
        var recente = Producao(2, "C3", Hoje.AddDays(-2));
        var outraLinhaMaisAntiga = Producao(3, "B1", Hoje.AddDays(-5));
        var entraHoje = Producao(4, "B2", Hoje);
        var depoisDaSemana = Producao(5, "B3", Hoje.AddDays(9));

        var gaveta = IndexModel.ProximasEntradasDaSemana([entraHoje, antiga, recente, outraLinhaMaisAntiga, depoisDaSemana], Hoje);

        // As em curso pela ordem do início (a mais antiga primeiro), a que acaba a anterior na C3, e depois as que entram.
        Assert.Equal([3, 2, 4], Ids(gaveta));
    }

    [Fact]
    public void Gaveta_ExperienciaDeUmDia_NaoEEmCurso()
    {
        var producao = Producao(1, "C3", Hoje.AddDays(-3));
        var experienciaDeOntem = Producao(2, "C3", Hoje.AddDays(-1), Hoje.AddDays(-1), experiencia: true);

        Assert.Equal([1], Ids(IndexModel.ProximasEntradasDaSemana([producao, experienciaDeOntem], Hoje)));
    }
}
