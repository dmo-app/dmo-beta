using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// DMO-403 (beta.89): «Duplicar para lote novo»: a cópia leva também o link do
/// mcaliper, a fábrica de origem, as linhas e os dados técnicos, e o lote novo
/// começa sem utilização.
/// </summary>
public sealed class FerramentasDuplicarCoberturaA2Tests : IDisposable
{
    private readonly DmoDbContext _db;

    public FerramentasDuplicarCoberturaA2Tests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task DuplicarParaLoteNovo_LevaOLinkMcaliperEAFabricaDeOrigem_ESemUtilizacao()
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool
        {
            ToolId = ToolRegistryConstants.RemovedToolId,
            Type = ToolType.Cm,
            Reference = "REMOVED",
            Lot = "REMOVED"
        });
        await _db.SaveChangesAsync();

        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(
            new CreateTool("CM", "5447", "3", "5810", null, new[] { machine.Id }));
        var sourceId = create.Tool!.ToolId;

        await new EditToolTechnicalValuesHandler(_db).HandleAsync(new EditToolTechnicalValues(
            sourceId, ProcessToken: "NNPB", NominalWeightGrams: 520m));

        const string mcaliper = "https://mcaliper.exemplo.test/ferramenta/5447-lote-3";
        var origem = await _db.Tools.SingleAsync(t => t.ToolId == sourceId);
        origem.FabricaOrigem = "MG";
        var tecnico = await _db.ToolTechnicalContexts.SingleAsync(t => t.ToolId == sourceId);
        tecnico.McaliperUrl = mcaliper;
        // O lote 3 já foi usado: uma leitura de utilização e o lembrete aberto.
        _db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao
        {
            ToolId = sourceId,
            Percentagem = 42.5m,
            Origem = FerramentaUtilizacao.OrigemManual,
            RegistadaEmUtc = DateTime.UtcNow
        });
        _db.FerramentaLembretesUtilizacao.Add(new FerramentaLembreteUtilizacao { ToolId = sourceId, CriadoEmUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var outcome = await new DuplicateToolHandler(_db).HandleAsync(new DuplicateTool(sourceId, "8"));

        Assert.Equal(DuplicateToolResult.Success, outcome.Result);
        var novoId = outcome.Tool!.ToolId;
        Assert.NotEqual(sourceId, novoId);

        var novo = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == novoId);
        Assert.Equal("8", novo.Lot);
        Assert.Equal("MG", novo.FabricaOrigem);
        var novoTecnico = await _db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == novoId);
        Assert.Equal(mcaliper, novoTecnico.McaliperUrl);
        Assert.Equal(520m, novoTecnico.NominalWeightGrams);

        // O lote novo começa sem utilização nem lembrete; a do lote antigo fica.
        Assert.Equal(0, await _db.FerramentaUtilizacoes.AsNoTracking().CountAsync(u => u.ToolId == novoId));
        Assert.Equal(0, await _db.FerramentaLembretesUtilizacao.AsNoTracking().CountAsync(l => l.ToolId == novoId));
        Assert.Equal(1, await _db.FerramentaUtilizacoes.AsNoTracking().CountAsync(u => u.ToolId == sourceId));

        // A fonte fica com o link e a fábrica que tinha.
        var fonteTecnico = await _db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == sourceId);
        Assert.Equal(mcaliper, fonteTecnico.McaliperUrl);
    }
}
