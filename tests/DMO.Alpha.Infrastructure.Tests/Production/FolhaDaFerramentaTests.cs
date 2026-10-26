using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// No Editar, a ferramenta escolhida traz os dados dela (Diogo, 26/10/2026:
/// «% uso deveria ter passado para 0% ficou com antiga»): a % de uso é a
/// dela, 0 se é nova, e as notas e a técnica são as do último Job On em que
/// foi usada; as quantidades e a utilização desse Job On não vêm.
/// </summary>
public sealed class FolhaDaFerramentaTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly FolhaDaFerramentaQuery _query;

    public FolhaDaFerramentaTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new FolhaDaFerramentaQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> JobOnComCmAsync(string toolId, string numero, DateOnly inicio, string notas, string pata, bool trocada = false)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync() ?? new Machine { Code = "C3" };
        var jobOn = new JobOn { Reference = "9262T288", ProductionNumber = numero, Machine = machine, InicioEm = inicio };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        _db.CmContexts.Add(trocada
            ? new CmContext { ReplacedJobOnId = jobOn.Id, ToolId = toolId }
            : new CmContext { JobOnId = jobOn.Id, ToolId = toolId });
        _db.JobOnFolhas.Add(new JobOnFolha { JobOnId = jobOn.Id, CmNotas = notas, CmPata = pata, CmUtilizacao = "62", CmQtdStock = "4" });
        await _db.SaveChangesAsync();
        return jobOn.Id;
    }

    [Fact]
    public async Task FerramentaNova_Utilizacao0_ESemJobOnNenhum()
    {
        var dados = await _query.ParaAsync(JobOnFolhaCampos.Cm, "CM-9262-17", null);

        Assert.Equal(0m, dados.Utilizacao);
        Assert.Null(dados.DoJobOn);
        Assert.Empty(dados.Campos);
    }

    [Fact]
    public async Task FerramentaUsada_AUltimaPercentagemDela_EAsNotasDoUltimoJobOn()
    {
        _db.FerramentaUtilizacoes.AddRange(
            new FerramentaUtilizacao { ToolId = "CM-9262-17", Percentagem = 10m, RegistadaEmUtc = DateTime.UtcNow },
            new FerramentaUtilizacao { ToolId = "CM-9262-17", Percentagem = 23.5m, RegistadaEmUtc = DateTime.UtcNow });
        await JobOnComCmAsync("CM-9262-17", "202601", new DateOnly(2026, 1, 5), "Antiga", "29");
        var ultimo = await JobOnComCmAsync("CM-9262-17", "202603", new DateOnly(2026, 3, 5), "Rodar 1/4", "29.50", trocada: true);
        var aberto = await JobOnComCmAsync("CM-9262-17", "202605", new DateOnly(2026, 5, 5), "Esta", "30");

        var dados = await _query.ParaAsync(JobOnFolhaCampos.Cm, "CM-9262-17", aberto);

        Assert.Equal(23.5m, dados.Utilizacao);
        Assert.Equal(ultimo, dados.DoJobOn);
        Assert.Equal("Rodar 1/4", dados.Campos["cm_notas"]);
        Assert.Equal("29.50", dados.Campos["cm_pata"]);
        Assert.False(dados.Campos.ContainsKey("cm_utilizacao"));
        Assert.False(dados.Campos.ContainsKey("cm_qtd_stock"));
        Assert.False(dados.Campos.ContainsKey("mf_notas"));
    }
}
