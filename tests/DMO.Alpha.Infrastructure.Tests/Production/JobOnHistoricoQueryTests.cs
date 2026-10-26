using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// The Job On history that used to be composed inside the page: the same
/// filter, dropdown, warnings and page, and nothing is written.
/// </summary>
public sealed class JobOnHistoricoQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Leituras _leituras = new();
    private readonly JobOnHistoricoQuery _query;
    private readonly Machine _b1 = new() { Code = "B1" };
    private readonly Machine _b2 = new() { Code = "B2" };

    public JobOnHistoricoQueryTests() =>
        _query = new JobOnHistoricoQuery(new ContaLeituras(new JobOnListQuery(_db), _leituras), new JobOnPaginaQuery(_db));

    public void Dispose() => _db.Dispose();

    private IReadOnlyList<MachineOption> Maquinas => [new(_b1.Id, "B1"), new(_b2.Id, "B2")];

    private async Task<JobOnHistoricoResult> HistoricoAsync(
        string? referencia = null, int? maquina = null, string? producao = null,
        string? desde = null, string? ate = null, int? pagina = null) =>
        await _query.HistoricoAsync(referencia, maquina, producao, desde, ate, pagina, Maquinas);

    private JobOn Producao(Machine machine, string reference, string numero, DateOnly? inicio = null, DateOnly? fim = null)
    {
        var j = new JobOn { Machine = machine, Reference = reference, ProductionNumber = numero, InicioEm = inicio, FimEm = fim };
        _db.JobOns.Add(j);
        return j;
    }

    [Fact]
    public async Task SemFiltro_NaoProcuraENaoLePagina()
    {
        Producao(_b1, "5447", "1");
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "  ", producao: "1");

        Assert.False(r.SearchApplied);
        Assert.Null(r.Reference);
        Assert.Empty(r.Productions);
        Assert.Empty(r.Page.Items);
        Assert.Same(PagedList<ProductionContextReadModel>.Empty, r.Page);
        Assert.Empty(r.DateWarnings);
        Assert.Equal(0, _leituras.Total);
    }

    [Fact]
    public async Task SoReferencia_ListaAsProducoesEPesquisaPelaReferencia()
    {
        Producao(_b1, "5447T173", "1");
        Producao(_b1, "5447T173", "2");
        Producao(_b1, "9999", "3");
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "  5447 ");

        Assert.True(r.SearchApplied);
        Assert.Equal("5447", r.Reference);
        Assert.Equal(["2", "1"], r.Productions.Select(p => p.ProductionNumber));
        Assert.Null(r.Production);
        Assert.Equal(2, r.Page.TotalCount);
        Assert.Equal(1, _leituras.Productions);
        Assert.Equal(1, _leituras.Searches);
    }

    [Fact]
    public async Task ReferenciaEMaquina_FiltraDropdownEResultados()
    {
        Producao(_b1, "5447", "1");
        Producao(_b2, "5447", "2");
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "5447", maquina: _b2.Id);

        Assert.Equal(_b2.Id, r.MachineId);
        Assert.Equal(["2"], r.Productions.Select(p => p.ProductionNumber));
        Assert.Equal(["2"], r.Page.Items.Select(p => p.ProductionNumber));
    }

    [Fact]
    public async Task MaquinaDesconhecida_NaoFiltra()
    {
        Producao(_b1, "5447", "1");
        Producao(_b2, "5447", "2");
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "5447", maquina: 99999);

        Assert.Null(r.MachineId);
        Assert.Equal(2, r.Page.TotalCount);

        var soMaquina = await HistoricoAsync(maquina: 99999);
        Assert.False(soMaquina.SearchApplied);
    }

    [Fact]
    public async Task ProducaoSoComNumero_EscolheAMaisRecenteComEsseNumero()
    {
        Producao(_b1, "5447A", "7");
        Producao(_b1, "5447B", "7");
        Producao(_b1, "5447A", "8");
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "5447", producao: " 7 ");

        Assert.Equal("7", r.Production);
        Assert.Equal("5447B", r.ProductionReference);
        Assert.Equal(["7"], r.Page.Items.Select(p => p.ProductionNumber));
    }

    [Fact]
    public async Task ProducaoComNumeroEReferencia_EscolheEssaReferencia()
    {
        Producao(_b1, "5447A", "7");
        Producao(_b1, "5447B", "7");
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "5447", producao: "7|5447a");

        Assert.Equal("7", r.Production);
        Assert.Equal("5447A", r.ProductionReference);
        Assert.Equal(1, r.Page.TotalCount);
        Assert.Equal('|', JobOnHistorico.Separador);
    }

    [Fact]
    public async Task ProducaoMaisAntigaPedidaPorLink_EntraNoDropdownEFiltra()
    {
        for (var i = 1; i <= IProductionPickerQuery.MaxResults + 3; i++)
        {
            Producao(_b1, "5447", $"P{i}");
        }
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "5447", producao: "P1");

        Assert.Equal(IProductionPickerQuery.MaxResults + 1, r.Productions.Count);
        Assert.Equal("P1", r.Productions[^1].ProductionNumber);
        Assert.Equal("P1", r.Production);
        Assert.Equal("5447", r.ProductionReference);
        Assert.Equal(["P1"], r.Page.Items.Select(p => p.ProductionNumber));
    }

    [Fact]
    public async Task ProducaoDeOutraReferencia_NaoFiltra()
    {
        Producao(_b1, "5447", "1");
        Producao(_b1, "9999", "900");
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(referencia: "5447", producao: "900");

        Assert.Null(r.Production);
        Assert.Null(r.ProductionReference);
        Assert.Equal(["1"], r.Page.Items.Select(p => p.ProductionNumber));
    }

    [Fact]
    public async Task DataIlegivel_AvisaEIgnora()
    {
        Producao(_b1, "5447", "1", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(desde: "ontem", ate: "2026-03-10");

        Assert.Null(r.Desde);
        Assert.Equal(new DateOnly(2026, 3, 10), r.Ate);
        Assert.Equal(["«Desde» ignorada: «ontem» não é uma data válida."], r.DateWarnings);
        Assert.True(r.SearchApplied);

        var soIlegivel = await HistoricoAsync(desde: "xx");
        Assert.False(soIlegivel.SearchApplied);
        Assert.Single(soIlegivel.DateWarnings);
    }

    [Fact]
    public async Task DesdeDepoisDeAte_TrocaAsDatasEAvisa()
    {
        Producao(_b1, "5447", "1", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));
        Producao(_b1, "5447", "2", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5));
        await _db.SaveChangesAsync();

        var r = await HistoricoAsync(desde: "2026-03-10", ate: "2026-02-20");

        Assert.Equal(new DateOnly(2026, 2, 20), r.Desde);
        Assert.Equal(new DateOnly(2026, 3, 10), r.Ate);
        Assert.Equal(["«Desde» era depois de «Até»: as datas foram trocadas."], r.DateWarnings);
        Assert.Equal(["1"], r.Page.Items.Select(p => p.ProductionNumber));
    }

    [Theory]
    [InlineData("2026-03-04")]
    [InlineData("04/03/2026")]
    [InlineData("04-03-2026")]
    [InlineData("  04/03/2026 ")]
    public async Task OsTresFormatosDeData_SaoAceites(string texto)
    {
        var r = await HistoricoAsync(desde: texto, ate: texto);

        Assert.Equal(new DateOnly(2026, 3, 4), r.Desde);
        Assert.Equal(new DateOnly(2026, 3, 4), r.Ate);
        Assert.Empty(r.DateWarnings);
    }

    [Fact]
    public async Task Paginacao_UmaPaginaDeCadaVezMaisRecentePrimeiro()
    {
        for (var i = 1; i <= ListPaging.DefaultPageSize + 5; i++)
        {
            Producao(_b1, "5447", $"P{i}");
        }
        await _db.SaveChangesAsync();

        var primeira = await HistoricoAsync(referencia: "5447");
        var segunda = await HistoricoAsync(referencia: "5447", pagina: 2);
        var longe = await HistoricoAsync(referencia: "5447", pagina: 99);

        Assert.Equal(ListPaging.DefaultPageSize + 5, primeira.Page.TotalCount);
        Assert.Equal(ListPaging.DefaultPageSize, primeira.Page.Items.Count);
        Assert.Equal("P30", primeira.Page.Items[0].ProductionNumber);
        Assert.Equal(5, segunda.Page.Items.Count);
        Assert.Equal("P1", segunda.Page.Items[^1].ProductionNumber);
        Assert.Equal(2, longe.Page.PageNumber);
    }

    [Fact]
    public async Task OPedido_NaoGravaNada()
    {
        Producao(_b1, "5447", "1");
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var antes = await _db.JobOns.CountAsync();

        await HistoricoAsync(referencia: "5447", producao: "1", desde: "2026-01-01");

        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.Equal(antes, await _db.JobOns.CountAsync());
    }

    private sealed class Leituras
    {
        public int Productions;
        public int Searches;
        public int Total => Productions + Searches;
    }

    /// <summary>Counts the history reads on top of the real query.</summary>
    private sealed class ContaLeituras(IJobOnListQuery inner, Leituras leituras) : IJobOnListQuery
    {
        public Task<PagedList<ProductionContextReadModel>> SearchHistoryPageAsync(JobOnHistoryFilter filter, int? pageNumber, int pageSize = ListPaging.DefaultPageSize, CancellationToken cancellationToken = default)
        {
            leituras.Searches++;
            return inner.SearchHistoryPageAsync(filter, pageNumber, pageSize, cancellationToken);
        }

        public Task<IReadOnlyList<JobOnProductionOption>> ListProductionsOfReferenceAsync(string reference, int? machineId = null, CancellationToken cancellationToken = default)
        {
            leituras.Productions++;
            return inner.ListProductionsOfReferenceAsync(reference, machineId, cancellationToken);
        }

        public Task<IReadOnlyList<ProductionContextReadModel>> ListByReferenceAsync(string reference, string? machine = null, CancellationToken cancellationToken = default) =>
            inner.ListByReferenceAsync(reference, machine, cancellationToken);

        public Task<IReadOnlyList<ProductionContextReadModel>> ListWindowAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken = default) =>
            inner.ListWindowAsync(de, ate, cancellationToken);

        public Task<IReadOnlyList<ProductionContextReadModel>> ListNextPerMachineAsync(DateOnly dia, CancellationToken cancellationToken = default) =>
            inner.ListNextPerMachineAsync(dia, cancellationToken);

        public Task<PagedList<ProductionContextReadModel>> ListPageOfReferenceAsync(string reference, int? pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            inner.ListPageOfReferenceAsync(reference, pageNumber, pageSize, cancellationToken);
    }
}
