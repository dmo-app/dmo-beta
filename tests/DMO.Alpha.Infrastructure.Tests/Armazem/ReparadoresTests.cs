using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// Os reparadores de CM e MF (0.2.0-beta.8): uma lista à parte dos
/// reparadores das Boquilhas (o mesmo nome pode existir nas duas), com o
/// nome único sem distinguir maiúsculas, pelo menos uma família (CM, MF ou
/// as duas) e arquivar em vez de apagar.
/// </summary>
public sealed class ReparadoresTests
{
    private static DmoDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-reparadores-" + Guid.NewGuid().ToString("N"))
            .Options);

    [Fact]
    public async Task Criar_ComNomeEFamilias_Grava()
    {
        using var db = CreateContext();
        var reparadores = new Reparadores(db);

        Assert.Equal(ReparadorResultado.Gravado, await reparadores.CriarAsync("  Moldes Lda ", aplicaCm: true, aplicaMf: false));

        var item = Assert.Single(await reparadores.ListarAsync());
        Assert.Equal(("Moldes Lda", true, false, true), (item.Nome, item.AplicaCm, item.AplicaMf, item.Ativo));
    }

    [Theory]
    [InlineData(null, true, true, ReparadorResultado.NomeVazio)]
    [InlineData("  ", true, true, ReparadorResultado.NomeVazio)]
    [InlineData("Moldes Lda", false, false, ReparadorResultado.SemFamilia)]
    public async Task Criar_ForaDaRegra_NaoGrava(string? nome, bool cm, bool mf, ReparadorResultado esperado)
    {
        using var db = CreateContext();

        Assert.Equal(esperado, await new Reparadores(db).CriarAsync(nome, cm, mf));
        Assert.Empty(await db.Reparadores.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Criar_NomeLongo_NaoGrava()
    {
        using var db = CreateContext();

        Assert.Equal(ReparadorResultado.NomeLongo, await new Reparadores(db).CriarAsync(new string('x', Reparador.NomeMaxLength + 1), true, true));
    }

    [Fact]
    public async Task Criar_OMesmoNomeComOutrasMaiusculas_JaExiste()
    {
        using var db = CreateContext();
        var reparadores = new Reparadores(db);
        await reparadores.CriarAsync("Moldes Lda", true, true);

        Assert.Equal(ReparadorResultado.JaExiste, await reparadores.CriarAsync("MOLDES LDA", true, false));
        Assert.Single(await db.Reparadores.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Criar_UmNomeQueJaExisteNaOutraLista_Grava()
    {
        // As duas listas não se misturam (Diogo, 18/10/2026).
        using var db = CreateContext();
        db.BqRepairers.Add(new BqRepairer { Name = "Vidroart" });
        await db.SaveChangesAsync();

        Assert.Equal(ReparadorResultado.Gravado, await new Reparadores(db).CriarAsync("Vidroart", true, true));
    }

    [Fact]
    public async Task Mudar_NomeFamiliasEEstado_MudaSoOQueVem()
    {
        using var db = CreateContext();
        var reparadores = new Reparadores(db);
        await reparadores.CriarAsync("Moldes Lda", true, true);
        await reparadores.CriarAsync("Outro", true, true);
        var id = (await db.Reparadores.AsNoTracking().SingleAsync(r => r.Nome == "Moldes Lda")).Id;

        Assert.Equal(ReparadorResultado.Gravado, await reparadores.MudarAsync(id, new MudancaDeReparador(Nome: " Moldes do Norte ")));
        Assert.Equal(ReparadorResultado.Gravado, await reparadores.MudarAsync(id, new MudancaDeReparador(AplicaCm: false, AplicaMf: true)));
        Assert.Equal(ReparadorResultado.Gravado, await reparadores.MudarAsync(id, new MudancaDeReparador(Ativo: false)));

        var r = await db.Reparadores.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(("Moldes do Norte", false, true, false), (r.Nome, r.AplicaCm, r.AplicaMf, r.Ativo));
        // Os ativos vêm primeiro na lista.
        Assert.Equal(["Outro", "Moldes do Norte"], (await reparadores.ListarAsync()).Select(x => x.Nome));
    }

    [Fact]
    public async Task Mudar_ForaDaRegra_NaoGrava()
    {
        using var db = CreateContext();
        var reparadores = new Reparadores(db);
        await reparadores.CriarAsync("Moldes Lda", true, false);
        await reparadores.CriarAsync("Outro", true, true);
        var id = (await db.Reparadores.AsNoTracking().SingleAsync(r => r.Nome == "Moldes Lda")).Id;

        Assert.Equal(ReparadorResultado.SemFamilia, await reparadores.MudarAsync(id, new MudancaDeReparador(AplicaCm: false, AplicaMf: false)));
        Assert.Equal(ReparadorResultado.JaExiste, await reparadores.MudarAsync(id, new MudancaDeReparador(Nome: "outro")));
        Assert.Equal(ReparadorResultado.NomeVazio, await reparadores.MudarAsync(id, new MudancaDeReparador(Nome: " ")));
        Assert.Equal(ReparadorResultado.NaoExiste, await reparadores.MudarAsync(id + 99, new MudancaDeReparador(Ativo: false)));

        var r = await db.Reparadores.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(("Moldes Lda", true, false), (r.Nome, r.AplicaCm, r.AplicaMf));
        // Mudar só o estado com o mesmo nome não é «já existe».
        Assert.Equal(ReparadorResultado.Gravado, await reparadores.MudarAsync(id, new MudancaDeReparador(Nome: "Moldes Lda")));
    }
}
