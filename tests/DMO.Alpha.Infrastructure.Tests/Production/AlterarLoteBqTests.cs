using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// O «Alterar lote» das Boquilhas (Diogo, 26/10/2026: «tenho de alterar o lote
/// da C2 agora para 167»): o lote novo nasce num bq_id novo da mesma produção,
/// os movimentos ficam no antigo e o Job On mostra todos os lotes usados.
/// </summary>
public sealed class AlterarLoteBqTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly AlterarLoteBq _alterar;

    public AlterarLoteBqTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _alterar = new AlterarLoteBq(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(JobOn JobOn, BqContext Bq)> SeedAsync()
    {
        var machine = new Machine { Code = "C2" };
        _db.Machines.Add(machine);
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-165", Type = ToolType.Bq, Reference = "T288", Lot = "165" },
            new Tool { ToolId = "BQ-167", Type = ToolType.Bq, Reference = "t288", Lot = "167" },
            new Tool { ToolId = "BQ-20", Type = ToolType.Bq, Reference = "T288", Lot = "020" },
            new Tool { ToolId = "BQ-OUTRA", Type = ToolType.Bq, Reference = "T173", Lot = "167" },
            new Tool { ToolId = "CM-288", Type = ToolType.Cm, Reference = "T288", Lot = "167" });
        var jobOn = new JobOn { Reference = "9262T288", ProductionNumber = "202605", Machine = machine };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var bq = new BqContext { JobOnId = jobOn.Id, ToolId = "BQ-165" };
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();
        jobOn.BqContext = bq;
        await _db.SaveChangesAsync();
        return (jobOn, bq);
    }

    [Fact]
    public async Task Lotes_SoAsOutrasBqDaMesmaReferencia_PorOrdemDoLote()
    {
        var (jobOn, bq) = await SeedAsync();

        var lotes = await _alterar.LotesAsync(bq.Id);

        Assert.NotNull(lotes);
        Assert.Equal((jobOn.Id, "T288", "165"), (lotes!.JobOnId, lotes.Reference, lotes.Lot));
        Assert.Equal(["BQ-20", "BQ-167"], lotes.Outros.Select(l => l.ToolId));
    }

    [Fact]
    public async Task Alterar_LoteNovoNumBqIdNovo_OAntigoFicaNaProducao_ComRasto()
    {
        var (jobOn, bq) = await SeedAsync();

        var outcome = await _alterar.AlterarAsync(new AlterarLote(bq.Id, "BQ-167", "Diogo"));

        Assert.Equal(AlterarLoteResultado.Alterado, outcome.Resultado);
        Assert.Equal("167", outcome.Lot);
        Assert.NotEqual(bq.Id, outcome.BqContextId);
        var novo = await _db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == outcome.BqContextId);
        Assert.Equal(("BQ-167", (int?)jobOn.Id), (novo.ToolId, novo.JobOnId));
        var antigo = await _db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == bq.Id);
        Assert.Equal(("BQ-165", (int?)null, (int?)jobOn.Id), (antigo.ToolId, antigo.JobOnId, antigo.ReplacedJobOnId));
        var mudou = await _db.JobOnChanges.AsNoTracking().SingleAsync(c => c.JobOnId == jobOn.Id && c.Field == JobOnChangeFields.Bq);
        Assert.Equal((JobOnChangeFields.Bq, "T288 · lote 165", "t288 · lote 167", "Diogo"),
            (mudou.Field, mudou.OldValueFrozen, mudou.NewValueFrozen, mudou.ChangedByNameFrozen));

        // O registo antigo já não é o da produção: nada a alterar nele.
        Assert.Null(await _alterar.LotesAsync(bq.Id));
        Assert.Equal(["165", "167"], await _alterar.LotesUsadosAsync(jobOn.Id));
    }

    [Fact]
    public async Task Alterar_DuasVezes_TodosOsLotesUsados_PelaOrdem()
    {
        var (jobOn, bq) = await SeedAsync();
        Assert.Equal(["165"], await _alterar.LotesUsadosAsync(jobOn.Id));

        var primeiro = await _alterar.AlterarAsync(new AlterarLote(bq.Id, "BQ-167", null));
        await _alterar.AlterarAsync(new AlterarLote(primeiro.BqContextId!.Value, "BQ-20", null));

        Assert.Equal(["165", "167", "020"], await _alterar.LotesUsadosAsync(jobOn.Id));
    }

    [Theory]
    [InlineData("BQ-OUTRA")]
    [InlineData("CM-288")]
    [InlineData("BQ-165")]
    [InlineData("NAO-EXISTE")]
    public async Task Alterar_OutraReferenciaOutroTipoOuOMesmo_NaoServe_ENadaMuda(string toolId)
    {
        var (jobOn, bq) = await SeedAsync();

        var outcome = await _alterar.AlterarAsync(new AlterarLote(bq.Id, toolId, null));

        Assert.Equal(AlterarLoteResultado.LoteNaoServe, outcome.Resultado);
        Assert.Equal("BQ-165", (await _db.BqContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOn.Id)).ToolId);
        Assert.Empty(_db.JobOnChanges);
    }

    [Fact]
    public async Task Alterar_NumLoteJaTrocado_NaoMudaNada()
    {
        var (_, bq) = await SeedAsync();
        await _alterar.AlterarAsync(new AlterarLote(bq.Id, "BQ-167", null));

        var mudancas = _db.JobOnChanges.Count();

        var outcome = await _alterar.AlterarAsync(new AlterarLote(bq.Id, "BQ-20", null));

        Assert.Equal(AlterarLoteResultado.JaNaoEDaProducao, outcome.Resultado);
        Assert.Equal(mudancas, _db.JobOnChanges.Count());
    }

    [Fact]
    public async Task Alterar_AUtilizacaoDaFolhaPassaParaADoLoteNovo_0SeENovo()
    {
        var (jobOn, bq) = await SeedAsync();
        _db.JobOnFolhas.Add(new JobOnFolha { JobOnId = jobOn.Id, BqUtilizacao = "62", BqNotas = "Tratar" });
        _db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "BQ-20", Percentagem = 12.5m, RegistadaEmUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var primeiro = await _alterar.AlterarAsync(new AlterarLote(bq.Id, "BQ-167", null));
        var folha = await _db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == jobOn.Id);
        Assert.Equal(("0", "Tratar"), (folha.BqUtilizacao, folha.BqNotas));
        Assert.Contains(_db.JobOnChanges, c => c.Field == JobOnChangeFields.Folha && c.NewValueFrozen == "BQ · Utilização (%)");

        await _alterar.AlterarAsync(new AlterarLote(primeiro.BqContextId!.Value, "BQ-20", null));
        Assert.Equal("12.5", (await _db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == jobOn.Id)).BqUtilizacao);
    }
}
