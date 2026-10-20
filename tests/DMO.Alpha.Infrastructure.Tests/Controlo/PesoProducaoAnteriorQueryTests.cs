using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Deep sweep 2 (18/10/2026, F-11): a «Produção anterior» do Peso tinha até
/// 50 produções da referência, e vinha escolhida uma posterior à atual ou uma
/// sem datas. As opções são só as produções da mesma referência anteriores à
/// atual, as últimas 5, da mais recente para a mais antiga. O PostgreSQL (os
/// null primeiro num DESC) prova-se em PostgresMigrationIntegrationTests.
/// </summary>
public sealed class PesoProducaoAnteriorQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoProducaoAnteriorQuery _query;
    private Machine? _machine;

    public PesoProducaoAnteriorQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new PesoProducaoAnteriorQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Um Job On da máquina B1, gravado já (os ids crescem pela ordem das chamadas).</summary>
    private async Task<int> SeedJobOnAsync(string reference, string number, DateOnly? inicio)
    {
        if (_machine is null)
        {
            _machine = new Machine { Code = "B1" };
            _db.Machines.Add(_machine);
            await _db.SaveChangesAsync();
        }

        var jobOn = new JobOn { Reference = reference, ProductionNumber = number, MachineId = _machine.Id, InicioEm = inicio };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        return jobOn.Id;
    }

    private async Task<string[]> NumerosAsync(int currentJobOnId) =>
        (await _query.ListOpcoesAsync(currentJobOnId)).Select(o => o.ProductionNumber).ToArray();

    [Fact]
    public async Task ComInicio_SoAsQueComecaramAntes_SemAsPosterioresNemAsSemDatas()
    {
        // A prova do achado: no Peso da 2459T035 · 2021017 vinha escolhida a
        // 2026138 (posterior); noutra, uma sem datas.
        await SeedJobOnAsync("2459T035", "2020090", new DateOnly(2020, 11, 2));
        var atual = await SeedJobOnAsync("2459T035", "2021017", new DateOnly(2021, 1, 11));
        await SeedJobOnAsync("2459T035", "2026138", new DateOnly(2026, 10, 19));
        await SeedJobOnAsync("2459T035", "2026153", null);
        await SeedJobOnAsync("2459T035", "2020100", new DateOnly(2020, 12, 7));
        await SeedJobOnAsync("9999X035", "2020120", new DateOnly(2020, 12, 21));

        Assert.Equal(new[] { "2020100", "2020090" }, await NumerosAsync(atual));
    }

    [Fact]
    public async Task ComInicio_AsUltimas5_EnoMesmoDiaAsRegistadasAntes()
    {
        await SeedJobOnAsync("5447T173", "202609", new DateOnly(2026, 9, 1));
        foreach (var mes in Enumerable.Range(1, 7))
        {
            await SeedJobOnAsync("5447T173", $"20260{mes}", new DateOnly(2026, mes, 3));
        }
        var atual = await SeedJobOnAsync("5447T173", "202610", new DateOnly(2026, 9, 1));
        await SeedJobOnAsync("5447T173", "202611", new DateOnly(2026, 9, 1));

        var numeros = await NumerosAsync(atual);

        // A do mesmo dia registada antes é anterior; a registada depois não.
        Assert.Equal(new[] { "202609", "202607", "202606", "202605", "202604" }, numeros);
        Assert.Equal(IProductionPickerQuery.MaxResults, numeros.Length);
    }

    [Fact]
    public async Task SemInicio_AsRegistadasAntes_PrimeiroAsComInicio()
    {
        // A atual sem início (3 das 5 da 6018Y232 no DEV): as registadas
        // antes dela, as com início primeiro, as sem início no fim; nunca as
        // registadas depois.
        await SeedJobOnAsync("6018Y232", "202502", null);
        await SeedJobOnAsync("6018Y232", "202503", null);
        await SeedJobOnAsync("6018Y232", "202616", new DateOnly(2026, 9, 9));
        await SeedJobOnAsync("6018Y232", "202501", new DateOnly(2025, 1, 6));
        var atual = await SeedJobOnAsync("6018Y232", "202617", null);
        await SeedJobOnAsync("6018Y232", "202618", new DateOnly(2026, 10, 16));

        Assert.Equal(new[] { "202616", "202501", "202503", "202502" }, await NumerosAsync(atual));
    }

    [Fact]
    public async Task SemOutrasAnteriores_ListaVazia()
    {
        var atual = await SeedJobOnAsync("1075C142", "202601", new DateOnly(2026, 1, 5));
        await SeedJobOnAsync("1075C142", "202602", new DateOnly(2026, 2, 2));

        Assert.Empty(await NumerosAsync(atual));
        Assert.Empty(await NumerosAsync(atual + 100));
    }
}
