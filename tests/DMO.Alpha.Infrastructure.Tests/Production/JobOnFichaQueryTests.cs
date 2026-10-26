using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// The reads of the open Job On sheet that used to be made inside the page:
/// the same data, the duplicate reads less, and nothing is written.
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
            new JobOnLotNoteHandler(_db),
            new JobOnChangeQuery(_db),
            new ContaPreviews(new DeleteJobOnHandler(_db), _leituras),
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

    private void Nota(int jobOnId, string slot, string lote, DateOnly dia) =>
        _db.JobOnLotNotes.Add(new JobOnLotNote
        {
            JobOnId = jobOnId, Dia = dia, Slot = slot, NewToolId = "x", ToolReferenceFrozen = "r",
            NewLotFrozen = lote, CreatedAtUtc = new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc)
        });

    private void Alteracao(int jobOnId, string campo, string de, string para, DateTime quando) =>
        _db.JobOnChanges.Add(new JobOnChange
        {
            JobOnId = jobOnId, Field = campo, OldValueFrozen = de, NewValueFrozen = para,
            ChangedByNameFrozen = "Rui", ChangedAtUtc = quando
        });

    [Fact]
    public async Task JobOnAberto_DevolveNotasAlteracoesPreviewFactosEFolha()
    {
        var producao = await ProducaoAsync();
        Nota(producao.JobOnId, "BQ", "2", new DateOnly(2026, 10, 22));
        Nota(producao.JobOnId, "CM", "3", new DateOnly(2026, 10, 24));
        Alteracao(producao.JobOnId, JobOnChangeFields.Machine, "B1", "B2", new DateTime(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc));
        Alteracao(producao.JobOnId, JobOnChangeFields.Calote, "37", "38", new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc));
        var outra = await ProducaoAsync(comFerramentas: false, numero: "2");
        Nota(outra.JobOnId, "MF", "9", new DateOnly(2026, 10, 22));
        Alteracao(outra.JobOnId, JobOnChangeFields.Start, "a", "b", new DateTime(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc));
        _db.JobOnFolhas.Add(new JobOnFolha { JobOnId = producao.JobOnId, CmNotas = "Nota da CM." });
        await _db.SaveChangesAsync();

        var ficha = await _query.FichaAsync(producao, Hoje, duplicar: false);

        Assert.Equal(["BQ", "CM"], ficha.LotNotes.Select(n => n.Slot));
        Assert.All(ficha.LotNotes, n => Assert.Equal(producao.JobOnId, n.JobOnId));
        // Todas as alterações de uma produção, da mais recente para a mais antiga.
        Assert.Equal([JobOnChangeFields.Calote, JobOnChangeFields.Machine], ficha.Changes.Select(c => c.Field));
        Assert.All(ficha.Changes, c => Assert.Equal(producao.JobOnId, c.JobOnId));
        Assert.NotNull(ficha.DeletePreview);
        Assert.Equal(producao.JobOnId, ficha.DeletePreview!.JobOnId);
        Assert.True(ficha.DeletePreview.StartReached);
        Assert.Equal(["bq-1", "cm-1", "mf-1"], ficha.ToolFacts.Select(f => f.ToolId).Order());
        Assert.Equal("Nota da CM.", ficha.Folha.Valor("cm_notas"));
        Assert.Equal(1, _leituras.Previews);
        Assert.Equal(1, _leituras.Factos);
        Assert.Equal(1, _leituras.Folhas);
    }

    [Fact]
    public async Task Duplicar_NaoLePreviewNemFactos_MasLeAFolhaQueCopia()
    {
        var producao = await ProducaoAsync();
        Nota(producao.JobOnId, "BQ", "2", new DateOnly(2026, 10, 22));
        _db.JobOnFolhas.Add(new JobOnFolha { JobOnId = producao.JobOnId, CmNotas = "Copiar." });
        await _db.SaveChangesAsync();

        var ficha = await _query.FichaAsync(producao, Hoje, duplicar: true);

        Assert.Null(ficha.DeletePreview);
        Assert.Empty(ficha.ToolFacts);
        Assert.Single(ficha.LotNotes);
        Assert.Equal("Copiar.", ficha.Folha.Valor("cm_notas"));
        Assert.Equal(0, _leituras.Previews);
        Assert.Equal(0, _leituras.Factos);
        Assert.Equal(1, _leituras.Folhas);
    }

    [Fact]
    public async Task SemFerramentas_NaoLeFactos_MasLePreview()
    {
        var producao = await ProducaoAsync(comFerramentas: false);

        var ficha = await _query.FichaAsync(producao, Hoje, duplicar: false);

        Assert.Empty(ficha.ToolFacts);
        Assert.NotNull(ficha.DeletePreview);
        Assert.Equal(0, _leituras.Factos);
        Assert.Equal(1, _leituras.Previews);
    }

    [Fact]
    public async Task SoUmaFerramenta_LeFactosSoDessa()
    {
        var producao = await ProducaoAsync(comFerramentas: false);
        _db.Tools.Add(new Tool { ToolId = "so-mf", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        _db.MfContexts.Add(new MfContext { JobOnId = producao.JobOnId, ToolId = "so-mf" });
        await _db.SaveChangesAsync();
        producao = (await new ProductionContextQuery(_db).GetAsync(producao.JobOnId))!;

        var ficha = await _query.FichaAsync(producao, Hoje, duplicar: false);

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

        var fichaCom = await _query.FichaAsync(com, Hoje, duplicar: false);
        var fichaSem = await _query.FichaAsync(sem, Hoje, duplicar: false);

        Assert.False(fichaCom.Folha.Vazia);
        Assert.Equal("512", fichaCom.Folha.Valor("cab_peso"));
        Assert.True(fichaSem.Folha.Vazia);
        Assert.Equal(sem.JobOnId, fichaSem.Folha.JobOnId);
    }

    [Fact]
    public async Task SemNotasNemAlteracoes_ListasVazias()
    {
        var producao = await ProducaoAsync();

        var ficha = await _query.FichaAsync(producao, Hoje, duplicar: false);

        Assert.Empty(ficha.LotNotes);
        Assert.Empty(ficha.Changes);
    }

    [Fact]
    public async Task OPreviewUsaODiaPedido()
    {
        var producao = await ProducaoAsync();

        var antes = await _query.FichaAsync(producao, new DateOnly(2026, 10, 19), duplicar: false);
        var depois = await _query.FichaAsync(producao, new DateOnly(2026, 10, 20), duplicar: false);

        Assert.False(antes.DeletePreview!.StartReached);
        Assert.True(depois.DeletePreview!.StartReached);
    }

    [Fact]
    public async Task AsLeiturasSaoPelaOrdemDaPagina_NotasAlteracoesPreviewFactosFolha()
    {
        var producao = await ProducaoAsync();

        await _query.FichaAsync(producao, Hoje, duplicar: false);

        Assert.Equal(["preview", "factos", "folha"], _leituras.Ordem);
    }

    [Fact]
    public async Task OPedido_NaoGravaNada()
    {
        var producao = await ProducaoAsync();
        Nota(producao.JobOnId, "BQ", "2", new DateOnly(2026, 10, 22));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var notas = await _db.JobOnLotNotes.CountAsync();
        var folhas = await _db.JobOnFolhas.CountAsync();

        await _query.FichaAsync(producao, Hoje, duplicar: false);
        await _query.FichaAsync(producao, Hoje, duplicar: true);

        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.Equal(notas, await _db.JobOnLotNotes.CountAsync());
        Assert.Equal(folhas, await _db.JobOnFolhas.CountAsync());
    }

    private sealed class Leituras
    {
        public int Previews;
        public int Factos;
        public int Folhas;
        public List<string> Ordem { get; } = [];
    }

    private sealed class ContaPreviews(IDeleteJobOnHandler inner, Leituras leituras) : IDeleteJobOnHandler
    {
        public Task<JobOnDeletionPreview?> PreviewAsync(int jobOnId, DateOnly today, CancellationToken cancellationToken = default)
        {
            leituras.Previews++;
            leituras.Ordem.Add("preview");
            return inner.PreviewAsync(jobOnId, today, cancellationToken);
        }

        public Task<DeleteJobOnResult> HandleAsync(DeleteJobOn command, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The sheet never deletes.");
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
