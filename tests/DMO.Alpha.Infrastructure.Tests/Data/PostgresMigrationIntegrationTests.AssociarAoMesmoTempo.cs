using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Associar ao mesmo tempo (deep sweep, 18/10/2026, I3-M-8): nas Boquilhas,
/// no Peso e nas Punções, dois pedidos liam o registo ainda por associar,
/// verificavam e gravavam os dois; ficava a última gravação e os dois
/// recebiam sucesso. Nos Pegamentos (I3-M-2), associar uma folha enquanto
/// outra era associada à mesma produção, ou enquanto o operador gravava a
/// 1.ª leitura, deixava duas folhas no mesmo controlo. O outro pedido parte
/// quando este já leu e vai gravar.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    // O outro pedido acaba logo (sem trinco) ou fica à espera deste até ao
    // prazo (com o trinco, só passa depois de este gravar).
    private static readonly TimeSpan AssociarPrazo = TimeSpan.FromSeconds(1.5);

    /// <summary>Corre o outro pedido, por outra ligação, antes da primeira gravação deste.</summary>
    private sealed class OutroPedido<T>(Func<DmoDbContext, Task<T>> pedido)
    {
        public Task<T>? Tarefa { get; private set; }

        public BeforeFirstSave AntesDeGravar() => new(async () =>
        {
            Tarefa = Task.Run(async () =>
            {
                await using var db = CreateContext();
                return await pedido(db);
            });
            await Task.WhenAny(Tarefa, Task.Delay(AssociarPrazo));
        });
    }

    [SkippableFact]
    public async Task Boquilhas_AssociarOMesmoRegistoADuasProducoesAoMesmoTempo_FicaSoAPrimeira()
    {
        SkipWithoutConnection();
        var seed = await SeedBqAssociacaoAsync();
        try
        {
            var outro = new OutroPedido<AssociateBqRepairTraceToContextOutcome>(db =>
                new AssociateBqRepairTraceToContextHandler(db).HandleAsync(
                    new AssociateBqRepairTraceToContext(seed.TraceId, seed.Context2)));
            await using var mine = CreateContextWith(outro.AntesDeGravar());

            var este = await new AssociateBqRepairTraceToContextHandler(mine).HandleAsync(
                new AssociateBqRepairTraceToContext(seed.TraceId, seed.Context1));
            var outroResultado = await outro.Tarefa!;

            Assert.Equal(AssociateBqRepairTraceToContextResult.Success, este.Result);
            Assert.Equal(AssociateBqRepairTraceToContextResult.TraceAlreadyAssociated, outroResultado.Result);
            await using var check = CreateContext();
            Assert.Equal(seed.Context1, await check.BqRepairTraces.AsNoTracking()
                .Where(t => t.Id == seed.TraceId).Select(t => t.BqContextId).SingleAsync());
        }
        finally
        {
            await DeleteBqAssociacaoAsync(seed);
        }
    }

    /// <summary>
    /// Associar o registo pré-produção à produção N1 enquanto outra pessoa
    /// grava o 1.º movimento da produção N2 com a mesma BQ: o movimento
    /// adotava o registo e a associação gravava por cima, ficando o movimento
    /// de N2 no registo de N1 e N2 sem registo.
    /// </summary>
    [SkippableFact]
    public async Task Boquilhas_AssociarEnquantoOutraProducaoGravaOPrimeiroMovimento_CadaUmaFicaComOSeuRegisto()
    {
        SkipWithoutConnection();
        var seed = await SeedBqAssociacaoAsync();
        try
        {
            var outro = new OutroPedido<RegisterBqMovementOutcome>(db =>
                new RegisterBqMovementHandler(db, new NoRouteSettings()).HandleForContextAsync(
                    new RegisterBqMovementForContext(seed.Context2, "saida", 7)));
            await using var mine = CreateContextWith(outro.AntesDeGravar());

            var associar = await new AssociateBqRepairTraceToContextHandler(mine).HandleAsync(
                new AssociateBqRepairTraceToContext(seed.TraceId, seed.Context1));
            var movimento = await outro.Tarefa!;

            Assert.Equal(AssociateBqRepairTraceToContextResult.Success, associar.Result);
            Assert.Equal(RegisterBqMovementResult.Success, movimento.Result);
            await using var check = CreateContext();
            var registos = await check.BqRepairTraces.AsNoTracking()
                .Where(t => t.BqContextId == seed.Context1 || t.BqContextId == seed.Context2)
                .Select(t => new
                {
                    t.Id,
                    t.BqContextId,
                    Quantidades = check.BqMovements.Where(m => m.BqRepairTraceId == t.Id).Select(m => m.Quantity).ToList()
                })
                .ToListAsync();
            var n1 = Assert.Single(registos, t => t.BqContextId == seed.Context1);
            Assert.Equal(seed.TraceId, n1.Id);
            Assert.Empty(n1.Quantidades);
            var n2 = Assert.Single(registos, t => t.BqContextId == seed.Context2);
            Assert.NotEqual(seed.TraceId, n2.Id);
            Assert.Equal(new[] { 7 }, n2.Quantidades);
        }
        finally
        {
            await DeleteBqAssociacaoAsync(seed);
        }
    }

    private sealed record BqAssociacao(string Bq, int Machine1, int Machine2, int Context1, int Context2, int TraceId);

    /// <summary>Duas produções com a mesma BQ e um registo pré-produção dessa BQ por associar.</summary>
    private static async Task<BqAssociacao> SeedBqAssociacaoAsync()
    {
        var marker = "AS" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var bq = "BQ-" + marker;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var (m1, j1) = await SeedJobOnAsync(db, marker + "A");
        var (m2, j2) = await SeedJobOnAsync(db, marker + "B");
        db.Tools.Add(new Tool { ToolId = bq, Type = ToolType.Bq, Reference = "9389T194", Lot = "1" });
        await db.SaveChangesAsync();
        var context1 = new BqContext { JobOnId = j1, ToolId = bq };
        var context2 = new BqContext { JobOnId = j2, ToolId = bq };
        db.BqContexts.AddRange(context1, context2);
        var preProduction = new BqRepairTrace { ToolId = bq, ToolReferenceFrozen = "9389T194", ToolLotFrozen = "1" };
        db.BqRepairTraces.Add(preProduction);
        await db.SaveChangesAsync();
        return new BqAssociacao(bq, m1, m2, context1.Id, context2.Id, preProduction.Id);
    }

    [SkippableFact]
    public async Task Peso_AssociarOMesmoPesoADuasProducoesAoMesmoTempo_FicaSoAPrimeira()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            int pesoId;
            await using (var db = CreateContext())
            {
                var peso = new Peso
                {
                    CmToolId = producoes.Marker + "-CM",
                    BqToolId = producoes.Marker + "-BQ",
                    Process = "NNPB",
                    WaterTemperatureC = 20,
                    VolumeMarisaCm3 = 10m,
                    VolumePuncaoCm3 = 30m,
                    GlassDensityGramsPerCm3 = 2.50m
                };
                db.Pesos.Add(peso);
                await db.SaveChangesAsync();
                pesoId = peso.Id;
            }

            var outro = new OutroPedido<AssociatePesoToControloOutcome>(db =>
                new AssociatePesoToControloHandler(db).HandleAsync(new AssociatePesoToControlo(producoes.Controlo2, pesoId)));
            await using var mine = CreateContextWith(outro.AntesDeGravar());

            var este = await new AssociatePesoToControloHandler(mine).HandleAsync(
                new AssociatePesoToControlo(producoes.Controlo1, pesoId));
            var outroResultado = await outro.Tarefa!;

            Assert.Equal(AssociatePesoToControloResult.Success, este.Result);
            Assert.Equal(AssociatePesoToControloResult.PesoAlreadyAssociated, outroResultado.Result);
            await using var check = CreateContext();
            Assert.Equal(producoes.Controlo1, await check.Pesos.AsNoTracking()
                .Where(p => p.Id == pesoId).Select(p => p.ControloId).SingleAsync());
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task Puncoes_AssociarOMesmoRegistoADuasProducoesAoMesmoTempo_FicaSoAPrimeira()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync(CmProcess.Nnpb);
        try
        {
            int puncoesId;
            await using (var db = CreateContext())
            {
                var registo = new Puncoes
                {
                    Reference = producoes.Marker,
                    PunchReference = "1075",
                    Lot = "1",
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.Puncoes.Add(registo);
                await db.SaveChangesAsync();
                puncoesId = registo.Id;
            }

            var outro = new OutroPedido<PuncoesResult>(db =>
                new PuncoesHandler(db).AssociateAsync(puncoesId, producoes.Controlo2, "4302"));
            await using var mine = CreateContextWith(outro.AntesDeGravar());

            var este = await new PuncoesHandler(mine).AssociateAsync(puncoesId, producoes.Controlo1, "4301");
            var outroResultado = await outro.Tarefa!;

            Assert.Equal(PuncoesResult.Success, este);
            Assert.Equal(PuncoesResult.AlreadyAssociated, outroResultado);
            await using var check = CreateContext();
            Assert.Equal(producoes.Controlo1, await check.Puncoes.AsNoTracking()
                .Where(p => p.Id == puncoesId).Select(p => p.ControloId).SingleAsync());
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    /// <summary>
    /// Associar uma folha sem Job On à produção enquanto o operador grava a
    /// 1.ª leitura dela: a leitura criava outra folha, a produção ficava com
    /// duas e a página mostrava só a primeira, sem a leitura acabada de gravar.
    /// </summary>
    [SkippableFact]
    public async Task Pegamentos_AssociarUmaFolhaEnquantoSeGravaAPrimeiraLeitura_FicaUmaFolhaComALeitura()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            var folha = await SeedFolhaSemJobOnAsync(producoes);
            var outro = new OutroPedido<RecordPegamentosMeasurementOutcome>(db =>
                new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db))
                    .HandleAsync(new RecordPegamentosMeasurement(producoes.Controlo1, "CM", 70.30m)));
            await using var mine = CreateContextWith(outro.AntesDeGravar());

            var associar = await new AssociatePegamentosToControloHandler(mine).HandleAsync(
                new AssociatePegamentosToControlo(producoes.Controlo1, folha));
            var leitura = await outro.Tarefa!;

            Assert.Equal(AssociatePegamentosToControloResult.Success, associar.Result);
            Assert.Equal(RecordPegamentosMeasurementResult.Success, leitura.Result);
            Assert.Equal(new[] { folha }, await FolhasDoControloAsync(producoes.Controlo1));
            Assert.Equal(folha, leitura.Measurement!.PegamentosId);
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task Pegamentos_AssociarDuasFolhasAMesmaProducaoAoMesmoTempo_FicaSoAPrimeira()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            var primeira = await SeedFolhaSemJobOnAsync(producoes);
            var segunda = await SeedFolhaSemJobOnAsync(producoes);
            var outro = new OutroPedido<AssociatePegamentosToControloOutcome>(db =>
                new AssociatePegamentosToControloHandler(db).HandleAsync(
                    new AssociatePegamentosToControlo(producoes.Controlo1, segunda)));
            await using var mine = CreateContextWith(outro.AntesDeGravar());

            var este = await new AssociatePegamentosToControloHandler(mine).HandleAsync(
                new AssociatePegamentosToControlo(producoes.Controlo1, primeira));
            var outroResultado = await outro.Tarefa!;

            Assert.Equal(AssociatePegamentosToControloResult.Success, este.Result);
            Assert.Equal(AssociatePegamentosToControloResult.ControloAlreadyHasPegamentos, outroResultado.Result);
            Assert.Equal(new[] { primeira }, await FolhasDoControloAsync(producoes.Controlo1));
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    /// <summary>
    /// A mesma folha associada ao mesmo tempo a duas produções com as mesmas
    /// ferramentas (I3-M-8, como no Peso e nas Punções): os trincos dos dois
    /// controlos são diferentes e os dois pedidos gravavam.
    /// </summary>
    [SkippableFact]
    public async Task Pegamentos_AssociarAMesmaFolhaADuasProducoesAoMesmoTempo_FicaSoAPrimeira()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            var folha = await SeedFolhaSemJobOnAsync(producoes);
            var outro = new OutroPedido<AssociatePegamentosToControloOutcome>(db =>
                new AssociatePegamentosToControloHandler(db).HandleAsync(
                    new AssociatePegamentosToControlo(producoes.Controlo2, folha)));
            await using var mine = CreateContextWith(outro.AntesDeGravar());

            var este = await new AssociatePegamentosToControloHandler(mine).HandleAsync(
                new AssociatePegamentosToControlo(producoes.Controlo1, folha));
            var outroResultado = await outro.Tarefa!;

            Assert.Equal(AssociatePegamentosToControloResult.Success, este.Result);
            Assert.Equal(AssociatePegamentosToControloResult.PegamentosAlreadyAssociated, outroResultado.Result);
            Assert.Equal(new[] { folha }, await FolhasDoControloAsync(producoes.Controlo1));
            Assert.Empty(await FolhasDoControloAsync(producoes.Controlo2));
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    /// <summary>Uma folha sem Job On com as ferramentas das duas produções.</summary>
    private static async Task<int> SeedFolhaSemJobOnAsync(DuasProducoes producoes)
    {
        await using var db = CreateContext();
        var folha = new DMO.Alpha.Core.Controlo.Pegamentos
        {
            CmToolId = producoes.Marker + "-CM",
            BqToolId = producoes.Marker + "-BQ",
            MfToolId = producoes.Marker + "-MF",
            ToleranceMm = 0.20m
        };
        db.Pegamentos.Add(folha);
        await db.SaveChangesAsync();
        return folha.Id;
    }

    private static async Task<int[]> FolhasDoControloAsync(int controloId)
    {
        await using var db = CreateContext();
        return await db.Pegamentos.AsNoTracking()
            .Where(p => p.ControloId == controloId)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToArrayAsync();
    }

    private sealed record DuasProducoes(string Marker, int Machine1, int Machine2, int Controlo1, int Controlo2);

    /// <summary>
    /// Duas produções com as mesmas CM, BQ e MF (o CM com 70,00 de gargalo e,
    /// quando indicado, o processo da ficha), cada uma com o seu controlo.
    /// </summary>
    private static async Task<DuasProducoes> SeedDuasProducoesAsync(CmProcess? process = null)
    {
        var marker = "AS" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (cm, bq, mf) = (marker + "-CM", marker + "-BQ", marker + "-MF");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.AddRange(
            new Tool { ToolId = cm, Type = ToolType.Cm, Reference = "1075", Lot = "3" },
            new Tool { ToolId = bq, Type = ToolType.Bq, Reference = "1075B", Lot = "1" },
            new Tool { ToolId = mf, Type = ToolType.Mf, Reference = "1075M", Lot = "1" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = cm, DiametroGargaloMm = 70.00m, Process = process });
        await db.SaveChangesAsync();

        var producoes = new List<(int MachineId, int ControloId)>();
        foreach (var suffix in new[] { "A", "B" })
        {
            var (machineId, jobOnId) = await SeedJobOnAsync(db, marker + suffix);
            db.CmContexts.Add(new CmContext { JobOnId = jobOnId, ToolId = cm });
            db.BqContexts.Add(new BqContext { JobOnId = jobOnId, ToolId = bq });
            db.MfContexts.Add(new MfContext { JobOnId = jobOnId, ToolId = mf });
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            producoes.Add((machineId, controlo.Id));
        }

        return new DuasProducoes(marker, producoes[0].MachineId, producoes[1].MachineId, producoes[0].ControloId, producoes[1].ControloId);
    }

    private static async Task DeleteDuasProducoesAsync(DuasProducoes producoes)
    {
        await using var db = CreateContext();
        var controlos = new[] { producoes.Controlo1, producoes.Controlo2 };
        await db.Pesos.Where(p => p.CmToolId == producoes.Marker + "-CM").ExecuteDeleteAsync();
        var puncoes = db.Puncoes.Where(p => p.Reference == producoes.Marker
            || (p.ControloId != null && controlos.Contains(p.ControloId.Value)));
        await db.PuncaoInspecoes.Where(i => puncoes.Select(p => p.Id).Contains(i.PuncoesId)).ExecuteDeleteAsync();
        await puncoes.ExecuteDeleteAsync();
        var pegamentos = db.Pegamentos.Where(p => p.CmToolId == producoes.Marker + "-CM"
            || (p.ControloId != null && controlos.Contains(p.ControloId.Value)));
        await db.PegamentosMeasurements.Where(m => pegamentos.Select(p => p.Id).Contains(m.PegamentosId)).ExecuteDeleteAsync();
        await pegamentos.ExecuteDeleteAsync();
        foreach (var machineId in new[] { producoes.Machine1, producoes.Machine2 })
        {
            var jobOns = db.JobOns.Where(j => j.MachineId == machineId).Select(j => j.Id);
            await db.CmContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await db.BqContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await db.MfContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await DeleteJobOnAsync(db, machineId);
        }

        await db.ToolTechnicalContexts.Where(t => t.ToolId.StartsWith(producoes.Marker)).ExecuteDeleteAsync();
        await db.Tools.Where(t => t.ToolId.StartsWith(producoes.Marker)).ExecuteDeleteAsync();
    }

    private static async Task DeleteBqAssociacaoAsync(BqAssociacao seed)
    {
        await using var db = CreateContext();
        var traces = db.BqRepairTraces.Where(t => t.Id == seed.TraceId || t.ToolId == seed.Bq
            || t.BqContextId == seed.Context1 || t.BqContextId == seed.Context2);
        await db.BqMovements.Where(m => traces.Select(t => t.Id).Contains(m.BqRepairTraceId)).ExecuteDeleteAsync();
        await traces.ExecuteDeleteAsync();
        await db.BqContexts.Where(c => c.ToolId == seed.Bq).ExecuteDeleteAsync();
        await DeleteJobOnAsync(db, seed.Machine1);
        await DeleteJobOnAsync(db, seed.Machine2);
        await db.Tools.Where(t => t.ToolId == seed.Bq).ExecuteDeleteAsync();
    }
}
