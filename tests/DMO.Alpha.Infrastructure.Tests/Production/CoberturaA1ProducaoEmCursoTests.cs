using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// UMA PRODUÇÃO EM CURSO POR LINHA (Diogo, 27/10/2026, DMO-459: dois C3 em curso
/// «isto não deve acontecer»): das produções que cobrem o dia fica a que começou
/// por último; a única exceção é uma experiência que entra e sai no mesmo dia,
/// que fica ao lado da produção da linha sem a tirar.
/// </summary>
public sealed class CoberturaA1ProducaoEmCursoTests : IDisposable
{
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    private static ProductionContextReadModel Producao(
        int id, string linha, DateOnly? inicio, DateOnly? fim, bool experiencia = false, string? referencia = null) =>
        new(id, referencia ?? $"REF{id}", $"20260{id}", linha, null, null, null, null, null, null,
            InicioEm: inicio, FimEm: fim, Experiencia: experiencia);

    private static IEnumerable<int> Ids(IEnumerable<ProductionContextReadModel> producoes) => producoes.Select(p => p.JobOnId);

    [Fact]
    public void DuasProducoesQueCobremODiaNaMesmaLinha_FicaAQueComecouPorUltimo()
    {
        var antiga = Producao(1, "C3", Dia.AddDays(-10), Dia.AddDays(10));
        var recente = Producao(2, "C3", Dia.AddDays(-2), null);
        var outraLinha = Producao(3, "B1", Dia.AddDays(-30), null);

        var emCurso = ProducaoEmCurso.UmaPorMaquina([antiga, recente, outraLinha], Dia);

        Assert.Equal([2, 3], Ids(emCurso).Order());
    }

    [Fact]
    public void ComOMesmoDiaDeInicio_FicaALancadaPorUltimo()
    {
        var primeira = Producao(5, "C3", Dia.AddDays(-1), null);
        var segunda = Producao(9, "C3", Dia.AddDays(-1), null);

        Assert.Equal([9], Ids(ProducaoEmCurso.UmaPorMaquina([primeira, segunda], Dia)));
        Assert.Equal([9], Ids(ProducaoEmCurso.UmaPorMaquina([segunda, primeira], Dia)));
    }

    [Fact]
    public void ProducaoSemFim_CobreOsDiasDepoisDoInicio_ENaoOsAntes()
    {
        var ateAviso = Producao(1, "B1", Dia, null);

        Assert.Equal([1], Ids(ProducaoEmCurso.UmaPorMaquina([ateAviso], Dia)));
        Assert.Equal([1], Ids(ProducaoEmCurso.UmaPorMaquina([ateAviso], Dia.AddDays(30))));
        Assert.Empty(ProducaoEmCurso.UmaPorMaquina([ateAviso], Dia.AddDays(-1)));
    }

    [Fact]
    public void OFimCobreOUltimoDia_ESoNesse_NoLimiteELogoAbaixoELogoAcima()
    {
        var comFim = Producao(1, "B1", Dia.AddDays(-5), Dia);

        Assert.Equal([1], Ids(ProducaoEmCurso.UmaPorMaquina([comFim], Dia.AddDays(-1))));
        Assert.Equal([1], Ids(ProducaoEmCurso.UmaPorMaquina([comFim], Dia)));
        Assert.Empty(ProducaoEmCurso.UmaPorMaquina([comFim], Dia.AddDays(1)));
    }

    [Fact]
    public void ProducaoSemDiaDeInicio_NaoEstaEmCurso()
    {
        Assert.Empty(ProducaoEmCurso.UmaPorMaquina([Producao(1, "B1", null, null)], Dia));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ExperienciaDeUmDia_SoQuandoEExperienciaEEntraESaiNoMesmoDia(bool experiencia, bool mesmoDia, bool esperado)
    {
        var producao = Producao(1, "B1", Dia, mesmoDia ? Dia : Dia.AddDays(1), experiencia);

        Assert.Equal(esperado, ProducaoEmCurso.ExperienciaDeUmDia(producao));
    }

    [Fact]
    public void ExperienciaSemFim_NaoEDeUmDia()
    {
        Assert.False(ProducaoEmCurso.ExperienciaDeUmDia(Producao(1, "B1", Dia, null, experiencia: true)));
    }

    [Fact]
    public void ExperienciaQueEntraESaiNoDia_FicaAoLadoDaProducaoDaLinha_SemATirar()
    {
        var linha = Producao(1, "C3", Dia.AddDays(-4), null);
        var experiencia = Producao(2, "C3", Dia, Dia, experiencia: true);

        var doDia = ProducaoEmCurso.DoDia([linha, experiencia], Dia);

        Assert.Equal([1, 2], Ids(doDia).Order());
    }

    [Fact]
    public void ExperienciaDeUmDia_SoApareceNoSeuDia()
    {
        var linha = Producao(1, "C3", Dia.AddDays(-4), null);
        var experiencia = Producao(2, "C3", Dia, Dia, experiencia: true);

        Assert.Equal([1], Ids(ProducaoEmCurso.DoDia([linha, experiencia], Dia.AddDays(1))));
        Assert.Equal([1], Ids(ProducaoEmCurso.DoDia([linha, experiencia], Dia.AddDays(-1))));
    }

    [Fact]
    public void ExperienciaDeUmDia_NaoTiraAProducaoNemSubstituiAEscolhaDaLinha()
    {
        // A experiência lançada depois não passa a ser «a que começou por último» da linha.
        var linha = Producao(1, "C3", Dia.AddDays(-4), null);
        var recente = Producao(3, "C3", Dia.AddDays(-1), null);
        var experiencia = Producao(7, "C3", Dia, Dia, experiencia: true);

        var doDia = ProducaoEmCurso.DoDia([linha, recente, experiencia], Dia);

        Assert.Equal([3, 7], Ids(doDia).Order());
    }

    [Fact]
    public void DuasExperienciasDeUmDiaNaMesmaLinha_FicaAUltimaLancada()
    {
        var primeira = Producao(4, "C3", Dia, Dia, experiencia: true);
        var segunda = Producao(8, "C3", Dia, Dia, experiencia: true);

        Assert.Equal([8], Ids(ProducaoEmCurso.DoDia([primeira, segunda], Dia)));
    }

    [Fact]
    public void ExperienciaDeUmDiaSozinha_AparecenaLinhaSemProducao()
    {
        var experiencia = Producao(2, "B2", Dia, Dia, experiencia: true);

        Assert.Equal([2], Ids(ProducaoEmCurso.DoDia([experiencia], Dia)));
    }

    [Fact]
    public void EntradasPorDia_UmaPorLinhaEPorDia_AUltimaLancada()
    {
        var primeira = Producao(1, "C3", Dia.AddDays(2), null);
        var segunda = Producao(2, "C3", Dia.AddDays(2), null);
        var outraLinha = Producao(3, "B1", Dia.AddDays(2), null);
        var outroDia = Producao(4, "C3", Dia.AddDays(3), null);

        var entradas = ProducaoEmCurso.EntradasPorDia([primeira, segunda, outraLinha, outroDia]);

        Assert.Equal([2, 3, 4], Ids(entradas).Order());
    }

    [Fact]
    public void EntradasPorDia_ExperienciaDeUmDiaEntraAoLadoDaProducaoDessaLinha()
    {
        var producao = Producao(1, "C3", Dia.AddDays(2), null);
        var experiencia = Producao(2, "C3", Dia.AddDays(2), Dia.AddDays(2), experiencia: true);

        Assert.Equal([1, 2], Ids(ProducaoEmCurso.EntradasPorDia([producao, experiencia])).Order());
    }

    [Fact]
    public void EntradasPorDia_PelaOrdemDeEntrada_ESemAsQueNaoTemDiaDeInicio()
    {
        var tarde = Producao(1, "B1", Dia.AddDays(5), null);
        var cedo = Producao(2, "B2", Dia.AddDays(1), null);
        var semDia = Producao(3, "B3", null, null);

        Assert.Equal([2, 1], Ids(ProducaoEmCurso.EntradasPorDia([tarde, cedo, semDia])));
    }

    // O mesmo no quadro do dia do Planeamento, lido da base.

    private async Task<IReadOnlyList<DayMachineRow>> QuadroAsync()
    {
        var maquinas = await _db.Machines.AsNoTracking().OrderBy(m => m.Id)
            .Select(m => new MachineOption(m.Id, m.Code)).ToListAsync();
        var janela = await new JobOnListQuery(_db).ListWindowAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31));
        var query = new JobOnQuadroDoDiaQuery(new JobOnListQuery(_db), new JobOnLotNoteHandler(_db), new JobOnChangeQuery(_db));
        return await query.QuadroAsync(maquinas, janela, Dia.Year, Dia.Month, Dia.Day, true);
    }

    [Fact]
    public async Task QuadroDoDia_ExperienciaDeUmDiaFicaAoLadoDaProducao_EVemMarcadaComoExperiencia()
    {
        var c3 = new Machine { Code = "C3" };
        _db.Machines.Add(c3);
        _db.JobOns.AddRange(
            new JobOn { Machine = c3, Reference = "5447T173", ProductionNumber = "1", InicioEm = Dia.AddDays(-5), FimEm = Dia.AddDays(5) },
            new JobOn { Machine = c3, Reference = "9999X999", ProductionNumber = "2", InicioEm = Dia, FimEm = Dia, Experiencia = true });
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        var daLinha = Assert.Single(linhas).Productions;
        Assert.Equal(["5447T173", "9999X999"], daLinha.Select(p => p.Reference));
        Assert.False(daLinha[0].Experiencia);
        Assert.True(daLinha[1].Experiencia);
    }

    [Fact]
    public async Task QuadroDoDia_ExperienciaDeDoisDias_NaoFicaAoLado_ECompeteComAProducaoDaLinha()
    {
        var c3 = new Machine { Code = "C3" };
        _db.Machines.Add(c3);
        _db.JobOns.AddRange(
            new JobOn { Machine = c3, Reference = "5447T173", ProductionNumber = "1", InicioEm = Dia.AddDays(-5), FimEm = Dia.AddDays(5) },
            new JobOn { Machine = c3, Reference = "9999X999", ProductionNumber = "2", InicioEm = Dia, FimEm = Dia.AddDays(1), Experiencia = true });
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        // Uma experiência de dois dias não é a exceção: a que entrou por último é a da linha.
        Assert.Equal("9999X999", Assert.Single(Assert.Single(linhas).Productions).Reference);
    }
}
