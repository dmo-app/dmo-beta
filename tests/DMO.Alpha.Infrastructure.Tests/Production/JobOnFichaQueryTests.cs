using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// The reads of the open Job On sheet that used to be made inside the page:
/// the tool facts and the saved sheet, the duplicate reads less, and nothing
/// is written (the lot notes, changes and delete preview are no longer read).
/// </summary>
public sealed class JobOnFichaQueryTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 10, 25);

    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Leituras _leituras = new();
    private readonly JobOnFichaQuery _query;

    public JobOnFichaQueryTests() =>
        _query = new JobOnFichaQuery(
            new ContaFactos(new JobOnToolFactsQuery(_db), _leituras),
            new ContaFolhas(new JobOnFolhaQuery(_db), _leituras));

    public void Dispose() => _db.Dispose();

    private async Task<ProductionContextReadModel> ProducaoAsync(bool comFerramentas = true, string numero = "1")
    {
        var jobOn = new JobOn
        {
            Machine = new Machine { Code = "B" + numero }, Reference = "5810T188", ProductionNumber = numero,
            InicioEm = new DateOnly(2026, 10, 20), FimEm = new DateOnly(2026, 10, 30)
        };
        _db.JobOns.Add(jobOn);
        if (comFerramentas)
        {
            _db.Tools.AddRange(
                new Tool { ToolId = "cm-" + numero, Type = ToolType.Cm, Reference = "5809", Lot = "1" },
                new Tool { ToolId = "mf-" + numero, Type = ToolType.Mf, Reference = "5810", Lot = "1" },
                new Tool { ToolId = "bq-" + numero, Type = ToolType.Bq, Reference = "T188", Lot = "1" });
            _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "cm-" + numero });
            _db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "mf-" + numero });
            _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "bq-" + numero });
        }
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return (await new ProductionContextQuery(_db).GetAsync(jobOn.Id))!;
    }

    [Fact]
    public async Task JobOnAberto_DevolveFactosEFolha()
    {
        var producao = await ProducaoAsync();
        _db.JobOnFolhas.Add(new JobOnFolha { JobOnId = producao.JobOnId, CmNotas = "Nota da CM." });
        await _db.SaveChangesAsync();

        var ficha = await _query.FichaAsync(producao, duplicar: false);

        Assert.Equal(["bq-1", "cm-1", "mf-1"], ficha.ToolFacts.Select(f => f.ToolId).Order());
        Assert.Equal("Nota da CM.", ficha.Folha.Valor("cm_notas"));
        Assert.Equal(1, _leituras.Factos);
        Assert.Equal(1, _leituras.Folhas);
    }

    [Fact]
    public async Task Duplicar_NaoLeFactos_MasLeAFolhaQueCopia()
    {
        var producao = await ProducaoAsync();
        _db.JobOnFolhas.Add(new JobOnFolha { JobOnId = producao.JobOnId, CmNotas = "Copiar." });
        await _db.SaveChangesAsync();

        var ficha = await _query.FichaAsync(producao, duplicar: true);

        Assert.Empty(ficha.ToolFacts);
        Assert.Equal("Copiar.", ficha.Folha.Valor("cm_notas"));
        Assert.Equal(0, _leituras.Factos);
        Assert.Equal(1, _leituras.Folhas);
    }

    [Fact]
    public async Task SemFerramentas_NaoLeFactos()
    {
        var producao = await ProducaoAsync(comFerramentas: false);

        var ficha = await _query.FichaAsync(producao, duplicar: false);

        Assert.Empty(ficha.ToolFacts);
        Assert.Equal(0, _leituras.Factos);
    }

    [Fact]
    public async Task SoUmaFerramenta_LeFactosSoDessa()
    {
        var producao = await ProducaoAsync(comFerramentas: false);
        _db.Tools.Add(new Tool { ToolId = "so-mf", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        _db.MfContexts.Add(new MfContext { JobOnId = producao.JobOnId, ToolId = "so-mf" });
        await _db.SaveChangesAsync();
        producao = (await new ProductionContextQuery(_db).GetAsync(producao.JobOnId))!;

        var ficha = await _query.FichaAsync(producao, duplicar: false);

        Assert.Equal(["so-mf"], ficha.ToolFacts.Select(f => f.ToolId));
        Assert.Equal(1, _leituras.Factos);
    }

    [Fact]
    public async Task FolhaGravada_Presente_OuAusenteLeSeComoVazia()
    {
        var com = await ProducaoAsync(numero: "1");
        var sem = await ProducaoAsync(numero: "2");
        _db.JobOnFolhas.Add(new JobOnFolha { JobOnId = com.JobOnId, CabPeso = "512" });
        await _db.SaveChangesAsync();

        var fichaCom = await _query.FichaAsync(com, duplicar: false);
        var fichaSem = await _query.FichaAsync(sem, duplicar: false);

        Assert.False(fichaCom.Folha.Vazia);
        Assert.Equal("512", fichaCom.Folha.Valor("cab_peso"));
        Assert.True(fichaSem.Folha.Vazia);
        Assert.Equal(sem.JobOnId, fichaSem.Folha.JobOnId);
    }

    [Fact]
    public async Task AsLeiturasSaoPelaOrdemDaPagina_FactosEFolha()
    {
        var producao = await ProducaoAsync();

        await _query.FichaAsync(producao, duplicar: false);

        Assert.Equal(["factos", "folha"], _leituras.Ordem);
    }

    [Fact]
    public async Task OPedido_NaoGravaNada()
    {
        var producao = await ProducaoAsync();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var folhas = await _db.JobOnFolhas.CountAsync();

        await _query.FichaAsync(producao, duplicar: false);
        await _query.FichaAsync(producao, duplicar: true);

        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.Equal(folhas, await _db.JobOnFolhas.CountAsync());
    }

    [Fact]
    public async Task NaoLeNotasDeLoteNemAlteracoesNemOQueSePerdeAoEliminar()
    {
        // The page no longer shows them, so the service must not read them, even when
        // the Job On has some: the result and the dependencies are the tool facts and the sheet only.
        var producao = await ProducaoAsync();
        _db.JobOnLotNotes.Add(new JobOnLotNote
        {
            JobOnId = producao.JobOnId, Dia = Hoje, Slot = "BQ", NewToolId = "x", ToolReferenceFrozen = "r",
            NewLotFrozen = "2", CreatedAtUtc = new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc)
        });
        _db.JobOnChanges.Add(new JobOnChange
        {
            JobOnId = producao.JobOnId, Field = JobOnChangeFields.Machine, OldValueFrozen = "B1", NewValueFrozen = "B2",
            ChangedAtUtc = new DateTime(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var ficha = await _query.FichaAsync(producao, duplicar: false);

        // The result carries only the tool facts and the saved sheet.
        Assert.Equal(["ToolFacts", "Folha"], typeof(JobOnFichaResult).GetProperties().Select(p => p.Name));
        Assert.Equal(3, ficha.ToolFacts.Count);
        Assert.Equal(["factos", "folha"], _leituras.Ordem);
        // The only constructor dependencies are the tool facts and the sheet queries.
        Assert.Equal(
            [typeof(IJobOnToolFactsQuery), typeof(IJobOnFolhaQuery)],
            typeof(JobOnFichaQuery).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
    }

    private sealed class Leituras
    {
        public int Factos;
        public int Folhas;
        public List<string> Ordem { get; } = [];
    }

    private sealed class ContaFactos(IJobOnToolFactsQuery inner, Leituras leituras) : IJobOnToolFactsQuery
    {
        public Task<IReadOnlyList<JobOnToolFacts>> FindAsync(
            IReadOnlyList<string> toolIds, string? machineCode, int? excludingJobOnId,
            DateOnly? inicioEm, DateOnly? fimEm, CancellationToken cancellationToken = default)
        {
            leituras.Factos++;
            leituras.Ordem.Add("factos");
            return inner.FindAsync(toolIds, machineCode, excludingJobOnId, inicioEm, fimEm, cancellationToken);
        }
    }

    private sealed class ContaFolhas(IJobOnFolhaQuery inner, Leituras leituras) : IJobOnFolhaQuery
    {
        public Task<JobOnFolhaLida> ForJobOnAsync(int jobOnId, CancellationToken cancellationToken = default)
        {
            leituras.Folhas++;
            leituras.Ordem.Add("folha");
            return inner.ForJobOnAsync(jobOnId, cancellationToken);
        }
    }
}
