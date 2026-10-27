using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// As decisões do Diogo de 19/10/2026 para o Peso: a densidade do vidro NNPB
/// e PS nas Definições do Controlo (D15), a densidade da água que cada Peso
/// usou fica gravada nele (AV-PESO-01), «Manter / Colocar de parte» cada CM
/// da comparação, com a aprovação por CM (D10, D11) e o aviso «não
/// comparável» quando a punção ou a densidade mudou desde o Peso inicial
/// (D12). Os pesos usam o exemplo da casa (199,64 g a 20 °C → 450,52 g).
/// </summary>
public sealed class PesoDecisoesDoDiogoTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoQuery _pesoQuery;
    private readonly PesoComparisonQuery _comparacoes;

    public PesoDecisoesDoDiogoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _pesoQuery = new PesoQuery(_db);
        _comparacoes = new PesoComparisonQuery(_db, _pesoQuery);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// A produção 202601 (1075C142, B1) com a CM T3 (5447, lote 3, NNPB,
    /// punção na ficha <paramref name="puncaoDaFicha"/>) e o Peso inicial
    /// (punção 30, vidro 2,50, a 20 °C) com duas leituras.
    /// </summary>
    private async Task<int> SeedPesoInicialAsync(decimal puncaoDaFicha = 30m, decimal densidadeConfigurada = 2.50m)
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T3", Process = CmProcess.Nnpb, VolumePuncaoCm3 = puncaoDaFicha });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ-TOOL-9", VolumeMarisaCm3 = 10m });
        _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = densidadeConfigurada });
        var jobOn = new JobOn { Reference = "1075C142", ProductionNumber = "202601", Machine = machine };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        var peso = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 199.64m });
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 2, CmSubject = "21", WaterWeightGrams = 99.82m });
        await _db.SaveChangesAsync();
        return peso.Id;
    }

    private Task<PesoComparacaoLeiturasResultado> CompararAsync(int pesoInicialId) =>
        _comparacoes.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId, "T3", 20m, [new("12", 199.64m), new("21", 99.82m)]));

    private async Task<PesoComparacaoReadModel> GuardarComparacaoAsync(int pesoInicialId)
    {
        var outcome = await new CreatePesoComparacaoHandler(_db, _comparacoes).HandleAsync(
            new CreatePesoComparacao(pesoInicialId, "T3", 20m, [new("12", 199.64m), new("21", 99.82m)]));
        Assert.Equal(PesoComparacaoResultado.Sucesso, outcome.Result);
        return outcome.Comparacao!;
    }

    // ---------------------------------------------------------------- D15

    [Fact]
    public async Task DensidadeDoVidro_GravaNnpbEPs_EOsPesosGravadosFicamComAQueUsaram()
    {
        var pesoId = await SeedPesoInicialAsync();
        var antes = await _pesoQuery.FindPesoAsync(pesoId);

        var result = await new SaveGlassDensityHandler(_db).SaveAsync(2.4027m, 2.4231m);

        Assert.True(result.Saved);
        Assert.Empty(result.Rounded);
        Assert.Empty(result.OutsideUsualRange);
        var configuracao = new GlassDensityConfiguration(_db);
        Assert.Equal(2.4027m, await configuracao.FindByProcessAsync(CmProcess.Nnpb));
        Assert.Equal(2.4231m, await configuracao.FindByProcessAsync(CmProcess.Ps));
        // O Peso gravado continua com 2,50 e o mesmo peso do vidro.
        var depois = await _pesoQuery.FindPesoAsync(pesoId);
        Assert.Equal(2.50m, depois!.GlassDensityGramsPerCm3);
        Assert.Equal(antes!.Averages!.AverageGlassWeightGrams, depois.Averages!.AverageGlassWeightGrams);
    }

    [Theory]
    [InlineData("0", "2.4231")]
    [InlineData("2.4027", "-1")]
    [InlineData("0.00004", "2.4231")]
    public async Task DensidadeDoVidro_ZeroOuNegativaNumDosCampos_NaoGravaNenhuma(string nnpb, string ps)
    {
        _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        await _db.SaveChangesAsync();

        var result = await new SaveGlassDensityHandler(_db).SaveAsync(Dec(nnpb), Dec(ps));

        Assert.False(result.Saved);
        var guardadas = await _db.GlassDensities.AsNoTracking().ToListAsync();
        Assert.Equal(2.50m, Assert.Single(guardadas).DensityGramsPerCm3);
    }

    [Theory]
    [InlineData("1.9999", true, false)]
    [InlineData("2.0000", false, false)]
    [InlineData("3.0000", false, false)]
    [InlineData("3.0001", true, false)]
    [InlineData("2.40274", false, true)]
    [InlineData("0.0001", true, false)]
    public async Task DensidadeDoVidro_ForaDoHabitualOuComMaisCasas_GravaComAviso(string valor, bool fora, bool arredondada)
    {
        var result = await new SaveGlassDensityHandler(_db).SaveAsync(Dec(valor), 2.4231m);

        Assert.True(result.Saved);
        Assert.Equal(fora, result.OutsideUsualRange.Contains(CmProcess.Nnpb));
        Assert.Equal(arredondada, result.Rounded.Contains(CmProcess.Nnpb));
        Assert.DoesNotContain(CmProcess.Ps, result.OutsideUsualRange);
        Assert.Equal(GlassDensityLimits.Normalize(Dec(valor)), await new GlassDensityConfiguration(_db).FindByProcessAsync(CmProcess.Nnpb));
    }

    // ----------------------------------------------------------- AV-PESO-01

    [Fact]
    public async Task PesoGravado_UsaADensidadeDaAguaQueGuardou_NaoADaTabelaCorrigida()
    {
        await SeedPesoInicialAsync();
        var peso = await _db.Pesos.SingleAsync();
        peso.WaterTemperatureC = 18;
        peso.WaterDensityGramsPerCm3 = 0.99765m; // a folha «Temp» antes da DT9
        await _db.SaveChangesAsync();

        var lido = await _pesoQuery.FindPesoAsync(peso.Id);
        Assert.Equal(PesoCalculation.CapacityCm3(199.64m, 0.99765m), lido!.Measurements[0].CapacityCm3);
        Assert.NotEqual(PesoCalculation.CapacityCm3(199.64m, WaterDensityTable.Resolve(18)!.Value), lido.Measurements[0].CapacityCm3);

        // Sem densidade gravada (um Peso criado diretamente), vale a tabela.
        peso.WaterDensityGramsPerCm3 = null;
        await _db.SaveChangesAsync();
        lido = await _pesoQuery.FindPesoAsync(peso.Id);
        Assert.Equal(PesoCalculation.CapacityCm3(199.64m, 0.99755m), lido!.Measurements[0].CapacityCm3);
    }

    [Fact]
    public async Task PesoNovo_GravaADensidadeDaAguaQueUsou()
    {
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T3", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 30m });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ-TOOL-9", VolumeMarisaCm3 = 10m });
        _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        await _db.SaveChangesAsync();

        var outcome = await new CreateToolScopedPesoHandler(_db, new GlassDensityConfiguration(_db))
            .HandleAsync(new CreateToolScopedPeso("T3", "BQ-TOOL-9", 18.2m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        var gravado = await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == outcome.Peso!.PesoId);
        Assert.Equal(0.99755m, gravado.WaterDensityGramsPerCm3);
    }

    // ---------------------------------------------------------------- D12

    [Fact]
    public async Task Comparacao_ComAMesmaPuncaoEDensidade_EComparavelESemAviso()
    {
        var pesoInicialId = await SeedPesoInicialAsync();

        var resultado = await CompararAsync(pesoInicialId);

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        Assert.True(resultado.Comparabilidade!.Comparavel);
    }

    [Fact]
    public async Task Comparacao_APuncaoDaCmMudouDesdeOPesoInicial_AvisaNaoComparavel_SemRecusar()
    {
        var pesoInicialId = await SeedPesoInicialAsync(puncaoDaFicha: 31m);

        var resultado = await CompararAsync(pesoInicialId);

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        var c = resultado.Comparabilidade!;
        Assert.False(c.Comparavel);
        Assert.True(c.PuncaoMudou);
        Assert.False(c.DensidadeMudou);
        Assert.Equal((30m, 31m), (c.PuncaoInicialCm3, c.PuncaoCm3));

        // Grava na mesma, e a guardada diz o mesmo, pelos valores congelados.
        var guardada = await GuardarComparacaoAsync(pesoInicialId);
        Assert.True(guardada.Comparabilidade!.PuncaoMudou);
        Assert.Equal(1, await _db.PesoComparacoes.CountAsync());
    }

    [Fact]
    public async Task Comparacao_ADensidadeDoVidroMudouNasDefinicoes_AvisaNaoComparavel_EAGuardadaFicaComOsSeusValores()
    {
        var pesoInicialId = await SeedPesoInicialAsync(densidadeConfigurada: 2.60m);

        var resultado = await CompararAsync(pesoInicialId);

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        Assert.True(resultado.Comparabilidade!.DensidadeMudou);
        Assert.False(resultado.Comparabilidade.PuncaoMudou);

        var guardada = await GuardarComparacaoAsync(pesoInicialId);
        // Voltar a densidade das Definições ao valor antigo não muda a
        // comparação guardada: continua não comparável (usou 2,60).
        (await _db.GlassDensities.SingleAsync()).DensityGramsPerCm3 = 2.50m;
        await _db.SaveChangesAsync();
        var relida = await _comparacoes.ObterComparacaoGuardadaAsync(guardada.ComparacaoId);
        Assert.Equal((2.50m, 2.60m), (relida!.Comparabilidade!.DensidadeInicial, relida.Comparabilidade.Densidade));
        Assert.False(relida.Comparabilidade.Comparavel);
    }

    // ---------------------------------------------------------- D10 / D11

    [Fact]
    public async Task ColocarDeParte_FicaPorAprovar_EAAprovacaoEPorCm()
    {
        var pesoInicialId = await SeedPesoInicialAsync();
        var guardada = await GuardarComparacaoAsync(pesoInicialId);
        var handler = new PesoCmDeParteHandler(_db);
        var linhas = await handler.DaComparacaoAsync(guardada.ComparacaoId, default);
        Assert.Equal(new[] { "12", "21" }, linhas.Select(l => l.CmSubject));
        Assert.All(linhas, l => Assert.Null(l.DeParte));

        Assert.Equal(CmDeParteResultado.Gravado, await handler.EscolherAsync(linhas[0].MeasurementId, true, "Ana", default));
        Assert.Equal(CmDeParteResultado.Gravado, await handler.EscolherAsync(linhas[1].MeasurementId, true, "Ana", default));
        Assert.Equal(2, await _db.PesoComparacaoCmDecisoes.CountAsync(d => d.DeParte && d.Aprovado == null));

        // Aprovar uma não decide a outra.
        var escolha = await _db.PesoComparacaoCmDecisoes.AsNoTracking().SingleAsync(d => d.MeasurementId == linhas[0].MeasurementId);
        Assert.Equal(CmDeParteResultado.Gravado,
            await handler.DecidirAsync(linhas[0].MeasurementId, true, escolha.EscolhidoEmUtc, "Chefe", default));
        Assert.Equal(1, await _db.PesoComparacaoCmDecisoes.CountAsync(d => d.DeParte && d.Aprovado == null));
        var depois = await handler.DaComparacaoAsync(guardada.ComparacaoId, default);
        Assert.Equal(("12", true, "Chefe"), (depois[0].CmSubject, depois[0].Aprovado, depois[0].AprovadoPor));
        Assert.Equal(("21", true, (bool?)null), (depois[1].CmSubject, depois[1].DeParte, depois[1].Aprovado));
    }

    [Fact]
    public async Task MudarAEscolha_TiraAAprovacao_EManterNaoVaiParaAprovacao()
    {
        var pesoInicialId = await SeedPesoInicialAsync();
        var guardada = await GuardarComparacaoAsync(pesoInicialId);
        var handler = new PesoCmDeParteHandler(_db);
        var cm = (await handler.DaComparacaoAsync(guardada.ComparacaoId, default))[0].MeasurementId;
        await handler.EscolherAsync(cm, true, "Ana", default);
        var escolha = await _db.PesoComparacaoCmDecisoes.AsNoTracking().SingleAsync();
        await handler.DecidirAsync(cm, false, escolha.EscolhidoEmUtc, "Chefe", default);

        Assert.Equal(CmDeParteResultado.Gravado, await handler.EscolherAsync(cm, false, "Ana", default));

        var linha = (await handler.DaComparacaoAsync(guardada.ComparacaoId, default))[0];
        Assert.Equal((false, (bool?)null, (string?)null), (linha.DeParte!.Value, linha.Aprovado, linha.AprovadoPor));
        Assert.Equal(0, await _db.PesoComparacaoCmDecisoes.CountAsync(d => d.DeParte && d.Aprovado == null));
        // A mesma escolha outra vez não muda nada.
        Assert.Equal(CmDeParteResultado.SemAlteracao, await handler.EscolherAsync(cm, false, "Ana", default));
    }

    [Fact]
    public async Task Aprovar_ComAEscolhaMudadaEntretanto_NaoGravaNada()
    {
        var pesoInicialId = await SeedPesoInicialAsync();
        var guardada = await GuardarComparacaoAsync(pesoInicialId);
        var handler = new PesoCmDeParteHandler(_db);
        var cm = (await handler.DaComparacaoAsync(guardada.ComparacaoId, default))[0].MeasurementId;
        await handler.EscolherAsync(cm, true, "Ana", default);
        var vista = (await _db.PesoComparacaoCmDecisoes.AsNoTracking().SingleAsync()).EscolhidoEmUtc;

        // Quem aprova viu a escolha de antes; a CM passou a «Manter».
        await handler.EscolherAsync(cm, false, "Ana", default);
        Assert.Equal(CmDeParteResultado.Mudou, await handler.DecidirAsync(cm, true, vista, "Chefe", default));
        // Uma escolha que não é a vista (outra hora) também não grava.
        await handler.EscolherAsync(cm, true, "Ana", default);
        Assert.Equal(CmDeParteResultado.Mudou, await handler.DecidirAsync(cm, true, vista.AddSeconds(-1), "Chefe", default));

        var gravada = await _db.PesoComparacaoCmDecisoes.AsNoTracking().SingleAsync();
        Assert.Null(gravada.Aprovado);
        Assert.Null(gravada.AprovadoPor);
    }

    [Fact]
    public async Task ColocarDeParte_NumaLeituraQueNaoExiste_NaoGravaNada()
    {
        var handler = new PesoCmDeParteHandler(_db);

        Assert.Equal(CmDeParteResultado.NaoExiste, await handler.EscolherAsync(999, true, "Ana", default));
        Assert.Equal(0, await _db.PesoComparacaoCmDecisoes.CountAsync());
    }

    [Fact]
    public async Task ColocarDeParteEAprovar_NaoMudaACmNasFerramentas()
    {
        var pesoInicialId = await SeedPesoInicialAsync();
        var guardada = await GuardarComparacaoAsync(pesoInicialId);
        var antes = System.Text.Json.JsonSerializer.Serialize(await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == "T3"));
        var handler = new PesoCmDeParteHandler(_db);
        var cm = (await handler.DaComparacaoAsync(guardada.ComparacaoId, default))[0].MeasurementId;

        await handler.EscolherAsync(cm, true, "Ana", default);
        var escolha = await _db.PesoComparacaoCmDecisoes.AsNoTracking().SingleAsync();
        await handler.DecidirAsync(cm, true, escolha.EscolhidoEmUtc, "Chefe", default);

        var depois = System.Text.Json.JsonSerializer.Serialize(await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == "T3"));
        Assert.Equal(antes, depois);
    }

    private static decimal Dec(string valor) =>
        decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);
}
