using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// A tabela dos códigos de procura no SAP (Diogo, 28/10/2026: «não deve ser só
/// para BQ», «eu pedi para alterar no UI»): para cada tipo e Linha, como se
/// monta o código que o script procura na ZPPG; o «Código SAP» escrito na
/// ferramenta ganha a tudo. Os códigos pedidos ao SAP e a leitura do CSV usam
/// a mesma regra.
/// </summary>
public sealed class SapProcuraRegrasTests
{
    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-sap-procura-" + Guid.NewGuid().ToString("N"))
            .Options);
        var b1 = new Machine { Code = "B1" };
        var c2 = new Machine { Code = "C2" };
        db.Machines.AddRange(b1, c2);
        db.Tools.AddRange(
            new Tool { ToolId = "cm11", Type = ToolType.Cm, Reference = "5447T173", Lot = "11", TechnicalName = "M544744MPPSX" },
            new Tool { ToolId = "bqB1", Type = ToolType.Bq, Reference = "5447", Lot = "3", TechnicalName = "ABNX123" },
            new Tool { ToolId = "bqC2", Type = ToolType.Bq, Reference = "6001", Lot = "4", TechnicalName = "ABPX456" },
            new Tool { ToolId = "bqSem", Type = ToolType.Bq, Reference = "7001", Lot = "5" });
        db.SaveChanges();
        db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = "bqB1", MachineId = b1.Id },
            new ToolMachineLink { ToolId = "bqC2", MachineId = c2.Id });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public void ACodigo_PorOmissao_EONomeTecnicoComOLote()
    {
        var codigo = SapProcura.Codigo([], ToolType.Cm, [], null, "m5447.44mppsx", "5447T173", "3");

        Assert.Equal("M544744MPPSX03", codigo);
    }

    [Fact]
    public void ARegraDaLinha_GanhaAGeral_EOCodigoAMaoGanhaATudo()
    {
        var regras = new[]
        {
            new SapProcuraRegra(ToolType.Bq, "", "AB", SapProcuraMeio.Referencia, "", true),
            new SapProcuraRegra(ToolType.Bq, "B1", "", SapProcuraMeio.NomeTecnico, "NZ", true)
        };

        Assert.Equal("ABNX123NZ03", SapProcura.Codigo(regras, ToolType.Bq, ["B1"], null, "ABNX123", "5447", "3"));
        Assert.Equal("AB600104", SapProcura.Codigo(regras, ToolType.Bq, ["C2"], null, "ABPX456", "6001", "4"));
        Assert.Equal("XPTO99", SapProcura.Codigo(regras, ToolType.Bq, ["B1"], " xpto 99 ", "ABNX123", "5447", "3"));
        // Sem lote em número não há código (o SAP não o acharia).
        Assert.Null(SapProcura.Codigo(regras, ToolType.Bq, ["C2"], null, "ABPX456", "6001", "A"));
    }

    [Fact]
    public async Task AsTresGerais_EstaoSempre_EAsGravadasLeemSeDeVolta()
    {
        await using var db = CreateContext();
        var regras = new SapProcuraRegras(db);

        Assert.Equal(new[] { ToolType.Cm, ToolType.Mf, ToolType.Bq }, (await regras.LerAsync()).Select(r => r.Tipo));

        Assert.Equal(SapProcuraGravacao.Gravado, await regras.GravarAsync(new SapProcuraRegra(ToolType.Bq, "b1", "ab", SapProcuraMeio.Referencia, "nx", true)));
        Assert.Equal(SapProcuraGravacao.TextoInvalido, await regras.GravarAsync(new SapProcuraRegra(ToolType.Bq, "C2", "AB*", SapProcuraMeio.Referencia, "", true)));
        Assert.Equal(SapProcuraGravacao.LinhaInvalida, await regras.GravarAsync(new SapProcuraRegra(ToolType.Bq, "C 2", "AB", SapProcuraMeio.Referencia, "", true)));

        var lidas = await regras.LerAsync();
        Assert.Contains(new SapProcuraRegra(ToolType.Bq, "B1", "AB", SapProcuraMeio.Referencia, "NX", true), lidas);
        Assert.Equal(4, lidas.Count);

        await regras.ApagarAsync(ToolType.Bq, "B1");
        Assert.Equal(3, (await regras.LerAsync()).Count);
    }

    [Fact]
    public async Task OsCodigosPedidosAoSap_SeguemATabelaEOCodigoSap_DeTodosOsTipos()
    {
        await using var db = CreateContext();
        var regras = new SapProcuraRegras(db);
        await regras.GravarAsync(new SapProcuraRegra(ToolType.Bq, "B1", "AB", SapProcuraMeio.Referencia, "NX", true));
        Assert.True(await regras.GravarCodigoAsync("bqSem", "ab7001zz05"));
        Assert.False(await regras.GravarCodigoAsync("bqSem", "AB*"));

        var codigos = await new FerramentasUsoSap(db, new Sessao()).TodosOsCodigosAsync();

        Assert.Equal(new[] { "AB5447NX03", "AB7001ZZ05", "ABPX45604", "M544744MPPSX11" }, codigos);
        Assert.Equal(("AB7001ZZ05", "AB7001ZZ05"), await regras.DaFerramentaAsync("bqSem"));
        Assert.Equal(("AB5447NX03", (string?)null), await regras.DaFerramentaAsync("bqB1"));

        // Vazio volta à tabela (sem nome técnico, não há código).
        Assert.True(await regras.GravarCodigoAsync("bqSem", " "));
        Assert.Equal(((string?)null, (string?)null), await regras.DaFerramentaAsync("bqSem"));
    }

    [Fact]
    public async Task OCsvDoScript_LigaPeloCodigoProcurado_QuandoONomeTecnicoNaoBate()
    {
        await using var db = CreateContext();
        await new SapProcuraRegras(db).GravarAsync(new SapProcuraRegra(ToolType.Bq, "B1", "AB", SapProcuraMeio.Referencia, "NX", true));
        const string csv = """
            search,equipment,percent_use,last_production,batch_description,status,error
            "AB5447NX03","AB5447NX03","42,5","21.10.2026","AB5447NX","FOUND",""
            """;

        var outcome = await new FerramentasUsoSap(db, new Sessao()).ImportarAsync(csv, "dmo-uso-sap.csv", null);

        Assert.Empty(outcome.SemFerramenta);
        var leitura = Assert.Single(await db.FerramentaUtilizacoes.ToListAsync());
        Assert.Equal("bqB1", leitura.ToolId);
        Assert.Equal(42.5m, leitura.Percentagem);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}
