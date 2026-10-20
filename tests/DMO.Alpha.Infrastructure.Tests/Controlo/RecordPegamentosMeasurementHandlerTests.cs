using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova o write path das medições de Pegamentos production-scoped: as
/// recusas tipadas (controlo inexistente, token desconhecido, componente
/// que o contexto da produção não tem — verificados antes de qualquer
/// escrita), o estado NotEvaluable sem bloqueio quando a tool não tem
/// gargalo e não é completado, a completion in-flow que grava no tool_id
/// owner sem nunca sobrescrever valores existentes, a tolerância
/// congelada no registo (uma alteração posterior da configuração não
/// reescreve as avaliações do registo — CONFIGURATION.md L47-51), o caso
/// normal de UM registo por controlo com reutilização do MESMO
/// pegamentos_id e o carácter append-only das leituras (as linhas
/// anteriores permanecem intactas).
/// </summary>
public sealed class RecordPegamentosMeasurementHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly RecordPegamentosMeasurementHandler _handler;

    public RecordPegamentosMeasurementHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new RecordPegamentosMeasurementHandler(
            _db,
            new PegamentosToleranceConfiguration(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<(int ControloId, string CmToolId, string BqToolId, string MfToolId)> SeedProductionAsync(
        decimal? cmGargalo = null)
    {
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "CM-X", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-X", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.Tools.Add(new Tool { ToolId = "MF-X", Type = ToolType.Mf, Reference = "5810", Lot = "1" });

        if (cmGargalo is { } gargalo)
        {
            _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
            {
                ToolId = "CM-X",
                DiametroGargaloMm = gargalo
            });
        }

        await _db.SaveChangesAsync();

        // A produção válida pela porta canónica: o CreateJobOn inclui
        // sempre o MfToolId (regra do Owner 2026-10-10 — o MF é
        // obrigatório) — o controlo_id nasce na mesma escrita.
        var machine = await _db.Machines.AsNoTracking().SingleAsync();
        var createJobOn = new CreateJobOnHandler(_db);
        var created = await createJobOn.HandleAsync(new CreateJobOn(
            "PN-1",
            "5810",
            machine.Id,
            "CM-X",
            "MF-X",
            "BQ-X"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        return (controlo.Id, "CM-X", "BQ-X", "MF-X");
    }

    /// <summary>
    /// Semeia uma produção LEGADA sem mf_id (o estado criável antes da
    /// regra do Owner 2026-10-10 «jobon necessita sempre de MF» — o
    /// CreateJobOn atual recusa-o, mas o dado antigo persiste) e devolve
    /// o seu controlo_id.
    /// </summary>
    private async Task<int> SeedLegacyProductionWithoutMfAsync()
    {
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "CM-X", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-X", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        await _db.SaveChangesAsync();

        var machine = await _db.Machines.AsNoTracking().SingleAsync();
        var jobOn = new JobOn { Reference = "5810", ProductionNumber = "PN-LEGACY", MachineId = machine.Id };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-X" });
        _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-X" });
        _db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn });
        await _db.SaveChangesAsync();

        return (await _db.Controlos.AsNoTracking().SingleAsync()).Id;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedAxes_AveragesUseActualRowResults_AndPreserveIndividualAlerts(bool reverse)
    {
        var production = await SeedProductionAsync(cmGargalo: 70m);
        var rows = new (decimal Costura, decimal? Contra)[] { (70.10m, 69.90m), (69.90m, 70.10m), (70.30m, null) };
        foreach (var row in reverse ? rows.Reverse() : rows)
        {
            var saved = await _handler.HandleAsync(new RecordPegamentosMeasurement(production.ControloId, "CM", row.Costura, row.Contra));
            Assert.Equal(RecordPegamentosMeasurementResult.Success, saved.Result);
        }
        var query = new PegamentosQuery(_db, new ProductionContextQuery(_db), new PegamentosToleranceConfiguration(_db));
        var sheet = (await query.FindSheetByControloAsync(production.ControloId))!;
        var avg = sheet.Pegamentos!.ComponentAverages.Single();
        Assert.Equal(0m, avg.AverageOvalizacaoMm); // +0.20 and -0.20; the single-axis row has no ovalization.
        Assert.Equal(70.10m, avg.AverageMediaMm); // Mean of 70.00, 70.00, 70.30.
        Assert.Equal(70.10m, avg.AverageCosturaMm);
        Assert.Equal(70m, avg.AverageContraCosturaMm);
        Assert.Equal(3, avg.SampleCount);
        // A good overall mean does not hide the 70.30 row; the ±0.20 ovalizations
        // reach the 0.20 tolerance and count too (Diogo, 19/10/2026, F8).
        Assert.Equal(3, avg.AlertCount);
        Assert.Null(sheet.Pegamentos.Measurements.Single(m => m.ContraCosturaMm is null).Evaluation.OvalizacaoMm);
        var pdf = PegamentosPdfGenerator.FromSheet(sheet);
        Assert.Equal(70.10m, pdf.Tables.Single(t => t.Component == "CM").AverageMediaMm);
        Assert.Equal(0m, pdf.Tables.Single(t => t.Component == "CM").AverageOvalizacaoMm);
    }

    [Fact]
    public async Task OvalizacaoAboveTolerance_CountsAsAlert_InTheSheetAndThePdf()
    {
        // Diogo, 19/10/2026 (F8): a ovalização acima da tolerância é um
        // alerta. Antes, só ficava a vermelho na página: a contagem, o
        // estado do PDF e a célula do PDF diziam que estava tudo dentro.
        var production = await SeedProductionAsync(cmGargalo: 70m);
        var saved = await _handler.HandleAsync(new RecordPegamentosMeasurement(production.ControloId, "CM", 70.11m, 69.89m));
        Assert.Equal(RecordPegamentosMeasurementResult.Success, saved.Result);

        var query = new PegamentosQuery(_db, new ProductionContextQuery(_db), new PegamentosToleranceConfiguration(_db));
        var sheet = (await query.FindSheetByControloAsync(production.ControloId))!;
        Assert.Equal(1, sheet.Pegamentos!.ComponentAverages.Single().AlertCount);

        var pdf = PegamentosPdfGenerator.FromSheet(sheet);
        var row = pdf.Tables.Single(t => t.Component == "CM").Rows.Single();
        Assert.True(row.IsAlert);
        Assert.True(row.OvalizacaoAlert);
        Assert.Contains("Existem medições que atingem ou ultrapassam os limites de tolerância", PdfText.Of(new PegamentosPdfGenerator().GenerateBytes(pdf)));
    }

    [Theory]
    [InlineData("CM", false)]
    [InlineData("BQ", false)]
    [InlineData("MF", false)]
    [InlineData("CM", true)]
    [InlineData("BQ", true)]
    [InlineData("MF", true)]
    public async Task HistoricalIdentity_UsesFrozenFacts_AndLegacyRecordsFallBackToCatalog(string component, bool legacy)
    {
        var production = await SeedProductionAsync();
        var toolId = component switch { "CM" => production.CmToolId, "BQ" => production.BqToolId, _ => production.MfToolId };
        var registered = await _handler.HandleAsync(new RecordPegamentosMeasurement(production.ControloId, component, 70.50m, 70m, 70m));
        Assert.Equal(RecordPegamentosMeasurementResult.Success, registered.Result);
        Assert.True(registered.Measurement!.Evaluation.HasAlert);
        var tool = await _db.Tools.SingleAsync(t => t.ToolId == toolId);
        var oldReference = tool.Reference;
        var oldLot = tool.Lot;
        if (legacy)
        {
            var record = await _db.Pegamentos.SingleAsync();
            if (component == "CM") { record.CmToolReferenceFrozen = null; record.CmToolLotFrozen = null; }
            if (component == "BQ") { record.BqToolReferenceFrozen = null; record.BqToolLotFrozen = null; }
            if (component == "MF") { record.MfToolReferenceFrozen = null; record.MfToolLotFrozen = null; }
            await _db.SaveChangesAsync();
        }

        var editor = new DMO.Alpha.Infrastructure.Tools.EditToolFactsHandler(_db);
        var edited = await editor.HandleAsync(new EditToolFacts(toolId, component, "REFERENCIA-ATUAL", "99"));
        Assert.Equal(EditToolFactsResult.Success, edited.Result);
        var query = new PegamentosQuery(_db, new ProductionContextQuery(_db), new PegamentosToleranceConfiguration(_db));
        var sheet = (await query.FindSheetByControloAsync(production.ControloId))!;
        var recordModel = sheet.Pegamentos!;
        var historicalReference = component switch { "CM" => recordModel.CmToolReference, "BQ" => recordModel.BqToolReference, _ => recordModel.MfToolReference };
        var historicalLot = component switch { "CM" => recordModel.CmToolLot, "BQ" => recordModel.BqToolLot, _ => recordModel.MfToolLot };
        Assert.Equal(legacy ? "REFERENCIA-ATUAL" : oldReference, historicalReference);
        Assert.Equal(legacy ? "99" : oldLot, historicalLot);
        var pdf = PegamentosPdfGenerator.FromSheet(sheet);
        var card = pdf.Components.Single(c => c.Component == component);
        Assert.Equal(historicalReference, card.Reference);
        Assert.Equal(historicalLot, card.Lot);
        Assert.True(recordModel.Measurements.Single().Evaluation.HasAlert);
        Assert.Equal("REFERENCIA-ATUAL", (await _db.Tools.SingleAsync(t => t.ToolId == toolId)).Reference);
    }

    [Fact]
    public async Task ControloNotFound_RefusesTyped_NoWrites()
    {
        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            999, "CM", 70.00m, null, 70.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.ControloNotFound, outcome.Result);
        Assert.Null(outcome.Measurement);

        // Recusa antes de qualquer escrita: nenhum registo, nenhuma
        // medição, nenhum valor técnico completado.
        Assert.Equal(0, await _db.Pegamentos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task UnknownComponentToken_RefusesTyped_NoWrites()
    {
        var (controloId, _, _, _) = await SeedProductionAsync();

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "XX", 70.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.UnknownComponent, outcome.Result);
        Assert.Equal(0, await _db.Pegamentos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task ComponentNotInProduction_MFRefusesTyped_ToolNeverInvented()
    {
        // Produção LEGADA sem mf_id (criada antes da regra do Owner
        // 2026-10-10 «jobon necessita sempre de MF» — o dado antigo
        // persiste): medir MF é recusa tipada — a tool do contexto nunca
        // é inventada nem re-selecionada.
        var controloId = await SeedLegacyProductionWithoutMfAsync();

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "MF", 59.00m, null, 59.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.ComponentNotInProduction, outcome.Result);
        Assert.Equal(0, await _db.Pegamentos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());
        // Nenhum gargalo foi completado numa tool que o contexto não tem.
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync(t => t.ToolId == "MF-X"));
    }

    [Fact]
    public async Task MissingGargaloWithoutCompletion_IsRefused_NothingWritten()
    {
        // Sem nominal do gargalo não há Pegamentos (Diogo, 17/10/2026): a
        // medição é recusada e nada é gravado, nem o registo nem a tool.
        var (controloId, _, _, _) = await SeedProductionAsync();

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.10m, 69.90m));

        Assert.Equal(RecordPegamentosMeasurementResult.MissingGargalo, outcome.Result);
        Assert.Null(outcome.Measurement);
        Assert.Equal(0, await _db.Pegamentos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync(t => t.ToolId == "CM-X"));
    }

    [Theory]
    [InlineData(0, 70)]
    [InlineData(-70, 70)]
    [InlineData(70, 0)]
    [InlineData(70, -1)]
    public async Task CosturaOrContraZeroOrLess_IsImpossible_NothingWritten(decimal costura, decimal contra)
    {
        // Uma costura ou contra costura de 0 ou menos é fisicamente
        // impossível (Diogo, 18/10/2026): nenhuma linha do envio fica.
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.00m, 70.00m,
            AdditionalRows: [new PegamentosRow(costura, contra)]));

        Assert.Equal(RecordPegamentosMeasurementResult.InvalidMeasurementValue, outcome.Result);
        Assert.Equal(0, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(0, 70)]       // costura no limite
    [InlineData(-0.01, 70)]   // costura logo abaixo
    [InlineData(70, 0)]       // contra costura no limite
    [InlineData(70, -0.01)]   // contra costura logo abaixo
    public async Task CosturaOuContraZeroOuMenosNaPrimeiraLinha_ERecusada_NadaFicaGravado(decimal costura, decimal contra)
    {
        // A regra vale também para a primeira linha do envio, não só para as
        // do «+ Linha»: nem a folha nem a leitura ficam.
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", costura, contra));

        Assert.Equal(RecordPegamentosMeasurementResult.InvalidMeasurementValue, outcome.Result);
        Assert.Null(outcome.Measurement);
        Assert.Equal(0, await _db.Pegamentos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(0.01, 70)]    // costura logo acima do limite
    [InlineData(70, 0.01)]    // contra costura logo acima do limite
    public async Task CosturaOuContraDe001NaPrimeiraLinha_EAceite_FicaGravada(decimal costura, decimal contra)
    {
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", costura, contra));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, outcome.Result);
        var row = await _db.PegamentosMeasurements.AsNoTracking().SingleAsync();
        Assert.Equal(costura, row.CosturaMm);
        Assert.Equal(contra, row.ContraCosturaMm);
    }

    [Theory]
    [InlineData(0, 70)]       // costura no limite, contra válida
    [InlineData(-0.01, 70)]   // costura logo abaixo
    [InlineData(70, 0)]       // contra costura no limite, costura válida
    [InlineData(70, -0.01)]   // contra costura logo abaixo
    public async Task CosturaOuContraZeroOuMenosNaCorrecao_ERecusada_ALeituraFicaIgual(decimal costura, decimal contra)
    {
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);
        var saved = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.10m, 69.90m));
        var anchor = new PegamentosMeasurementAnchor(saved.Measurement!.MeasurementId, controloId, null);

        var result = await new CorrectPegamentosMeasurementHandler(_db).EditAsync(
            new EditPegamentosMeasurement(anchor, costura, contra));

        Assert.Equal(CorrectPegamentosMeasurementResult.ImpossibleValue, result);
        _db.ChangeTracker.Clear();
        var row = await _db.PegamentosMeasurements.AsNoTracking().SingleAsync();
        Assert.Equal(70.10m, row.CosturaMm);
        Assert.Equal(69.90m, row.ContraCosturaMm);
    }

    [Theory]
    [InlineData(0.01, 69.90)]   // costura logo acima do limite
    [InlineData(70.10, 0.01)]   // contra costura logo acima do limite
    [InlineData(70.10, null)]   // contra costura n/a (eixo único)
    public async Task CosturaOuContraDe001NaCorrecao_EAceite_ALeituraFicaComOValorNovo(decimal costura, double? contraOuNada)
    {
        var contra = (decimal?)contraOuNada; // o InlineData não converte double em decimal?
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);
        var saved = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.20m, 69.80m));
        var anchor = new PegamentosMeasurementAnchor(saved.Measurement!.MeasurementId, controloId, null);

        var result = await new CorrectPegamentosMeasurementHandler(_db).EditAsync(
            new EditPegamentosMeasurement(anchor, costura, contra));

        Assert.Equal(CorrectPegamentosMeasurementResult.Success, result);
        _db.ChangeTracker.Clear();
        var row = await _db.PegamentosMeasurements.AsNoTracking().SingleAsync();
        Assert.Equal(costura, row.CosturaMm);
        Assert.Equal(contra, row.ContraCosturaMm);
    }

    [Fact]
    public async Task Correction_ToZeroOrLess_IsImpossible_ReadingKeepsItsValues()
    {
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);
        var saved = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.10m, 69.90m));
        var anchor = new PegamentosMeasurementAnchor(saved.Measurement!.MeasurementId, controloId, null);
        var correct = new CorrectPegamentosMeasurementHandler(_db);

        Assert.Equal(
            CorrectPegamentosMeasurementResult.ImpossibleValue,
            await correct.EditAsync(new EditPegamentosMeasurement(anchor, 0m, 70m)));
        Assert.Equal(
            CorrectPegamentosMeasurementResult.ImpossibleValue,
            await correct.EditAsync(new EditPegamentosMeasurement(anchor, 70m, -0.5m)));

        var row = await _db.PegamentosMeasurements.AsNoTracking().SingleAsync();
        Assert.Equal(70.10m, row.CosturaMm);
        Assert.Equal(69.90m, row.ContraCosturaMm);
    }

    [Fact]
    public async Task Completion_NeverOverwritesExistingToolValue()
    {
        // A tool JÁ tem gargalo 70.00: um valor fornecido no fluxo (99.99)
        // é IGNORADO (completion, não correção silenciosa da Tool) e a
        // linha congela o nominal da TOOL (70.00).
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.10m, 69.90m, 99.99m));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, outcome.Result);
        Assert.Equal(70.00m, outcome.Measurement!.NominalGargaloMm);

        var cmTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "CM-X");
        Assert.Equal(70.00m, cmTechnical.DiametroGargaloMm);
    }

    [Fact]
    public async Task ConfiguredTolerance_FrozenAtFirstMeasurement_LaterConfigChangeDoesNotRewrite()
    {
        // A tolerância configurada (±0,30) substitui o default no momento da
        // criação do registo e fica CONGELADA: depois de a configuração
        // passar para ±0,50, a medição seguinte NO MESMO registo continua
        // a avaliar com ±0,30 (70.35 está fora do corredor ±0,30 mas dentro
        // de ±0,50 — o alerta prova a tolerância congelada).
        _db.PegamentosTolerances.Add(new PegamentosTolerance { ToleranceMm = 0.30m });
        await _db.SaveChangesAsync();

        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);

        var first = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.10m, null));
        Assert.Equal(RecordPegamentosMeasurementResult.Success, first.Result);

        var registro = await _db.Pegamentos.AsNoTracking().SingleAsync();
        Assert.Equal(0.30m, registro.ToleranceMm);

        // A configuração muda (a linha singleton é atualizada).
        var configuration = await _db.PegamentosTolerances.SingleAsync();
        configuration.ToleranceMm = 0.50m;
        await _db.SaveChangesAsync();

        var second = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.35m, null));
        Assert.Equal(RecordPegamentosMeasurementResult.Success, second.Result);

        // O MESMO registo (nunca um segundo criado em silêncio) e a
        // tolerância dele continua congelada em 0,30.
        Assert.Equal(registro.Id, second.Measurement!.PegamentosId);
        Assert.Equal(0.30m, second.Measurement.Evaluation.CorridorUpperMm - 70.00m);
        // 70.35 >= 70.30 (limite com ±0,30) → alerta; com ±0,50 (70.50)
        // estaria dentro — o alerta prova a avaliação histórica.
        Assert.True(second.Measurement.Evaluation.HasAlert);

        // A configuração corrente (0,50) continua a ser a efetiva para
        // registos NOVOS — nunca locked no cálculo.
        var freshSheet = new PegamentosQuery(
            _db,
            new ProductionContextQuery(_db),
            new PegamentosToleranceConfiguration(_db));
        var otherProduction = await SeedSecondProductionAsync();
        var sheet = await freshSheet.FindSheetByControloAsync(otherProduction);
        Assert.NotNull(sheet);
        Assert.Equal(0.50m, sheet!.CurrentToleranceMm);
    }

    [Fact]
    public async Task SameControlo_ReusesSingleRegistro_AppendOnlyKeepsEarlierRows()
    {
        // O caso normal da spec é UM registo por produção: medições
        // sucessivas usam o MESMO pegamentos_id e as linhas anteriores
        // permanecem factos históricos intactos (append-only).
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);

        var first = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.10m, 69.90m));
        var second = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 69.95m, 70.05m));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, first.Result);
        Assert.Equal(RecordPegamentosMeasurementResult.Success, second.Result);
        Assert.Equal(first.Measurement!.PegamentosId, second.Measurement!.PegamentosId);

        Assert.Equal(1, await _db.Pegamentos.AsNoTracking().CountAsync());
        Assert.Equal(2, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());

        // A 1.ª linha continua exatamente como foi observada: ovalização
        // +0,20 com sinal preservado (a 2.ª linha tem −0,10, um fato
        // distinto), média 70.00, nominal congelado 70.00.
        var firstRow = await _db.PegamentosMeasurements.AsNoTracking()
            .SingleAsync(m => m.RowPosition == 1);
        Assert.Equal(70.10m, firstRow.CosturaMm);
        Assert.Equal(69.90m, firstRow.ContraCosturaMm);
        Assert.Equal(70.00m, firstRow.NominalGargaloMm);

        var secondRow = await _db.PegamentosMeasurements.AsNoTracking()
            .SingleAsync(m => m.RowPosition == 2);
        Assert.Equal(-0.10m, secondRow.CosturaMm - secondRow.ContraCosturaMm!.Value);
    }

    [Fact]
    public async Task RegistroFreezesCombinationToolsFromContextAtCreation()
    {
        // O registo congela as tools da combinação herdadas do contexto no
        // momento da criação (evidência histórica): CM/BQ/MF presentes —
        // o MF é obrigatório em todo o Job On (regra do Owner 2026-10-10).
        var (controloId, cmToolId, bqToolId, mfToolId) = await SeedProductionAsync(cmGargalo: 70.00m);

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.00m));
        Assert.Equal(RecordPegamentosMeasurementResult.Success, outcome.Result);

        var registro = await _db.Pegamentos.AsNoTracking().SingleAsync();
        Assert.Equal(cmToolId, registro.CmToolId);
        Assert.Equal(bqToolId, registro.BqToolId);
        Assert.Equal(mfToolId, registro.MfToolId);
    }

    [Fact]
    public async Task WithoutConfiguredTolerance_DefaultOwnerToleranceApplies()
    {
        // Sem configuração registada, o default do Owner ±0,20 é congelado
        // no registo (nunca locked: a configuração substitui-o quando
        // existir).
        var (controloId, _, _, _) = await SeedProductionAsync(cmGargalo: 70.00m);

        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.00m));

        var registro = await _db.Pegamentos.AsNoTracking().SingleAsync();
        Assert.Equal(PegamentosCalculation.DefaultToleranceMm, registro.ToleranceMm);
        Assert.Equal(69.80m, outcome.Measurement!.Evaluation.CorridorLowerMm);
        Assert.Equal(70.20m, outcome.Measurement.Evaluation.CorridorUpperMm);
    }

    /// <summary>Semeia uma segunda produção independente e devolve o seu controlo_id.</summary>
    private async Task<int> SeedSecondProductionAsync()
    {
        _db.Machines.Add(new Machine { Code = "B2" });
        _db.Tools.Add(new Tool { ToolId = "CM-Y", Type = ToolType.Cm, Reference = "5809", Lot = "1" });
        _db.Tools.Add(new Tool { ToolId = "MF-Y", Type = ToolType.Mf, Reference = "5774", Lot = "1" });
        _db.Tools.Add(new Tool { ToolId = "BQ-Y", Type = ToolType.Bq, Reference = "T173", Lot = "24" });
        await _db.SaveChangesAsync();

        var machine = await _db.Machines.AsNoTracking().SingleAsync(m => m.Code == "B2");
        var createJobOn = new CreateJobOnHandler(_db);
        var created = await createJobOn.HandleAsync(new CreateJobOn(
            "PN-2",
            "5774",
            machine.Id,
            "CM-Y",
            "MF-Y",
            "BQ-Y"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);

        return (await _db.Controlos.AsNoTracking()
            .Where(c => c.JobOnId == created.JobOn!.JobOnId)
            .SingleAsync()).Id;
    }
}
