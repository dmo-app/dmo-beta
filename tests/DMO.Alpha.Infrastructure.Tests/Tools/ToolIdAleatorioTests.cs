using System.Globalization;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// O tool_id de uma ferramenta nova é aleatório, NUNCA sequencial (ToolIdMinter).
/// Audit externo S5, 16/11/2026 (M2): um mint sequencial passava nos testes.
/// </summary>
public sealed class ToolIdAleatorioTests : IDisposable
{
    private readonly DmoDbContext _db;

    public ToolIdAleatorioTests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Garante: cada ferramenta criada tem um tool_id «T» + 10 hex, diferente, e os ids não vão em sequência.</summary>
    [Fact]
    public async Task Criar_DaToolIdsAleatorios_NuncaEmSequencia()
    {
        var maquina = _db.Machines.Single().Id;
        var ids = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            var criada = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db), new CodificacaoFerramentas(_db))
                .HandleAsync(new CreateTool("CM", "7" + i.ToString("000", CultureInfo.InvariantCulture), "1", MachineIds: [maquina]));
            ids.Add(criada.Tool!.ToolId);
        }

        Assert.All(ids, id => Assert.Matches("^T[0-9a-f]{10}$", id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var numeros = ids.Select(id => long.Parse(id[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToList();
        var passos = numeros.Zip(numeros.Skip(1), (a, b) => b - a).ToList();
        // Oito ids aleatórios de 40 bits nunca ficam todos a um passo pequeno e fixo.
        Assert.False(passos.All(p => p > 0 && p < 1000), "os tool_id estão em sequência");
        Assert.True(passos.Distinct().Count() > 1, "os tool_id avançam sempre o mesmo passo");
    }
}
