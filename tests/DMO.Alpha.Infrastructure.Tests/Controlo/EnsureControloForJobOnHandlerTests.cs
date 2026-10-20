using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova o ensure do controlo_id de uma produção (spec Job On L33: o
/// controlo_id é criado com o Job On): get-or-create idempotente por
/// jobon_id — cria UM controlo para um jobon existente, a re-execução
/// devolve o MESMO controlo_id sem segunda escrita (abrir 2× = 1
/// controlo_id), um jobon inexistente é recusa tipada sem escrita nenhuma,
/// dois jobons têm dois controles distintos (um por produção) e uma
/// produção criada antes deste corte (estilo J10 real: jobon persistido
/// sem controlo) ganha aqui o seu controlo_id UMA vez por backfill. O
/// handler nunca muta o Job On nem os seus contextos e nunca é um ato de
/// criação do utilizador — é bookkeeping idempotente suportado pelo
/// índice único na coluna JobOnId (padrão DuplicateProductionNumber).
/// </summary>
public sealed class EnsureControloForJobOnHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly EnsureControloForJobOnHandler _handler;

    public EnsureControloForJobOnHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new EnsureControloForJobOnHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<(int JobOnId, int CmId, int BqId)> SeedJobOnAsync(
        string productionNumber,
        string reference,
        string machineCode,
        string? cmToolId = null,
        string? bqToolId = null)
    {
        // A âncora real: um Job On persistido com os contextos aplicáveis
        // (a mesma forma com que a página Job On e os seeds dos testes de
        // página constroem uma produção).
        var machine = new Machine { Code = machineCode };
        var jobOn = new JobOn
        {
            Reference = reference,
            ProductionNumber = productionNumber,
            Machine = machine
        };

        CmContext? cm = null;
        BqContext? bq = null;
        if (cmToolId is not null)
        {
            cm = new CmContext { JobOn = jobOn, ToolId = cmToolId };
        }

        if (bqToolId is not null)
        {
            bq = new BqContext { JobOn = jobOn, ToolId = bqToolId };
        }

        _db.JobOns.Add(jobOn);
        if (cm is not null)
        {
            _db.CmContexts.Add(cm);
        }

        if (bq is not null)
        {
            _db.BqContexts.Add(bq);
        }

        await _db.SaveChangesAsync();
        return (jobOn.Id, cm?.Id ?? 0, bq?.Id ?? 0);
    }

    [Fact]
    public async Task Ensure_ExistingJobOn_CreatesExactlyOneControlo()
    {
        // O cenário J10: produção real existente (ref 5810, B1, cm 1→T3,
        // bq 1→BQ-1, mf não aplicável). O ensure cria UM controlo
        // vinculado a ESTE jobon_id — e nada mais é escrito (o Job On e os
        // contextos ficam intocados).
        var (jobOnId, _, _) = await SeedJobOnAsync("J10", "5810", "B1", cmToolId: "T3", bqToolId: "BQ-1");

        var outcome = await _handler.HandleAsync(new EnsureControloForJobOn(jobOnId));

        Assert.Equal(EnsureControloResult.Success, outcome.Result);
        Assert.NotNull(outcome.ControloId);

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        Assert.Equal(outcome.ControloId, controlo.Id);
        Assert.Equal(jobOnId, controlo.JobOnId);

        // A tabela tem exactamente a identidade e a relação: nenhum facto
        // CM/BQ/MF/PU/CS, nenhum timestamp, nenhum estado (a forma do
        // modelo prova-o nos ControloResumoSchemaTests).
        Assert.Equal(1, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Ensure_ReExecution_ReturnsSameControloId()
    {
        // Abrir 2× = 1 controlo_id: a re-execução do ensure devolve o
        // MESMO id e NÃO cria um segundo registo (idempotência por
        // JobOnId; a reabertura não é uma segunda escrita).
        var (jobOnId, _, _) = await SeedJobOnAsync("J10", "5810", "B1", cmToolId: "T3", bqToolId: "BQ-1");

        var first = await _handler.HandleAsync(new EnsureControloForJobOn(jobOnId));
        var second = await _handler.HandleAsync(new EnsureControloForJobOn(jobOnId));

        Assert.Equal(EnsureControloResult.Success, first.Result);
        Assert.Equal(EnsureControloResult.Success, second.Result);
        Assert.Equal(first.ControloId, second.ControloId);
        Assert.Equal(1, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Ensure_UnknownJobOn_RefusedTypedWithoutWrite()
    {
        // O jobon_id é âncora canónica persistida: um id que não resolve é
        // recusa tipada — nunca é criado um controlo para uma produção
        // inexistente (o ensure não é um acto de criação de produção).
        var outcome = await _handler.HandleAsync(new EnsureControloForJobOn(999));

        Assert.Equal(EnsureControloResult.JobOnNotFound, outcome.Result);
        Assert.Null(outcome.ControloId);
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Ensure_TwoJobOns_TwoControlos()
    {
        // Um controlo por produção: dois jobons têm dois controlo_id
        // distintos, cada um vinculado ao seu jobon_id.
        var (firstJobOnId, _, _) = await SeedJobOnAsync("J10", "5810", "B1", cmToolId: "T3", bqToolId: "BQ-1");
        var (secondJobOnId, _, _) = await SeedJobOnAsync("J11", "5447", "B2", cmToolId: "T4");

        var first = await _handler.HandleAsync(new EnsureControloForJobOn(firstJobOnId));
        var second = await _handler.HandleAsync(new EnsureControloForJobOn(secondJobOnId));

        Assert.Equal(EnsureControloResult.Success, first.Result);
        Assert.Equal(EnsureControloResult.Success, second.Result);
        Assert.NotEqual(first.ControloId, second.ControloId);

        var controlos = await _db.Controlos.AsNoTracking().ToListAsync();
        Assert.Equal(2, controlos.Count);
        Assert.Contains(controlos, c => c.JobOnId == firstJobOnId && c.Id == first.ControloId);
        Assert.Contains(controlos, c => c.JobOnId == secondJobOnId && c.Id == second.ControloId);
    }

    [Fact]
    public async Task Ensure_PreCutJobOn_GainsControloByBackfillOnce()
    {
        // Backfill estilo J10 real: uma produção criada ANTES deste corte
        // (persistida sem controlo — exactamente o estado da base real)
        // ganha o seu controlo_id na primeira abertura do Resumo, e as
        // aberturas seguintes devolvem o MESMO controlo_id (a coluna
        // JobOnId com índice único é a última linha da verdade: nunca um
        // segundo controlo_id para a mesma produção).
        var (jobOnId, _, _) = await SeedJobOnAsync("J10", "5810", "B1", cmToolId: "T3", bqToolId: "BQ-1");
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());

        var first = await _handler.HandleAsync(new EnsureControloForJobOn(jobOnId));
        var second = await _handler.HandleAsync(new EnsureControloForJobOn(jobOnId));
        var third = await _handler.HandleAsync(new EnsureControloForJobOn(jobOnId));

        Assert.Equal(EnsureControloResult.Success, first.Result);
        Assert.Equal(first.ControloId, second.ControloId);
        Assert.Equal(first.ControloId, third.ControloId);

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        Assert.Equal(jobOnId, controlo.JobOnId);
        Assert.Equal(first.ControloId, controlo.Id);
    }
}
