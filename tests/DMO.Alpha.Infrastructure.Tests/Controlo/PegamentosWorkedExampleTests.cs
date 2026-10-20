using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O CASO DE ACEITAÇÃO do corte Pegamentos production-scoped — o exemplo
/// trabalhado da BASE REAL (o estado ao vivo que o Owner confirma):
/// Job On J10 · referência 5810 · máquina B1 · cm_id → T3 (CM 5447 lote 3,
/// NNPB) · bq_id → BQ-1 (BQ T194 12) · mf_id → MF-TOOL-9 (MF 5810 lote 1 —
/// o molde final OBRIGATÓRIO, regra do Owner 2026-10-10: «jobon necessita
/// sempre de MF, senao nao faz garrafas ou frascos»; a referência do
/// desenho dá a base 4-dígitos da referência da produção) · controlo_id 1
/// (backfill idempotente). A LEI da app de ponta a ponta: jobon nasce o
/// controlo_id, com controlo_id ganha-se o contexto todo — a folha herda
/// T3, BQ-1 e MF-TOOL-9 do contexto e o operador NUNCA seleciona
/// ferramentas. O gargalo de cada tool está em falta na base real (o MF
/// SÓ tem o gargalo como valor técnico — regra do Owner) e é completado
/// no fluxo da folha, gravando no tool_id owner (nunca no registo de
/// Pegamentos). O gesto em cruz em dois eixos, o eixo único da BQ, o
/// alerta informativo no limite, o estado NotEvaluable e a tolerância
/// congelada — tudo verificado sobre os factos persistidos.
/// </summary>
public sealed class PegamentosWorkedExampleTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _createJobOn;
    private readonly RecordPegamentosMeasurementHandler _recordHandler;
    private readonly PegamentosQuery _pegamentosQuery;
    private readonly IPegamentosToleranceConfiguration _toleranceConfiguration;

    public PegamentosWorkedExampleTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _createJobOn = new CreateJobOnHandler(_db);
        _toleranceConfiguration = new PegamentosToleranceConfiguration(_db);
        _recordHandler = new RecordPegamentosMeasurementHandler(_db, _toleranceConfiguration);
        _pegamentosQuery = new PegamentosQuery(
            _db,
            new ProductionContextQuery(_db),
            _toleranceConfiguration);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task WorkedExample_J10_5810_B1_T3_BQ1_FolhaCompleta()
    {
        // --- Pré-condições da base real: a máquina B1, as Tools canónicas
        //     T3 (CM 5447 lote 3, processo NNPB), BQ-1 (BQ T194 12) e
        //     MF-TOOL-9 (MF 5810 lote 1 — o desenho); a T3 tem contexto
        //     técnico (o corte do Peso registou o processo) mas NENHUMA
        //     delas tem diametro_gargalo (a coluna nova nasce NULL — o MF
        //     só tem o gargalo como valor técnico). Nenhuma tolerância
        //     configurada: aplica-se o default do Owner ±0,20.
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb
        });
        await _db.SaveChangesAsync();

        // --- Job On J10: a produção real com o contexto estabelecido
        //     (cm_id → T3, bq_id → BQ-1, mf_id → MF-TOOL-9 — o MF
        //     obrigatório do desenho, regra do Owner 2026-10-10) e o
        //     controlo_id assegurado na MESMA escrita (spec Job On L33).
        var created = await _createJobOn.HandleAsync(new CreateJobOn(
            "J10",
            "5810",
            _db.Machines.AsNoTracking().Single().Id,
            "T3",
            "MF-TOOL-9",
            "BQ-1"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        Assert.Equal(created.JobOn!.JobOnId, controlo.JobOnId);

        // --- Abrir a folha por controlo_id (a LEI: com controlo_id ganha o
        //     contexto todo): o contexto canónico da produção composto no
        //     momento da leitura, as tools HERDADAS (nunca selecionadas),
        //     os gargalos em falta e AINDA SEM registo de Pegamentos — a
        //     ausência é o estado válido.
        var sheet = await _pegamentosQuery.FindSheetByControloAsync(controlo.Id);
        Assert.NotNull(sheet);
        Assert.Equal(controlo.Id, sheet!.ControloId);
        Assert.Equal(created.JobOn.JobOnId, sheet.JobOnId);
        Assert.Equal("5810", sheet.Reference);
        Assert.Equal("J10", sheet.ProductionNumber);
        Assert.Equal("B1", sheet.Machine);
        Assert.Equal("T3", sheet.CmToolId);
        Assert.Equal("BQ-1", sheet.BqToolId);
        Assert.Equal("MF-TOOL-9", sheet.MfToolId);

        // Só os componentes aplicáveis do contexto têm card: CM, BQ e o
        // MF obrigatório do J10 — os três presentes, nenhum inventado.
        Assert.Equal(3, sheet.Components.Count);
        var cmComponent = sheet.Components.Single(c => c.Component == PegamentosComponent.Cm);
        Assert.Equal("T3", cmComponent.ToolId);
        Assert.Equal("5447", cmComponent.ToolReference);
        Assert.Equal("3", cmComponent.ToolLot);
        Assert.Null(cmComponent.DiametroGargaloMm);
        var bqComponent = sheet.Components.Single(c => c.Component == PegamentosComponent.Bq);
        Assert.Equal("BQ-1", bqComponent.ToolId);
        Assert.Equal("T194", bqComponent.ToolReference);
        Assert.Null(bqComponent.DiametroGargaloMm);
        var mfComponent = sheet.Components.Single(c => c.Component == PegamentosComponent.Mf);
        Assert.Equal("MF-TOOL-9", mfComponent.ToolId);
        Assert.Equal("5810", mfComponent.ToolReference);
        Assert.Equal("1", mfComponent.ToolLot);
        Assert.Null(mfComponent.DiametroGargaloMm);

        // Sem configuração registada: a tolerância efetiva corrente é o
        // default do Owner.
        Assert.Equal(0.20m, sheet.CurrentToleranceMm);
        Assert.Null(sheet.Pegamentos);

        // --- 1.ª medição — a CM em cruz (costura 70.10, contra costura
        //     69.90) com o gargalo da T3 completado no fluxo (70.00):
        //     gravado no tool_id OWNER, nunca no registo de Pegamentos.
        var first = await _recordHandler.HandleAsync(new RecordPegamentosMeasurement(
            controlo.Id, "CM", 70.10m, 69.90m, 70.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, first.Result);
        var firstMeasurement = first.Measurement!;
        Assert.Equal(1, firstMeasurement.PegamentosId);
        Assert.Equal(1, firstMeasurement.RowPosition);
        Assert.Equal(PegamentosComponent.Cm, firstMeasurement.Component);
        Assert.Equal(70.00m, firstMeasurement.NominalGargaloMm);

        // O cálculo backend: ovalização +0,20 (sinal preservado), média
        // 70.00, corredor [69.80, 70.20] — os eixos e a média dentro; a
        // ovalização toca a tolerância 0,20 e é alerta (Diogo, 19/10/2026, F8).
        Assert.Equal(0.20m, firstMeasurement.Evaluation.OvalizacaoMm);
        Assert.Equal(70.00m, firstMeasurement.Evaluation.MediaMm);
        Assert.Equal(69.80m, firstMeasurement.Evaluation.CorridorLowerMm);
        Assert.Equal(70.20m, firstMeasurement.Evaluation.CorridorUpperMm);
        Assert.True(firstMeasurement.Evaluation.HasAlert);
        Assert.DoesNotContain(firstMeasurement.Evaluation.Values, v => v.Axis != PegamentosEvaluatedAxis.Ovalizacao && v.Alert);

        // A completion gravou no tool_id owner (regra Ferramentas/spec
        // L184): a T3 ficou com diametro_gargalo 70.00 reutilizável; o
        // registo de Pegamentos NÃO é um segundo dono do facto.
        var t3Technical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "T3");
        Assert.Equal(70.00m, t3Technical.DiametroGargaloMm);
        Assert.Equal(CmProcess.Nnpb, t3Technical.Process);

        // O registo durável nasceu da primeira medição: tools da combinação
        // herdadas do contexto (o MF-TOOL-9 do desenho congelado com os
        // seus factos humanos) e a tolerância efetiva congelada (default
        // ±0,20 — nenhuma configuração existia).
        var registro = await _db.Pegamentos.AsNoTracking().SingleAsync();
        Assert.Equal(controlo.Id, registro.ControloId);
        Assert.Equal("T3", registro.CmToolId);
        Assert.Equal("BQ-1", registro.BqToolId);
        Assert.Equal("MF-TOOL-9", registro.MfToolId);
        Assert.Equal("5810", registro.MfToolReferenceFrozen);
        Assert.Equal("1", registro.MfToolLotFrozen);
        Assert.Equal(0.20m, registro.ToleranceMm);

        // --- 2.ª medição — a CM no limite (costura 70.20 = limite
        //     superior do corredor): igualdade no limite cria ALERTA
        //     informativo (manual L350-352) — a app é de registo, nunca
        //     bloqueia.
        var second = await _recordHandler.HandleAsync(new RecordPegamentosMeasurement(
            controlo.Id, "CM", 70.20m, 69.80m));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, second.Result);
        Assert.Equal(firstMeasurement.PegamentosId, second.Measurement!.PegamentosId); // o MESMO registo do controlo
        Assert.Equal(2, second.Measurement.RowPosition);
        Assert.Equal(0.40m, second.Measurement.Evaluation.OvalizacaoMm);
        Assert.Equal(70.00m, second.Measurement.Evaluation.MediaMm);
        Assert.True(second.Measurement.Evaluation.HasAlert);
        Assert.Contains(second.Measurement.Evaluation.Values, v =>
            v.Axis == PegamentosEvaluatedAxis.Costura && v.Alert);

        // --- 3.ª medição — a BQ em eixo único (a geometria real não
        //     permite o 90°): contra costura NULL, ovalização indefinida,
        //     média = costura; o gargalo da BQ-1 (60.00) completado no
        //     fluxo gravou no tool_id owner.
        var third = await _recordHandler.HandleAsync(new RecordPegamentosMeasurement(
            controlo.Id, "BQ", 59.90m, null, 60.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, third.Result);
        Assert.Equal(firstMeasurement.PegamentosId, third.Measurement!.PegamentosId);
        Assert.Equal(3, third.Measurement.RowPosition);
        Assert.Equal(PegamentosComponent.Bq, third.Measurement.Component);
        Assert.Null(third.Measurement.ContraCosturaMm);
        Assert.Equal(60.00m, third.Measurement.NominalGargaloMm);
        Assert.Null(third.Measurement.Evaluation.OvalizacaoMm);
        Assert.Equal(59.90m, third.Measurement.Evaluation.MediaMm);
        Assert.Equal(59.80m, third.Measurement.Evaluation.CorridorLowerMm);
        Assert.Equal(60.20m, third.Measurement.Evaluation.CorridorUpperMm);
        Assert.False(third.Measurement.Evaluation.HasAlert);

        var bqTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "BQ-1");
        Assert.Equal(60.00m, bqTechnical.DiametroGargaloMm);

        // --- MF: o molde final do desenho (obrigatório na criação, regra
        //     do Owner 2026-10-10): a medição em cruz com o gargalo
        //     completado no fluxo (59.00) grava no tool_id OWNER
        //     MF-TOOL-9 — o facto técnico é da ferramenta (o MF SÓ tem o
        //     gargalo como valor técnico), nunca do registo.
        var fourth = await _recordHandler.HandleAsync(new RecordPegamentosMeasurement(
            controlo.Id, "MF", 59.10m, 58.90m, 59.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, fourth.Result);
        Assert.Equal(firstMeasurement.PegamentosId, fourth.Measurement!.PegamentosId);
        Assert.Equal(4, fourth.Measurement.RowPosition);
        Assert.Equal(PegamentosComponent.Mf, fourth.Measurement.Component);
        Assert.Equal(59.00m, fourth.Measurement.NominalGargaloMm);

        var mfTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "MF-TOOL-9");
        Assert.Equal(59.00m, mfTechnical.DiametroGargaloMm);

        // A folha reaberta: o registo com as quatro leituras pela ordem de
        // registo (factos históricos append-only — a 1.ª linha intacta),
        // a tolerância congelada e as médias por componente.
        var reopened = await _pegamentosQuery.FindSheetByControloAsync(controlo.Id);
        Assert.NotNull(reopened);
        Assert.NotNull(reopened!.Pegamentos);
        Assert.Equal(0.20m, reopened.Pegamentos!.ToleranceMm);
        Assert.Equal(4, reopened.Pegamentos.Measurements.Count);

        var cmFirst = reopened.Pegamentos.Measurements[0];
        Assert.Equal(70.10m, cmFirst.CosturaMm);
        Assert.Equal(69.90m, cmFirst.ContraCosturaMm);
        Assert.Equal(70.00m, cmFirst.NominalGargaloMm);
        Assert.Equal(0.20m, cmFirst.Evaluation.OvalizacaoMm);
        Assert.True(cmFirst.Evaluation.HasAlert); // a ovalização toca a tolerância (F8)

        var cmSecond = reopened.Pegamentos.Measurements[1];
        Assert.True(cmSecond.Evaluation.HasAlert);

        var bqThird = reopened.Pegamentos.Measurements[2];
        Assert.Null(bqThird.Evaluation.OvalizacaoMm);
        Assert.False(bqThird.Evaluation.IsNotEvaluable);

        var mfFourth = reopened.Pegamentos.Measurements[3];
        Assert.Equal(PegamentosComponent.Mf, mfFourth.Component);
        Assert.Equal(59.00m, mfFourth.NominalGargaloMm);
        Assert.Equal(0.20m, mfFourth.Evaluation.OvalizacaoMm);
        Assert.Equal(59.00m, mfFourth.Evaluation.MediaMm);
        Assert.True(mfFourth.Evaluation.HasAlert); // a ovalização toca a tolerância (F8)

        // Médias por componente (padrão do gesto em cruz): CM — média das
        // costuras 70.15, média das contra costuras 69.85, ovalização média
        // +0.30, média da média 70.00, 2 alertas em 2 amostras (a 1.ª pela
        // ovalização +0,20, F8); BQ — eixo único, média 59.90.
        var cmAverages = reopened.Pegamentos.ComponentAverages.Single(a => a.Component == PegamentosComponent.Cm);
        Assert.Equal(2, cmAverages.SampleCount);
        Assert.Equal(2, cmAverages.AlertCount);
        Assert.Equal(0, cmAverages.NotEvaluableCount);
        Assert.Equal(70.15m, cmAverages.AverageCosturaMm);
        Assert.Equal(69.85m, cmAverages.AverageContraCosturaMm);
        Assert.Equal(0.30m, cmAverages.AverageOvalizacaoMm);
        Assert.Equal(70.00m, cmAverages.AverageMediaMm);

        var bqAverages = reopened.Pegamentos.ComponentAverages.Single(a => a.Component == PegamentosComponent.Bq);
        Assert.Equal(1, bqAverages.SampleCount);
        Assert.Equal(59.90m, bqAverages.AverageCosturaMm);
        Assert.Null(bqAverages.AverageContraCosturaMm);
        Assert.Null(bqAverages.AverageOvalizacaoMm);
        Assert.Equal(59.90m, bqAverages.AverageMediaMm);

        // MF — o gesto em cruz completo do molde final: média das costuras
        // 59.10, média das contra costuras 58.90, ovalização média +0.20,
        // média da média 59.00.
        var mfAverages = reopened.Pegamentos.ComponentAverages.Single(a => a.Component == PegamentosComponent.Mf);
        Assert.Equal(1, mfAverages.SampleCount);
        Assert.Equal(59.10m, mfAverages.AverageCosturaMm);
        Assert.Equal(58.90m, mfAverages.AverageContraCosturaMm);
        Assert.Equal(0.20m, mfAverages.AverageOvalizacaoMm);
        Assert.Equal(59.00m, mfAverages.AverageMediaMm);

        // Os gargalos completados ficaram visíveis como facto corrente das
        // tools (reutilizável por folhas futuras) — as componentes da folha
        // já não os mostram em falta (T3, BQ-1 e o MF-TOOL-9 do desenho).
        Assert.Equal(70.00m, reopened.Components.Single(c => c.Component == PegamentosComponent.Cm).DiametroGargaloMm);
        Assert.Equal(60.00m, reopened.Components.Single(c => c.Component == PegamentosComponent.Bq).DiametroGargaloMm);
        Assert.Equal(59.00m, reopened.Components.Single(c => c.Component == PegamentosComponent.Mf).DiametroGargaloMm);

        // Nenhum segundo registo do controlo foi criado em silêncio: o caso
        // normal é um registo por produção (o adicional EXPLÍCITO é slice
        // futuro).
        Assert.Equal(1, await _db.Pegamentos.AsNoTracking().CountAsync(p => p.ControloId == controlo.Id));
        Assert.Equal(4, await _db.PegamentosMeasurements.AsNoTracking().CountAsync());
    }
}
