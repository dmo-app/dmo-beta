using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

// A procura do separador Histórico do Peso, fora da página (Diogo,
// 27/10/2026): as mesmas regras, testadas sem a página.
public sealed class ControloHistoricoProcuraTests
{
    private static readonly ControloHistorySearchItem Registo =
        new(ControloHistoryKind.Peso, 1, 1, "5447T173", "202501", "M1", null, null, null);

    [Fact]
    public async Task SemNenhumFiltro_NaoProcura()
    {
        var (procura, historico, _) = Montar();

        var resultado = await procura.ProcurarAsync(Pedido());

        Assert.False(resultado.ComProcura);
        Assert.Null(resultado.Pagina);
        Assert.Empty(historico.Procuras);
    }

    [Fact]
    public async Task UmLinkAntigoSoComONumero_EscolheAMaisRecenteComEsseNumero()
    {
        var (procura, historico, _) = Montar(
            dropdown: [new("202501", "5447T173"), new("202501", "5447T174")],
            resultados: [Registo]);

        var resultado = await procura.ProcurarAsync(Pedido(referencia: "5447", producao: "202501"));

        Assert.Equal(("202501", "5447T173"), (resultado.Producao, resultado.ProducaoReferencia));
        Assert.Equal([("5447", "202501", "5447T173", (string?)null)], historico.Procuras);
        Assert.Equal([Registo], resultado.Pagina!.Items);
    }

    [Fact]
    public async Task OValorDoDropdown_EscolheONumeroDaquelaReferencia()
    {
        var (procura, _, _) = Montar(dropdown: [new("202501", "5447T173"), new("202501", "5447T174")]);

        var resultado = await procura.ProcurarAsync(Pedido(referencia: "5447", producao: "202501|5447t174"));

        Assert.Equal(("202501", "5447T174"), (resultado.Producao, resultado.ProducaoReferencia));
    }

    [Fact]
    public async Task UmaProducaoForaDoDropdown_FiltraEEntraNoDropdown()
    {
        var (procura, historico, _) = Montar(
            dropdown: [new("202502", "5447T173")],
            antiga: new("201901", "5447T173"));

        var resultado = await procura.ProcurarAsync(Pedido(referencia: "5447", producao: "201901"));

        Assert.Equal("201901", resultado.Producao);
        Assert.Equal(["202502", "201901"], resultado.Producoes.Select(p => p.ProductionNumber));
        Assert.Equal("201901", historico.Procuras[0].Producao);
    }

    [Fact]
    public async Task UmaProducaoQueJaNaoEDaReferencia_NaoFiltra()
    {
        var (procura, historico, _) = Montar(dropdown: [new("202502", "5447T173")]);

        var resultado = await procura.ProcurarAsync(Pedido(referencia: "5447", producao: "199901"));

        Assert.Null(resultado.Producao);
        Assert.Null(historico.Procuras[0].Producao);
    }

    [Fact]
    public async Task SemResultadosPelaReferencia_ProcuraAReferenciaComoFerramenta()
    {
        var (procura, historico, _) = Montar(resultadosDaFerramenta: [Registo]);

        var resultado = await procura.ProcurarAsync(Pedido(referencia: "CM-5447"));

        Assert.Equal(
            [("CM-5447", (string?)null, (string?)null, (string?)null), (null, null, null, "CM-5447")],
            historico.Procuras);
        Assert.Equal([Registo], resultado.Pagina!.Items);
    }

    [Fact]
    public async Task SemJobOn_NaoHaDropdownDasProducoes()
    {
        var (procura, historico, jobOns) = Montar(dropdown: [new("202501", "5447T173")], resultados: [Registo]);

        var resultado = await procura.ProcurarAsync(Pedido(referencia: "5447", producao: "202501", semJobOn: true));

        Assert.Equal(0, jobOns.Vezes);
        Assert.Empty(resultado.Producoes);
        Assert.Null(resultado.Producao);
        Assert.True(resultado.ComProcura);
        Assert.Single(historico.Procuras);
    }

    [Fact]
    public async Task OTipoEADataSaoLidosDoPedido()
    {
        var (procura, _, _) = Montar();

        var resultado = await procura.ProcurarAsync(Pedido(tipo: "Comparacao", data: "2026-10-27"));

        Assert.Equal((ControloHistoryKind.Comparacao, new DateOnly(2026, 10, 27)), (resultado.Tipo, resultado.Data));
        Assert.True(resultado.ComProcura);
    }

    private static ControloHistoricoPedido Pedido(
        string? referencia = null, string? producao = null, string? tipo = null, string? data = null, bool semJobOn = false) =>
        new(referencia, producao, null, null, tipo, data, semJobOn, null);

    private static (ControloHistoricoProcura, HistoricoFalso, JobOnsFalsos) Montar(
        IReadOnlyList<JobOnProductionOption>? dropdown = null,
        JobOnProductionOption? antiga = null,
        IReadOnlyList<ControloHistorySearchItem>? resultados = null,
        IReadOnlyList<ControloHistorySearchItem>? resultadosDaFerramenta = null)
    {
        var historico = new HistoricoFalso(antiga, resultados ?? [], resultadosDaFerramenta ?? []);
        var jobOns = new JobOnsFalsos(dropdown ?? []);
        return (new ControloHistoricoProcura(jobOns, historico), historico, jobOns);
    }

    private sealed class HistoricoFalso(
        JobOnProductionOption? antiga,
        IReadOnlyList<ControloHistorySearchItem> resultados,
        IReadOnlyList<ControloHistorySearchItem> resultadosDaFerramenta) : IControloHistorySearchQuery
    {
        public List<(string? Referencia, string? Producao, string? ProducaoReferencia, string? Ferramenta)> Procuras { get; } = [];

        public Task<PagedList<ControloHistorySearchItem>> SearchPageAsync(
            string? reference, string? productionNumber, string? tool, ControloHistoryKind? tipo, DateOnly? data,
            int? pageNumber, int pageSize = ListPaging.DefaultPageSize, CancellationToken cancellationToken = default,
            bool semJobOn = false, string? productionReference = null, string? lote = null)
        {
            Procuras.Add((reference, productionNumber, productionReference, tool));
            var itens = tool is null ? resultados : resultadosDaFerramenta;
            return Task.FromResult(new PagedList<ControloHistorySearchItem>(itens, itens.Count, 1, pageSize));
        }

        public Task<JobOnProductionOption?> ProducaoDaReferenciaAsync(
            string reference, string productionNumber, string? productionReference, CancellationToken cancellationToken = default) =>
            Task.FromResult(antiga?.ProductionNumber == productionNumber ? antiga : null);
    }

    private sealed class JobOnsFalsos(IReadOnlyList<JobOnProductionOption> dropdown) : IJobOnListQuery
    {
        public int Vezes { get; private set; }

        public Task<IReadOnlyList<JobOnProductionOption>> ListProductionsOfReferenceAsync(
            string reference, int? machineId = null, CancellationToken cancellationToken = default)
        {
            Vezes++;
            return Task.FromResult(dropdown);
        }

        public Task<IReadOnlyList<ProductionContextReadModel>> ListByReferenceAsync(string reference, string? machineCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProductionContextReadModel>> ListWindowAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProductionContextReadModel>> ListNextPerMachineAsync(DateOnly dia, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PagedList<ProductionContextReadModel>> SearchHistoryPageAsync(JobOnHistoryFilter filter, int? pageNumber, int pageSize = ListPaging.DefaultPageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PagedList<ProductionContextReadModel>> ListPageOfReferenceAsync(string reference, int? pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
