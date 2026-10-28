using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// Os filtros Estado, Localização e Fábrica de origem do Ferramentas (Diogo,
/// 28/10/2026): o estado é o último registado; a posição do Armazém vem
/// primeiro, senão a localização dos movimentos, e sem movimentos é Fabrico;
/// a Reparação filtra-se também por reparador. Filtram antes da paginação.
/// </summary>
public sealed class ToolCandidateEstadoLocalTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly ToolCandidateQuery _query;

    public ToolCandidateEstadoLocalTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new ToolCandidateQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "ARM", Type = ToolType.Cm, Reference = "1075", Lot = "1", FabricaOrigem = "MG" },
            new Tool { ToolId = "FAB", Type = ToolType.Cm, Reference = "1075", Lot = "2" },
            new Tool { ToolId = "REP-A", Type = ToolType.Mf, Reference = "1075", Lot = "3", FabricaOrigem = "LE" },
            new Tool { ToolId = "REP-B", Type = ToolType.Mf, Reference = "1075", Lot = "4" },
            new Tool { ToolId = "SUC", Type = ToolType.Bq, Reference = "C142", Lot = "5" },
            new Tool { ToolId = "EMP", Type = ToolType.Bq, Reference = "C142", Lot = "6", FabricaOrigem = "mg" });
        _db.ArmazemOcupacoes.AddRange(
            new ArmazemOcupacao { ToolId = "ARM", PosicaoId = 1, MovimentoId = 1 },
            new ArmazemOcupacao { ToolId = "FAB", PosicaoId = 2, MovimentoId = 2, LibertadaEmUtc = DateTime.UtcNow });
        _db.FerramentaLocalizacoes.AddRange(
            new FerramentaLocalizacao { ToolId = "ARM", Localizacao = FerramentaLocalizacaoTipo.Fabrico, MovimentoId = 1 },
            new FerramentaLocalizacao { ToolId = "FAB", Localizacao = FerramentaLocalizacaoTipo.Fabrico, MovimentoId = 2 },
            new FerramentaLocalizacao { ToolId = "REP-A", Localizacao = FerramentaLocalizacaoTipo.AReparar, ReparadorNome = "Moldes Silva", MovimentoId = 3 },
            new FerramentaLocalizacao { ToolId = "REP-B", Localizacao = FerramentaLocalizacaoTipo.AReparar, ReparadorNome = "Outro", MovimentoId = 4 },
            new FerramentaLocalizacao { ToolId = "SUC", Localizacao = FerramentaLocalizacaoTipo.Sucata, MovimentoId = 5 },
            new FerramentaLocalizacao { ToolId = "EMP", Localizacao = FerramentaLocalizacaoTipo.Emprestado, MovimentoId = 6 });
        _db.FerramentaEstados.AddRange(
            new FerramentaEstado { Id = 1, ToolId = "ARM", Estado = FerramentaEstadoTipo.PorReparar },
            new FerramentaEstado { Id = 2, ToolId = "ARM", Estado = FerramentaEstadoTipo.Reparado },
            new FerramentaEstado { Id = 3, ToolId = "FAB", Estado = FerramentaEstadoTipo.Novo },
            new FerramentaEstado { Id = 4, ToolId = "REP-A", Estado = FerramentaEstadoTipo.PorReparar });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<string[]> IdsAsync(ToolCandidateFilter filter) =>
        (await _query.FindCandidatesAsync(filter)).Select(c => c.ToolId).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Estado_e_o_ultimo_registado()
    {
        await SeedAsync();
        Assert.Equal(["ARM"], await IdsAsync(new ToolCandidateFilter(Estado: FerramentaEstadoTipo.Reparado)));
        Assert.Equal(["REP-A"], await IdsAsync(new ToolCandidateFilter(Estado: FerramentaEstadoTipo.PorReparar)));
        Assert.Equal(["FAB"], await IdsAsync(new ToolCandidateFilter(Estado: FerramentaEstadoTipo.Novo)));
    }

    [Fact]
    public async Task Localizacao_armazem_primeiro_e_sem_movimentos_e_fabrico()
    {
        await SeedAsync();
        _db.Tools.Add(new Tool { ToolId = "SEM", Type = ToolType.Cm, Reference = "9999", Lot = "1" });
        await _db.SaveChangesAsync();

        Assert.Equal(["ARM"], await IdsAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Armazem))));
        Assert.Equal(["FAB", "SEM"], await IdsAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Fabrico))));
        Assert.Equal(["REP-A", "REP-B"], await IdsAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Reparacao))));
        Assert.Equal(["REP-A"], await IdsAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Reparacao, "moldes silva"))));
        Assert.Equal(["SUC"], await IdsAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Sucata))));
        Assert.Equal(["EMP"], await IdsAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Emprestado))));
    }

    [Fact]
    public async Task Fabrica_de_origem_sem_maiusculas_e_antes_da_paginacao()
    {
        await SeedAsync();
        Assert.Equal(["ARM", "EMP"], await IdsAsync(new ToolCandidateFilter(FabricaOrigem: "mg")));

        var pagina = await _query.FindPageAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Reparacao), Estado: FerramentaEstadoTipo.PorReparar), 1, 10);
        Assert.Equal(1, pagina.TotalCount);
        Assert.Equal("REP-A", Assert.Single(pagina.Items).ToolId);
    }
}
