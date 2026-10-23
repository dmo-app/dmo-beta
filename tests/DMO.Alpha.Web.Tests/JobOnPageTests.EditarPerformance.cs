using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnPageTests
{
    [Fact]
    public async Task EditarFolha_NaoCarregaProducoesDoPainelQueNaoMostra()
    {
        SeedUser("4495", "Editar rápido", "provider-editar-rapido", "Editar1234!");
        var id = await SeedJobOnCompletoAsync();
        using var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IJobOnListQuery, PainelNaoNecessario>()));
        using var client = await LoginClientAsync("4495", "Editar1234!", factory);
        var response = await client.GetAsync($"/JobOn/Folha?jobon_id={id}&editar=1");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"versao\"", await response.Content.ReadAsStringAsync());
    }

    private sealed class PainelNaoNecessario : IJobOnListQuery
    {
        private static InvalidOperationException Desnecessario() => new("Editar não deve carregar o painel que não desenha.");
        public Task<IReadOnlyList<ProductionContextReadModel>> ListWindowAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken = default) => throw Desnecessario();
        public Task<IReadOnlyList<ProductionContextReadModel>> ListByReferenceAsync(string reference, string? machineCode, CancellationToken cancellationToken = default) => throw Desnecessario();
        public Task<IReadOnlyList<ProductionContextReadModel>> ListNextPerMachineAsync(DateOnly dia, CancellationToken cancellationToken = default) => throw Desnecessario();
        public Task<PagedList<ProductionContextReadModel>> SearchHistoryPageAsync(JobOnHistoryFilter filter, int? pageNumber, int pageSize = ListPaging.DefaultPageSize, CancellationToken cancellationToken = default) => throw Desnecessario();
        public Task<IReadOnlyList<JobOnProductionOption>> ListProductionsOfReferenceAsync(string reference, int? machineId = null, CancellationToken cancellationToken = default) => throw Desnecessario();
        public Task<PagedList<ProductionContextReadModel>> ListPageOfReferenceAsync(string reference, int? pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw Desnecessario();
    }
}
