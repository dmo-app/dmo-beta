using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// As fábricas da BA (Diogo, 28/10/2026): começa vazia; sigla de duas letras,
/// única, em maiúsculas; não se apaga, desativa-se; e o Empréstimo filtra-se
/// pela fábrica da última Saída.
/// </summary>
public sealed class FabricasBaTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly FabricasBa _fabricas;

    public FabricasBaTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _fabricas = new FabricasBa(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Comeca_vazia_e_acrescenta_com_sigla_de_duas_letras_unica()
    {
        Assert.Empty(await _fabricas.ListarAsync());

        Assert.Equal(FabricaBaResultado.Gravada, await _fabricas.CriarAsync(" mg ", " Fábrica MG "));
        Assert.Equal(FabricaBaResultado.SiglaInvalida, await _fabricas.CriarAsync("M", "Curta"));
        Assert.Equal(FabricaBaResultado.SiglaInvalida, await _fabricas.CriarAsync("M1", "Com número"));
        Assert.Equal(FabricaBaResultado.NomeVazio, await _fabricas.CriarAsync("LE", " "));
        Assert.Equal(FabricaBaResultado.JaExiste, await _fabricas.CriarAsync("Mg", "Outra"));

        var mg = Assert.Single(await _fabricas.ListarAsync());
        Assert.Equal(("MG", "Fábrica MG", true), (mg.Sigla, mg.Nome, mg.Ativa));
    }

    [Fact]
    public async Task Desativa_sem_apagar_e_MG_e_a_proposta()
    {
        await _fabricas.CriarAsync("LE", "Fábrica LE");
        await _fabricas.CriarAsync("MG", "Fábrica MG");
        Assert.Equal("MG", FabricasBaTextos.PorOmissao(await _fabricas.ListarAsync()));

        var mg = (await _fabricas.ListarAsync()).Single(f => f.Sigla == "MG");
        Assert.Equal(FabricaBaResultado.Gravada, await _fabricas.MudarAsync(mg.Id, null, false));
        var lista = await _fabricas.ListarAsync();
        Assert.Equal(["LE", "MG"], lista.Select(f => f.Sigla));
        Assert.False(lista.Single(f => f.Sigla == "MG").Ativa);
        Assert.Equal("LE", FabricasBaTextos.PorOmissao(lista));
        Assert.Equal(FabricaBaResultado.NaoExiste, await _fabricas.MudarAsync(999, "X", null));
    }

    [Fact]
    public async Task Emprestimo_filtra_pela_fabrica_da_ultima_saida()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "EMP-LE", Type = ToolType.Cm, Reference = "1075", Lot = "1" },
            new Tool { ToolId = "EMP-SO", Type = ToolType.Cm, Reference = "1075", Lot = "2" });
        _db.ArmazemMovimentos.AddRange(
            new ArmazemMovimento { Id = 10, Tipo = ArmazemMovimentoTipo.Saida, ToolId = "EMP-LE", Destino = ArmazemDestino.Emprestado, FabricaDestino = "LE" },
            new ArmazemMovimento { Id = 11, Tipo = ArmazemMovimentoTipo.Saida, ToolId = "EMP-SO", Destino = ArmazemDestino.Emprestado, FabricaDestino = "SO" });
        _db.FerramentaLocalizacoes.AddRange(
            new FerramentaLocalizacao { ToolId = "EMP-LE", Localizacao = FerramentaLocalizacaoTipo.Emprestado, MovimentoId = 10 },
            new FerramentaLocalizacao { ToolId = "EMP-SO", Localizacao = FerramentaLocalizacaoTipo.Emprestado, MovimentoId = 11 });
        await _db.SaveChangesAsync();

        var query = new ToolCandidateQuery(_db);
        var le = await query.FindCandidatesAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Emprestado, Fabrica: "le")));
        Assert.Equal("EMP-LE", Assert.Single(le).ToolId);
        Assert.Equal(2, (await query.FindCandidatesAsync(new ToolCandidateFilter(Local: new(ToolCandidateLocalTipo.Emprestado)))).Count);
    }
}
