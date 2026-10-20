using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova as comparações TODAS do Peso no backend: a comparação das novas
/// leituras dos contra-moldes contra o Peso inicial (a base consumida — a
/// densidade do vidro da CM ou, na sua falta, a congelada do Peso inicial;
/// o volume_marisa SEMPRE do Peso inicial congelado), as recusas tipadas
/// (o peso inicial não selecionado, a temperatura fora da tabela, a massa
/// de água não utilizável, sem leituras, o resultado não positivo), o
/// registo guardado com os inputs CONGELADOS (uma edição posterior da Tool
/// não reescreve o registo), o comparar de 2 Pesos por id e as opções dos
/// selects. Os pesos usam o worked example da casa (199,64 g a 20 °C →
/// 450,52 g de vidro).
/// </summary>
public sealed class PesoComparacaoHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoQuery _pesoQuery;
    private readonly PesoComparisonQuery _query;
    private readonly CreatePesoComparacaoHandler _handler;

    public PesoComparacaoHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _pesoQuery = new PesoQuery(_db);
        _query = new PesoComparisonQuery(_db, _pesoQuery);
        _handler = new CreatePesoComparacaoHandler(_db, _query);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>
    /// O worked example: a produção 202601 (1075C142, B1) com a CM 5447
    /// (lote 3, NNPB) e a BQ T194; o Peso inicial com DUAS leituras
    /// (199,64 g e 99,82 g a 20 °C — 450,52 g e 200,26 g de vidro).
    /// </summary>
    private async Task<(int JobOnId, int ControloId, int PesoInicialId)> SeedProducaoAsync(
        bool densidadeConfigurada = true,
        decimal? volumePuncaoDaTool = 30m)
    {
        var machine = new Machine { Code = "B1" };
        var cmTool = new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" };
        var bqTool = new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" };
        _db.Machines.Add(machine);
        _db.Tools.Add(cmTool);
        _db.Tools.Add(bqTool);
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb,
            VolumePuncaoCm3 = volumePuncaoDaTool
        });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "BQ-TOOL-9",
            VolumeMarisaCm3 = 10m
        });
        if (densidadeConfigurada)
        {
            _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        }

        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = "202601",
            Machine = machine
        };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" });
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

        return (jobOn.Id, controlo.Id, peso.Id);
    }

    [Fact]
    public async Task CompararLeituras_CalculaPeloBackendComABaseDoPesoInicial()
    {
        // As novas leituras (a mesma CM) contra o Peso inicial: o peso do
        // vidro pela fórmula do Peso (199,64 g a 20 °C → 450,52 g); os pares
        // pelo nº de CM («12»↔«12», «21»↔«21», «31» sem contraparte).
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var resultado = await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId,
            "T3",
            20m,
            new[]
            {
                new PesoComparacaoLeituraInput("12", 199.64m),
                new PesoComparacaoLeituraInput("21", 99.82m),
                new PesoComparacaoLeituraInput("31", 150m)
            }));

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        Assert.Equal(20, resultado.WaterTemperatureC);
        Assert.Equal(0.99717m, resultado.WaterDensityGramsPerCm3);
        Assert.Equal(2.50m, resultado.GlassDensityGramsPerCm3);
        Assert.Equal(10m, resultado.VolumeMarisaCm3);        // SEMPRE do Peso inicial
        Assert.Equal(30m, resultado.VolumePuncaoCm3);        // da CM (a mesma)
        Assert.Equal(325.38734618971689882367098890m, resultado.PesoInicialPesoMedioGrams); // a média das DUAS leituras do inicial
        Assert.False(resultado.FerramentaDiferente);

        Assert.Equal(3, resultado.Leituras.Count);
        Assert.Equal(450.51646158628919843156131852m, resultado.Leituras[0].GlassWeightGrams);
        Assert.Equal(200.25823079314459921578065928m, resultado.Leituras[1].GlassWeightGrams);
        Assert.Equal(326.06426186106681909804747435m, resultado.Leituras[2].GlassWeightGrams);

        Assert.NotNull(resultado.Medias);
        Assert.Equal(3, resultado.Pares!.Rows.Count);
        Assert.Equal(2, resultado.Pares.ParesValidos);
        Assert.Equal(1, resultado.Pares.SemContraparte);
        Assert.Equal("12", resultado.Pares.Rows[0].CmSubjectAnterior);
        Assert.Equal(0m, resultado.Pares.Rows[0].DeltaPesoGrams);
        Assert.Null(resultado.Pares.Rows[2].CmSubjectAnterior);
        Assert.Equal(0m, resultado.Pares.DeltaPesoMedioGrams);
    }

    [Fact]
    public async Task CompararLeituras_SemProcessoNaTool_UsaADensidadeCongeladaDoPesoInicial()
    {
        // O html: «cm.process || initial.result.glass_density» — a CM sem
        // processo cai na densidade CONGELADA do Peso inicial.
        var (_, _, pesoInicialId) = await SeedProducaoAsync(densidadeConfigurada: false);

        var resultado = await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }));

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        Assert.Equal(2.50m, resultado.GlassDensityGramsPerCm3);
        Assert.Equal(450.51646158628919843156131852m, resultado.Leituras[0].GlassWeightGrams);
    }

    [Fact]
    public async Task CompararLeituras_SemToolCaiNoCmDoPesoInicial()
    {
        // O fallback do html: «state.data.tools.find(...) || initial.context?.cm».
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var resultado = await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId, null, 20m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }));

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        Assert.Equal("T3", resultado.CmToolId);
        Assert.Equal("5447", resultado.CmToolReference);
    }

    [Fact]
    public async Task CompararLeituras_RecusasTipadas()
    {
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        // O peso inicial não selecionado (o id 0 do form vazio).
        Assert.Equal(PesoComparacaoResultado.PesoInicialNaoSelecionado,
            (await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(0, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }))).Refusal);

        // O peso inicial que não existe.
        Assert.Equal(PesoComparacaoResultado.PesoInicialNaoEncontrado,
            (await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(9999, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }))).Refusal);

        // A temperatura fora da tabela (5–35 °C).
        Assert.Equal(PesoComparacaoResultado.TemperaturaInvalida,
            (await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(pesoInicialId, "T3", 40m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }))).Refusal);

        // Sem leituras.
        Assert.Equal(PesoComparacaoResultado.SemLeituras,
            (await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(pesoInicialId, "T3", 20m, Array.Empty<PesoComparacaoLeituraInput>()))).Refusal);

        // A massa de água não positiva.
        Assert.Equal(PesoComparacaoResultado.MassaAguaNaoPositiva,
            (await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(pesoInicialId, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 0m) }))).Refusal);
        Assert.Equal(PesoComparacaoResultado.MassaAguaNaoPositiva,
            (await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(pesoInicialId, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", -5m) }))).Refusal);
    }

    [Fact]
    public async Task CompararLeituras_ResultadoNaoPositivo_RecusaAntesDeQualquerValor()
    {
        // O baseline §8: um resultado não estritamente positivo deixa a
        // leitura de fora (beta.65); sendo a única, não há nada a comparar
        // (o volume punção maior do que a capacidade + marisa).
        var (_, _, pesoInicialId) = await SeedProducaoAsync(volumePuncaoDaTool: 500m);

        var resultado = await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }));

        Assert.Equal(PesoComparacaoResultado.ResultadoNaoPositivo, resultado.Refusal);
        Assert.Empty(resultado.Leituras);
        Assert.Null(resultado.Pares);
    }

    [Fact]
    public async Task Guardar_PersisteORegistoComOsInputsCongelados()
    {
        var (jobOnId, controloId, pesoInicialId) = await SeedProducaoAsync();

        var outcome = await _handler.HandleAsync(new CreatePesoComparacao(
            pesoInicialId,
            "T3",
            20m,
            new[]
            {
                new PesoComparacaoLeituraInput("12", 199.64m),
                new PesoComparacaoLeituraInput("31", 150m)
            },
            "Verificação do desgaste"));

        Assert.Equal(PesoComparacaoResultado.Sucesso, outcome.Result);
        Assert.NotNull(outcome.Comparacao);
        var registo = outcome.Comparacao!;

        Assert.Equal(controloId, registo.ControloId);
        Assert.Equal("1075C142", registo.Reference);
        Assert.Equal("202601", registo.ProductionNumber);
        Assert.Equal("B1", registo.Machine);
        Assert.Equal("5447", registo.CmToolReference);   // o facto humano congelado
        Assert.Equal("3", registo.CmToolLot);
        Assert.Equal(20, registo.WaterTemperatureC);
        Assert.Equal(0.99717m, registo.WaterDensityGramsPerCm3);
        Assert.Equal(2.50m, registo.GlassDensityGramsPerCm3);
        Assert.Equal(10m, registo.VolumeMarisaCm3);
        Assert.Equal(30m, registo.VolumePuncaoCm3);
        Assert.Equal("Verificação do desgaste", registo.Note);
        Assert.Equal(2, registo.Leituras.Count);
        Assert.Equal(1, registo.Pares.ParesValidos);
        Assert.Equal(1, registo.Pares.SemContraparte);

        // O registo vive na BD sob o controlo da produção.
        var persistido = await _db.PesoComparacoes.AsNoTracking().SingleAsync();
        Assert.Equal(controloId, persistido.ControloId);
        Assert.Equal(pesoInicialId, persistido.PesoInicialId);
        Assert.NotEqual(default, persistido.CreatedAtUtc);
        Assert.Equal(2, await _db.PesoComparacaoMeasurements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Guardado_CalculoCongeladoContraEdicaoPosteriorDaTool()
    {
        // O padrão do Peso: o registo concluído é reproduzível — depois de a
        // Tool mudar (o volume punção) e a configuração mudar, o read model
        // do registo continua a calcular com os inputs CONGELADOS.
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var outcome = await _handler.HandleAsync(new CreatePesoComparacao(
            pesoInicialId, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }));
        var comparacaoId = outcome.Comparacao!.ComparacaoId;

        var technical = await _db.ToolTechnicalContexts.SingleAsync(t => t.ToolId == "T3");
        technical.VolumePuncaoCm3 = 99m;
        var density = await _db.GlassDensities.SingleAsync();
        _db.GlassDensities.Remove(density);
        await _db.SaveChangesAsync();

        var registo = await _query.ObterComparacaoGuardadaAsync(comparacaoId);

        Assert.NotNull(registo);
        Assert.Equal(30m, registo!.VolumePuncaoCm3);
        Assert.Equal(2.50m, registo.GlassDensityGramsPerCm3);
        Assert.Equal(450.51646158628919843156131852m, registo.Leituras[0].GlassWeightGrams);
        Assert.Equal(1, registo.Pares.ParesValidos);
    }

    [Fact]
    public async Task CompararGuardados_PorId_ParesPeloNumeroDoCm()
    {
        var (_, controloId, pesoInicialId) = await SeedProducaoAsync();

        // O segundo Peso da produção: o CM «12» pesa menos (198,64 g — o
        // desgaste) e entra o CM «31» novo; o «21» desapareceu.
        var pesoSegundo = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = null // tool-scoped também é comparável (o html lista todos)
        };
        _db.Pesos.Add(pesoSegundo);
        await _db.SaveChangesAsync();
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = pesoSegundo.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 198.64m });
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = pesoSegundo.Id, RowPosition = 2, CmSubject = "31", WaterWeightGrams = 150m });
        await _db.SaveChangesAsync();

        var view = await _query.CompararGuardadosAsync(pesoInicialId, pesoSegundo.Id);

        Assert.NotNull(view);
        Assert.Equal(pesoInicialId, view!.PrimeiroPesoId);
        Assert.Equal(pesoSegundo.Id, view.SegundoPesoId);
        Assert.Contains("1075C142", view.PrimeiroRotulo);
        Assert.Contains("5447", view.SegundoRotulo);
        Assert.True(view.Compativel);
        Assert.Equal(1, view.Pares.ParesValidos);
        Assert.Equal("12", view.Pares.Rows[0].CmSubjectAnterior);
        // (450,51646… − 448,00936…) = +2,50709… g — o desgaste com o sinal
        // preservado (198,64 g de água → a capacidade resolve pela tabela).
        Assert.Equal(2.50709507907377879398698314m, view.Pares.Rows[0].DeltaPesoGrams);
        Assert.Equal(2.50709507907377879398698314m, view.Pares.DeltaPesoMedioGrams);
        Assert.Equal(1, view.Pares.SemContraparte); // o «21» do primeiro não tem contraparte no segundo
    }

    [Fact]
    public async Task CompararGuardados_Inexistente_Null()
    {
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        Assert.Null(await _query.CompararGuardadosAsync(pesoInicialId, 9999));
        Assert.Null(await _query.CompararGuardadosAsync(0, pesoInicialId));
    }

    [Fact]
    public async Task CompararGuardados_MesmoPeso_DeltaZeroEmVezDeRecusa()
    {
        // A18 (beta.65): o mesmo Peso dos dois lados já não é recusa — cada
        // leitura emparelha consigo própria e o Δ é 0.
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var view = await _query.CompararGuardadosAsync(pesoInicialId, pesoInicialId);

        Assert.NotNull(view);
        Assert.Equal(pesoInicialId, view!.PrimeiroPesoId);
        Assert.Equal(pesoInicialId, view.SegundoPesoId);
        Assert.True(view.Compativel);
        Assert.Equal(2, view.Pares.ParesValidos);
        Assert.Equal(0, view.Pares.SemContraparte);
        Assert.Equal(0m, view.Pares.DeltaPesoMedioGrams);
        Assert.All(view.Pares.Rows, row => Assert.Equal(0m, row.DeltaPesoGrams));
        Assert.Equal(view.PrimeiroPesoMedioGrams, view.SegundoPesoMedioGrams);
    }

    [Fact]
    public async Task CompararLeituras_MassaNaoPositivaNumaLinha_FicaDeForaEAsOutrasComparam()
    {
        // A3 (beta.65): uma leitura com massa ≤ 0 já não anula a comparação.
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var resultado = await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId, "T3", 20m, new[]
            {
                new PesoComparacaoLeituraInput("12", 199.64m),
                new PesoComparacaoLeituraInput("21", 0m),
                new PesoComparacaoLeituraInput("31", -4m)
            }));

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        var leitura = Assert.Single(resultado.Leituras);
        Assert.Equal("12", leitura.CmSubject);
        Assert.Equal(1, leitura.RowPosition);
        Assert.Equal(1, resultado.Pares!.ParesValidos);
        Assert.NotNull(resultado.Ignoradas);
        Assert.Collection(resultado.Ignoradas!,
            i =>
            {
                Assert.Equal(2, i.Indice);
                Assert.Equal("21", i.CmSubject);
                Assert.Equal(PesoComparacaoMotivoIgnorada.MassaNaoPositiva, i.Motivo);
            },
            i =>
            {
                Assert.Equal(3, i.Indice);
                Assert.Equal("31", i.CmSubject);
                Assert.Equal(PesoComparacaoMotivoIgnorada.MassaNaoPositiva, i.Motivo);
            });
    }

    [Fact]
    public async Task CompararLeituras_ResultadoNaoPositivoNumaLinha_FicaDeForaEAsOutrasComparam()
    {
        // A4 (beta.65): 15 g de água a 20 °C dão uma capacidade menor do que
        // punção − marisa (30 − 10): o peso calculado é negativo. Essa linha
        // fica de fora; a de 199,64 g compara-se (450,52 g).
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var resultado = await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId, "T3", 20m, new[]
            {
                new PesoComparacaoLeituraInput("7", 15m),
                new PesoComparacaoLeituraInput("12", 199.64m)
            }));

        Assert.Equal(PesoComparacaoResultado.Sucesso, resultado.Refusal);
        var leitura = Assert.Single(resultado.Leituras);
        Assert.Equal("12", leitura.CmSubject);
        Assert.Equal(1, leitura.RowPosition);
        Assert.True(leitura.GlassWeightGrams > 0m);
        var ignorada = Assert.Single(resultado.Ignoradas!);
        Assert.Equal(1, ignorada.Indice);
        Assert.Equal("7", ignorada.CmSubject);
        Assert.Equal(15m, ignorada.WaterWeightGrams);
        Assert.Equal(PesoComparacaoMotivoIgnorada.ResultadoNaoPositivo, ignorada.Motivo);
    }

    [Fact]
    public async Task Guardar_LeituraNaoPositiva_GravaAsOutrasEDevolveAsDeFora()
    {
        // A4 (beta.65): o Guardar grava as leituras utilizáveis; as de fora
        // não são gravadas e voltam no outcome para o aviso.
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var outcome = await _handler.HandleAsync(new CreatePesoComparacao(
            pesoInicialId, "T3", 20m, new[]
            {
                new PesoComparacaoLeituraInput("12", 199.64m),
                new PesoComparacaoLeituraInput("7", 15m),
                new PesoComparacaoLeituraInput("21", 0m)
            }));

        Assert.Equal(PesoComparacaoResultado.Sucesso, outcome.Result);
        Assert.NotNull(outcome.Comparacao);
        var gravada = Assert.Single(outcome.Comparacao!.Leituras);
        Assert.Equal("12", gravada.CmSubject);
        Assert.Equal(2, outcome.Ignoradas!.Count);
        Assert.Equal(1, await _db.PesoComparacoes.AsNoTracking().CountAsync());
        Assert.Equal(1, await _db.PesoComparacaoMeasurements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CompararLeituras_NenhumaLeituraUtilizavel_RecusaComAsDeFora()
    {
        // Sem nenhuma leitura utilizável não há nada a comparar nem a gravar:
        // a recusa fica, mas traz a lista para o aviso dizer quais.
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var resultado = await _query.CompararLeiturasAsync(new PesoComparacaoLeiturasPedido(
            pesoInicialId, "T3", 20m, new[]
            {
                new PesoComparacaoLeituraInput("12", 0m),
                new PesoComparacaoLeituraInput("21", 15m)
            }));

        Assert.Equal(PesoComparacaoResultado.ResultadoNaoPositivo, resultado.Refusal);
        Assert.Empty(resultado.Leituras);
        Assert.Equal(2, resultado.Ignoradas!.Count);
    }

    [Fact]
    public async Task ListarIniciais_OsPesosDaProducaoDoMaisAntigo()
    {
        var (jobOnId, _, pesoInicialId) = await SeedProducaoAsync();

        var opcoes = await _query.ListarIniciaisAsync(jobOnId);

        var unica = Assert.Single(opcoes);
        Assert.Equal(pesoInicialId, unica.PesoId);
        Assert.Equal("5447", unica.CmToolReference);
        Assert.Equal("T3", unica.CmToolId);
        Assert.Equal(2, unica.Leituras);
        Assert.Equal(325.39m, unica.PesoMedioGrams); // (450,52 + 200,26) / 2 — a média da casa
        Assert.Equal("1075C142", unica.Reference);
        Assert.Equal("202601", unica.ProductionNumber);
    }

    [Fact]
    public async Task Guardar_AMesmaComparacaoTresVezesSeguidas_FicaUmRegisto()
    {
        // B-peso-2 (deep sweep 18/10/2026): três cliques em «Guardar
        // comparação» com a rede lenta criavam três comparações iguais. Os
        // repetidos abrem a comparação do primeiro.
        var (_, _, pesoInicialId) = await SeedProducaoAsync();
        var command = new CreatePesoComparacao(
            pesoInicialId, "T3", 22m,
            new[] { new PesoComparacaoLeituraInput("76", 132.2m), new PesoComparacaoLeituraInput("33", 127.7m) },
            "Nota");

        var outcomes = new List<CreatePesoComparacaoOutcome>();
        for (var i = 0; i < 3; i++)
        {
            outcomes.Add(await _handler.HandleAsync(command));
        }

        Assert.All(outcomes, o => Assert.Equal(PesoComparacaoResultado.Sucesso, o.Result));
        Assert.Single(outcomes.Select(o => o.Comparacao!.ComparacaoId).Distinct());
        Assert.Equal(1, await _db.PesoComparacoes.AsNoTracking().CountAsync());
        Assert.Equal(2, await _db.PesoComparacaoMeasurements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Guardar_OutraLeituraOuOutraNota_CriaOutraComparacao()
    {
        var (_, _, pesoInicialId) = await SeedProducaoAsync();
        var leituras = new[] { new PesoComparacaoLeituraInput("76", 132.2m) };

        var primeira = await _handler.HandleAsync(new CreatePesoComparacao(pesoInicialId, "T3", 22m, leituras));
        var outraMassa = await _handler.HandleAsync(new CreatePesoComparacao(
            pesoInicialId, "T3", 22m, new[] { new PesoComparacaoLeituraInput("76", 132.3m) }));
        var outraNota = await _handler.HandleAsync(new CreatePesoComparacao(pesoInicialId, "T3", 22m, leituras, "Outra"));
        var maisUma = await _handler.HandleAsync(new CreatePesoComparacao(
            pesoInicialId, "T3", 22m, new[] { leituras[0], new PesoComparacaoLeituraInput("33", 127.7m) }, "Outra"));

        Assert.Equal(4, new[] { primeira, outraMassa, outraNota, maisUma }.Select(o => o.Comparacao!.ComparacaoId).Distinct().Count());
        Assert.Equal(4, await _db.PesoComparacoes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Guardar_AMesmaComparacaoDepoisDaJanela_CriaOutra()
    {
        // Medir outra vez mais tarde, com os mesmos valores, é outra
        // comparação: só o envio repetido logo a seguir é o mesmo clique.
        var (_, _, pesoInicialId) = await SeedProducaoAsync();
        var command = new CreatePesoComparacao(pesoInicialId, "T3", 22m, new[] { new PesoComparacaoLeituraInput("76", 132.2m) });

        var primeira = await _handler.HandleAsync(command);
        var guardada = await _db.PesoComparacoes.SingleAsync();
        guardada.CreatedAtUtc = DateTime.UtcNow - CreatePesoComparacaoHandler.RepeatedSubmitWindow - TimeSpan.FromSeconds(1);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var segunda = await _handler.HandleAsync(command);

        Assert.NotEqual(primeira.Comparacao!.ComparacaoId, segunda.Comparacao!.ComparacaoId);
        Assert.Equal(2, await _db.PesoComparacoes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Guardar_RecusaPreservaNada()
    {
        var (_, _, pesoInicialId) = await SeedProducaoAsync();

        var recusas = new[]
        {
            new CreatePesoComparacao(0, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }),
            new CreatePesoComparacao(pesoInicialId, "T3", 40m, new[] { new PesoComparacaoLeituraInput("12", 199.64m) }),
            new CreatePesoComparacao(pesoInicialId, "T3", 20m, Array.Empty<PesoComparacaoLeituraInput>()),
            new CreatePesoComparacao(pesoInicialId, "T3", 20m, new[] { new PesoComparacaoLeituraInput("12", 0m) })
        };

        foreach (var command in recusas)
        {
            var outcome = await _handler.HandleAsync(command);
            Assert.NotEqual(PesoComparacaoResultado.Sucesso, outcome.Result);
            Assert.Null(outcome.Comparacao);
        }

        Assert.Equal(0, await _db.PesoComparacoes.AsNoTracking().CountAsync());
    }
}
