using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.ReparacaoInterna;

/// <summary>
/// A Consulta da Reparação Interna, peça 3 (RI-05): sem filtro não lê nada;
/// cada filtro (a máquina, os dias operacionais, quem, CM ou MF, a
/// referência pelo início e a produção escolhida) sozinho e juntos; os dias
/// de / até inclusive; as anuladas ficam de fora; da mais recente para a
/// mais antiga, em páginas de 25. A ferramenta vem do contexto (referência
/// e lote); os utilizadores do filtro «Quem» são só os que registaram.
/// </summary>
public sealed class ReparacoesInternasConsultaTests
{
    private static readonly Guid Rui = Guid.Parse("4b3c2a10-0000-4000-8000-000000000001");
    private static readonly Guid Ana = Guid.Parse("4b3c2a10-0000-4000-8000-000000000002");
    private static readonly Guid Ze = Guid.Parse("4b3c2a10-0000-4000-8000-000000000003");

    private static readonly DateOnly Dia10 = new(2030, 3, 10);
    private static readonly DateOnly Dia11 = new(2030, 3, 11);
    private static readonly DateOnly Dia12 = new(2030, 3, 12);

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-ri-consulta-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.AddRange(new Machine { Id = 1, Code = "B1" }, new Machine { Id = 2, Code = "B2" });
        db.Users.AddRange(
            new User { Id = Rui, Name = "Rui Costa", OperatorId = "2165", ProviderUserId = "p-2165" },
            new User { Id = Ana, Name = "Ana Lopes", OperatorId = "2166", ProviderUserId = "p-2166" },
            // O Zé nunca registou: não aparece no filtro «Quem».
            new User { Id = Ze, Name = "Zé Sousa", OperatorId = "2167", ProviderUserId = "p-2167" });
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = "cm-a", Type = ToolType.Cm, Reference = "5538T324", Lot = "2", TechnicalName = "Molde técnico secreto" },
            new Tool { ToolId = "mf-a", Type = ToolType.Mf, Reference = "5538T324", Lot = "3" },
            new Tool { ToolId = "cm-b", Type = ToolType.Cm, Reference = "6012A118", Lot = "4" });
        db.JobOns.AddRange(
            new JobOn { Id = 10, MachineId = 1, Reference = "5538T324", ProductionNumber = "202510" },
            new JobOn { Id = 11, MachineId = 2, Reference = "6012A118", ProductionNumber = "202511" },
            new JobOn { Id = 12, MachineId = 1, Reference = "6020X101", ProductionNumber = "202512" });
        db.CmContexts.AddRange(
            new CmContext { Id = 100, JobOnId = 10, ToolId = "cm-a" },
            new CmContext { Id = 101, JobOnId = 11, ToolId = "cm-b" },
            // A CM da 202512 foi eliminada: o contexto passou para a REMOVED.
            new CmContext { Id = 102, JobOnId = 12, ToolId = ToolRegistryConstants.RemovedToolId });
        db.MfContexts.Add(new MfContext { Id = 200, JobOnId = 10, ToolId = "mf-a" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    /// <summary>
    /// As seis do costume: 1 a 5 contam, a 6 está anulada.
    /// 1 B1 202510 CM 10/03 Rui · 2 B1 202510 MF 11/03 Ana · 3 B2 202511 CM 11/03 Rui
    /// · 4 B2 202511 CM 12/03 Ana · 5 B1 202512 CM 12/03 Rui · 6 B1 202510 CM 11/03 Rui (anulada).
    /// </summary>
    private static DmoDbContext ComAsSeis()
    {
        var db = CreateContext();
        db.ReparacoesInternas.AddRange(
            Rep(1, 10, ReparacaoTipo.Cm, Dia10, Rui),
            Rep(2, 10, ReparacaoTipo.Mf, Dia11, Ana),
            Rep(3, 11, ReparacaoTipo.Cm, Dia11, Rui),
            Rep(4, 11, ReparacaoTipo.Cm, Dia12, Ana),
            Rep(5, 12, ReparacaoTipo.Cm, Dia12, Rui),
            Rep(6, 10, ReparacaoTipo.Cm, Dia11, Rui, anulada: true));
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static Reparacao Rep(int id, int jobOn, ReparacaoTipo tipo, DateOnly dia, Guid? quem, bool anulada = false)
    {
        var maquina = jobOn == 11 ? 2 : 1;
        var contexto = (jobOn, tipo) switch
        {
            (10, ReparacaoTipo.Cm) => 100,
            (11, ReparacaoTipo.Cm) => 101,
            (12, ReparacaoTipo.Cm) => 102,
            _ => 200
        };
        var registo = dia.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc).AddMinutes(id);
        return new Reparacao
        {
            Id = id, JobOnId = jobOn, MachineId = maquina, Tipo = tipo,
            CmContextId = tipo == ReparacaoTipo.Cm ? contexto : null,
            MfContextId = tipo == ReparacaoTipo.Mf ? contexto : null,
            Numero = $"N{id}", RegistadoPorUserId = quem, RegistadoEmUtc = registo, DiaOperacional = dia,
            AnuladaEmUtc = anulada ? registo.AddMinutes(1) : null
        };
    }

    private static async Task<int[]> IdsAsync(DmoDbContext db, RiConsultaFiltro filtro) =>
        (await new ReparacoesInternasConsulta(db).ConsultarAsync(filtro, null)).Items.Select(l => l.Id).ToArray();

    [Fact]
    public async Task SemFiltro_NaoLeNada()
    {
        using var db = ComAsSeis();

        var lista = await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(), null);

        Assert.Empty(lista.Items);
        Assert.Equal(0, lista.TotalCount);
        // Espaços na referência não contam como filtro.
        Assert.False(new RiConsultaFiltro(Referencia: "  ").ComFiltro);
    }

    [Theory]
    [InlineData("maquina-b1", new[] { 5, 2, 1 })]
    [InlineData("maquina-b2", new[] { 4, 3 })]
    [InlineData("de-11", new[] { 5, 4, 3, 2 })]
    [InlineData("ate-11", new[] { 3, 2, 1 })]
    [InlineData("pessoa-rui", new[] { 5, 3, 1 })]
    [InlineData("pessoa-ana", new[] { 4, 2 })]
    [InlineData("tipo-cm", new[] { 5, 4, 3, 1 })]
    [InlineData("tipo-mf", new[] { 2 })]
    [InlineData("referencia-60", new[] { 5, 4, 3 })]
    [InlineData("referencia-5538", new[] { 2, 1 })]
    [InlineData("referencia-a1", new[] { 4, 3 })]
    [InlineData("referencia-9", new int[0])]
    [InlineData("producao-11", new[] { 4, 3 })]
    [InlineData("producao-manda", new[] { 2, 1 })]
    public async Task CadaFiltroSozinho(string caso, int[] esperado)
    {
        using var db = ComAsSeis();
        var filtro = caso switch
        {
            "maquina-b1" => new RiConsultaFiltro(MachineId: 1),
            "maquina-b2" => new RiConsultaFiltro(MachineId: 2),
            "de-11" => new RiConsultaFiltro(De: Dia11),
            "ate-11" => new RiConsultaFiltro(Ate: Dia11),
            "pessoa-rui" => new RiConsultaFiltro(PessoaId: Rui),
            "pessoa-ana" => new RiConsultaFiltro(PessoaId: Ana),
            "tipo-cm" => new RiConsultaFiltro(Tipo: ReparacaoTipo.Cm),
            "tipo-mf" => new RiConsultaFiltro(Tipo: ReparacaoTipo.Mf),
            // Pelo início da referência: «60» traz todas as que começam por 60.
            "referencia-60" => new RiConsultaFiltro(Referencia: "60"),
            "referencia-5538" => new RiConsultaFiltro(Referencia: " 5538 "),
            // A parte BQ, como nas outras procuras (sem distinção de maiúsculas).
            "referencia-a1" => new RiConsultaFiltro(Referencia: "a1"),
            "referencia-9" => new RiConsultaFiltro(Referencia: "9"),
            "producao-11" => new RiConsultaFiltro(JobOnId: 11),
            // A produção escolhida manda sobre o texto da referência.
            "producao-manda" => new RiConsultaFiltro(Referencia: "60", JobOnId: 10),
            _ => throw new ArgumentOutOfRangeException(nameof(caso))
        };

        Assert.Equal(esperado, await IdsAsync(db, filtro));
    }

    [Theory]
    [InlineData("b1-rui-cm", new[] { 5, 1 })]
    [InlineData("b2-desde-12", new[] { 4 })]
    [InlineData("referencia-60-ana", new[] { 4 })]
    [InlineData("b1-dia-11", new[] { 2 })]
    [InlineData("b1-dias-11-12-cm", new[] { 5 })]
    [InlineData("producao-10-rui", new[] { 1 })]
    [InlineData("b2-mf", new int[0])]
    public async Task OsFiltrosJuntos(string caso, int[] esperado)
    {
        using var db = ComAsSeis();
        var filtro = caso switch
        {
            "b1-rui-cm" => new RiConsultaFiltro(MachineId: 1, PessoaId: Rui, Tipo: ReparacaoTipo.Cm),
            "b2-desde-12" => new RiConsultaFiltro(MachineId: 2, De: Dia12),
            "referencia-60-ana" => new RiConsultaFiltro(PessoaId: Ana, Referencia: "60"),
            "b1-dia-11" => new RiConsultaFiltro(MachineId: 1, De: Dia11, Ate: Dia11),
            "b1-dias-11-12-cm" => new RiConsultaFiltro(MachineId: 1, De: Dia11, Ate: Dia12, Tipo: ReparacaoTipo.Cm),
            "producao-10-rui" => new RiConsultaFiltro(PessoaId: Rui, JobOnId: 10),
            "b2-mf" => new RiConsultaFiltro(MachineId: 2, Tipo: ReparacaoTipo.Mf),
            _ => throw new ArgumentOutOfRangeException(nameof(caso))
        };

        Assert.Equal(esperado, await IdsAsync(db, filtro));
    }

    [Theory]
    // Uma reparação no dia 11: «de» no dia antes, no próprio e no dia depois.
    [InlineData("2030-03-10", null, true)]
    [InlineData("2030-03-11", null, true)]
    [InlineData("2030-03-12", null, false)]
    // «Até» no dia antes, no próprio e no dia depois.
    [InlineData(null, "2030-03-10", false)]
    [InlineData(null, "2030-03-11", true)]
    [InlineData(null, "2030-03-12", true)]
    // O intervalo de um só dia: o dia antes, o próprio e o dia depois.
    [InlineData("2030-03-10", "2030-03-10", false)]
    [InlineData("2030-03-11", "2030-03-11", true)]
    [InlineData("2030-03-12", "2030-03-12", false)]
    public async Task OsDias_DeEAteInclusive(string? de, string? ate, bool aparece)
    {
        using var db = CreateContext();
        db.ReparacoesInternas.Add(Rep(1, 10, ReparacaoTipo.Cm, Dia11, Rui));
        await db.SaveChangesAsync();

        var ids = await IdsAsync(db, new RiConsultaFiltro(
            De: de is null ? null : DateOnly.Parse(de, System.Globalization.CultureInfo.InvariantCulture),
            Ate: ate is null ? null : DateOnly.Parse(ate, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(aparece ? new[] { 1 } : [], ids);
    }

    [Fact]
    public async Task AsAnuladas_FicamDeFora()
    {
        using var db = ComAsSeis();

        // A 6 (anulada) é da B1, do Rui, CM, do dia 11 e da 202510: nenhum filtro a traz.
        var lista = await new ReparacoesInternasConsulta(db).ConsultarAsync(
            new RiConsultaFiltro(MachineId: 1, De: Dia11, Ate: Dia11, PessoaId: Rui, Tipo: ReparacaoTipo.Cm, JobOnId: 10), null);

        Assert.Empty(lista.Items);
        Assert.Equal(0, lista.TotalCount);
        Assert.DoesNotContain(6, await IdsAsync(db, new RiConsultaFiltro(MachineId: 1)));
    }

    [Theory]
    [InlineData(24, 1, 24, 0)]
    [InlineData(25, 1, 25, 0)]
    [InlineData(26, 2, 25, 1)]
    public async Task EmPaginasDe25(int quantas, int paginas, int naPrimeira, int naSegunda)
    {
        using var db = CreateContext();
        for (var i = 1; i <= quantas; i++)
        {
            db.ReparacoesInternas.Add(Rep(i, 10, ReparacaoTipo.Cm, Dia11, Rui));
        }

        // Uma anulada a mais não conta para as páginas.
        db.ReparacoesInternas.Add(Rep(quantas + 1, 10, ReparacaoTipo.Cm, Dia11, Rui, anulada: true));
        await db.SaveChangesAsync();
        var consulta = new ReparacoesInternasConsulta(db);
        var filtro = new RiConsultaFiltro(MachineId: 1);

        var primeira = await consulta.ConsultarAsync(filtro, 1);
        var segunda = await consulta.ConsultarAsync(filtro, 2);

        Assert.Equal(ListPaging.DefaultPageSize, primeira.PageSize);
        Assert.Equal((quantas, paginas, naPrimeira), (primeira.TotalCount, primeira.TotalPages, primeira.Items.Count));
        // A segunda página fora do fim volta à última; com duas, tem a mais antiga.
        Assert.Equal(paginas, segunda.PageNumber);
        if (naSegunda > 0)
        {
            Assert.Equal(new[] { 1 }, segunda.Items.Select(l => l.Id));
        }

        Assert.Equal(quantas, primeira.Items[0].Id);
    }

    [Fact]
    public async Task DaMaisRecenteParaAMaisAntiga()
    {
        using var db = ComAsSeis();

        var ids = await IdsAsync(db, new RiConsultaFiltro(De: Dia10));

        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, ids);
    }

    [Fact]
    public async Task ALinha_AProducaoAFerramentaEQuem_NuncaONomeTecnico()
    {
        using var db = ComAsSeis();
        // Uma reparação do ADMIN (sem utilizador) também aparece, sem nome.
        db.ReparacoesInternas.Add(Rep(7, 11, ReparacaoTipo.Cm, Dia12, null));
        await db.SaveChangesAsync();

        var linhas = (await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(De: Dia10), null)).Items.ToDictionary(l => l.Id);

        var mf = linhas[2];
        Assert.Equal((Dia11, "B1", 10, "202510", "5538T324", ReparacaoTipo.Mf, "N2", "Ana Lopes"),
            (mf.Dia, mf.Maquina, mf.JobOnId, mf.Producao, mf.Referencia, mf.Tipo, mf.Numero, mf.Quem));
        Assert.Equal(new RiFerramenta(200, "5538T324", "3"), mf.Ferramenta);
        Assert.Equal(new RiFerramenta(100, "5538T324", "2"), linhas[1].Ferramenta);
        Assert.Equal(new RiFerramenta(101, "6012A118", "4"), linhas[3].Ferramenta);
        // A CM eliminada: o contexto fica, sem referência nem lote.
        Assert.Equal(new RiFerramenta(102, null, null), linhas[5].Ferramenta);
        Assert.Null(linhas[7].Quem);
        Assert.Equal(new DateTime(2030, 3, 11, 9, 2, 0, DateTimeKind.Utc), mf.RegistadoEmUtc);
        Assert.DoesNotContain(linhas.Values, l => l.ToString().Contains("Molde técnico secreto", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OFiltroQuem_SoOsUtilizadoresQueRegistaram_PorNome()
    {
        using var db = ComAsSeis();
        db.ReparacoesInternas.Add(Rep(7, 11, ReparacaoTipo.Cm, Dia12, null));
        await db.SaveChangesAsync();

        var pessoas = await new ReparacoesInternasConsulta(db).PessoasAsync();

        Assert.Equal(new[] { new RiPessoa(Ana, "Ana Lopes"), new RiPessoa(Rui, "Rui Costa") }, pessoas);
    }

    [Fact]
    public async Task ResumoPorPessoa_ContaReparacoesDiasEMedia_SemAsAnuladas()
    {
        using var db = ComAsSeis();
        var consulta = new ReparacoesInternasConsulta(db);

        var resumo = await consulta.ResumoPorPessoaAsync(new RiConsultaFiltro(De: Dia10));

        // Rui: 1, 3 e 5 em três dias (a 6 está anulada); Ana: 2 e 4 em dois dias.
        Assert.Collection(resumo,
            rui => Assert.Equal(("Rui Costa", 3, 3, 1.00m), (rui.Nome, rui.Reparacoes, rui.Dias, rui.MediaPorDia)),
            ana => Assert.Equal(("Ana Lopes", 2, 2, 1.00m), (ana.Nome, ana.Reparacoes, ana.Dias, ana.MediaPorDia)));
        Assert.Empty(await consulta.ResumoPorPessoaAsync(new RiConsultaFiltro()));
    }
}
