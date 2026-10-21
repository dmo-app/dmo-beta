using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.ReparacaoInterna;

/// <summary>
/// A Reparação Interna, peça 2 (RI-04): corrigir só o número, com as regras
/// do registo, e anular (a reparação sai da lista e das contagens e fica na
/// base). Quem tem o módulo pode fazê-lo, não só quem registou; cada um
/// deixa uma linha no rasto (antes, depois, quem, quando, motivo opcional).
/// O mesmo envio duas vezes aplica-se uma; uma anulada já não se corrige nem
/// se anula outra vez.
/// </summary>
public sealed class ReparacoesInternasCorrigirTests
{
    // 10:00 em Lisboa (inverno, UTC+0): o dia operacional é 11/03.
    private static readonly DateTimeOffset Manha = new(2030, 3, 11, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid Rui = Guid.Parse("8f0c4c2e-1d1b-4b8e-9a52-0f6d6f1a0b01");

    private static readonly Guid Ana = Guid.Parse("3b7f1d0a-55a2-4c3e-8f61-2a9d7e4c0b02");

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-ri-corrigir-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.AddRange(new Machine { Id = 1, Code = "B1" }, new Machine { Id = 2, Code = "B2" });
        db.Users.AddRange(
            new User { Id = Rui, Name = "Rui Costa", OperatorId = "2165", ProviderUserId = "p-2165" },
            new User { Id = Ana, Name = "Ana Lopes", OperatorId = "2170", ProviderUserId = "p-2170" });
        db.Tools.AddRange(
            new Tool { ToolId = "cm-a", Type = ToolType.Cm, Reference = "5538T324", Lot = "2" },
            new Tool { ToolId = "mf-a", Type = ToolType.Mf, Reference = "5538T324", Lot = "2" });
        db.JobOns.AddRange(
            new JobOn { Id = 11, MachineId = 1, Reference = "5538T324", ProductionNumber = "202511", InicioEm = new DateOnly(2030, 3, 11) },
            new JobOn { Id = 14, MachineId = 2, Reference = "5538T324", ProductionNumber = "202514", InicioEm = new DateOnly(2030, 3, 9) });
        db.CmContexts.AddRange(
            new CmContext { Id = 101, JobOnId = 11, ToolId = "cm-a" },
            new CmContext { Id = 105, JobOnId = 14, ToolId = "cm-a" });
        db.MfContexts.Add(new MfContext { Id = 201, JobOnId = 11, ToolId = "mf-a" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ReparacoesInternas Servico(DmoDbContext db, Guid? quem = null, DateTimeOffset? agora = null) =>
        new(db, new SemHora(), new OperacoesRegistadas(db), new Sessao(quem ?? Rui), new Relogio(agora ?? Manha));

    /// <summary>Uma reparação registada pelo Rui na B1 (ou na B2).</summary>
    private static async Task<int> RegistadaAsync(DmoDbContext db, string numero, int maquina = 1)
    {
        var saida = await Servico(db).RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), maquina, null, ReparacaoTipo.Cm, null, numero));
        Assert.Equal(ReparacaoResultado.Gravado, saida.Resultado);
        return saida.ReparacaoId!.Value;
    }

    private static PedidoDeCorrecao Correcao(int id, string? numero, string? motivo = null) => new(Guid.NewGuid(), id, numero, motivo);

    private static PedidoDeAnulacao Anulacao(int id, string? motivo = null) => new(Guid.NewGuid(), id, motivo);

    [Fact]
    public async Task Corrigir_OutraPessoaComOModulo_MudaSoONumero_EDeixaRasto()
    {
        using var db = CreateContext();
        var id = await RegistadaAsync(db, "0457");

        var saida = await Servico(db, Ana, Manha.AddMinutes(5)).CorrigirAsync(Correcao(id, "  0475 ", "  trocou os algarismos "));

        Assert.Equal((CorrecaoResultado.Corrigido, "0475"), (saida.Resultado, saida.Numero));
        Assert.True(saida.Gravou);
        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        // Só o número muda: quem registou, a hora, a produção e o contexto ficam.
        Assert.Equal(("0475", (Guid?)Rui, Manha.UtcDateTime, 11, (int?)101, (DateTime?)null),
            (r.Numero, r.RegistadoPorUserId, r.RegistadoEmUtc, r.JobOnId, r.CmContextId, r.AnuladaEmUtc));
        var rasto = await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync();
        Assert.Equal((id, ReparacaoAcao.Corrigido, "0457", "0475", (Guid?)Ana, Manha.UtcDateTime.AddMinutes(5), "trocou os algarismos"),
            (rasto.ReparacaoId, rasto.Acao, rasto.NumeroAntes, rasto.NumeroDepois, rasto.PorUserId, rasto.EmUtc, rasto.Motivo));
        var operacao = await db.OperacoesRegistadas.AsNoTracking().OrderByDescending(o => o.MomentoUtc).FirstAsync(o => o.Tipo == ReparacoesInternas.OperacaoCorrigir);
        Assert.Equal((ReparacoesInternas.ResultadoTipo, (int?)id), (operacao.ResultadoTipo, operacao.ResultadoId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Corrigir_SemNumero_Recusa_ENadaMuda(string? numero)
    {
        using var db = CreateContext();
        var id = await RegistadaAsync(db, "12");

        Assert.Equal(CorrecaoResultado.SemNumero, (await Servico(db).CorrigirAsync(Correcao(id, numero))).Resultado);
        Assert.Equal("12", (await db.ReparacoesInternas.AsNoTracking().SingleAsync()).Numero);
        Assert.Empty(await db.ReparacoesInternasRasto.ToListAsync());
    }

    [Theory]
    [InlineData(39, CorrecaoResultado.Corrigido)]
    [InlineData(40, CorrecaoResultado.Corrigido)]
    [InlineData(41, CorrecaoResultado.NumeroLongo)]
    public async Task Corrigir_ONumeroTemAte40Caracteres(int tamanho, CorrecaoResultado esperado)
    {
        using var db = CreateContext();
        var id = await RegistadaAsync(db, "12");
        var numero = new string('7', tamanho);

        var saida = await Servico(db).CorrigirAsync(Correcao(id, " " + numero + " "));

        Assert.Equal((esperado, numero), (saida.Resultado, saida.Numero));
        Assert.Equal(esperado == CorrecaoResultado.Corrigido ? numero : "12", (await db.ReparacoesInternas.AsNoTracking().SingleAsync()).Numero);
        Assert.Equal(esperado == CorrecaoResultado.Corrigido ? 1 : 0, await db.ReparacoesInternasRasto.CountAsync());
    }

    [Theory]
    [InlineData(199, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public async Task OMotivo_EOpcional_ETemAte200Caracteres(int tamanho, bool cabe)
    {
        using var db = CreateContext();
        var corrigida = await RegistadaAsync(db, "1");
        var anulada = await RegistadaAsync(db, "2");
        var motivo = new string('m', tamanho);

        var correcao = await Servico(db).CorrigirAsync(Correcao(corrigida, "10", "  " + motivo + "  "));
        var anulacao = await Servico(db).AnularAsync(Anulacao(anulada, motivo));

        Assert.Equal(cabe ? CorrecaoResultado.Corrigido : CorrecaoResultado.MotivoLongo, correcao.Resultado);
        Assert.Equal(cabe ? CorrecaoResultado.Anulado : CorrecaoResultado.MotivoLongo, anulacao.Resultado);
        Assert.Equal(cabe ? new[] { motivo, motivo } : [], (await db.ReparacoesInternasRasto.AsNoTracking().OrderBy(r => r.Id).ToListAsync()).Select(r => r.Motivo));
        Assert.Equal(cabe ? 1 : 2, (await Servico(db).ListarAsync(1, 11, null)).TotalCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SemMotivo_ORastoFicaSemMotivo(string? motivo)
    {
        using var db = CreateContext();
        var id = await RegistadaAsync(db, "1");

        Assert.Equal(CorrecaoResultado.Corrigido, (await Servico(db).CorrigirAsync(Correcao(id, "2", motivo))).Resultado);
        Assert.Equal(CorrecaoResultado.Anulado, (await Servico(db).AnularAsync(Anulacao(id, motivo))).Resultado);

        Assert.All(await db.ReparacoesInternasRasto.ToListAsync(), r => Assert.Null(r.Motivo));
    }

    [Fact]
    public async Task Corrigir_ParaOMesmoNumero_NadaMuda_SemRasto()
    {
        using var db = CreateContext();
        var id = await RegistadaAsync(db, "33");

        Assert.Equal((CorrecaoResultado.Igual, "33"), Par(await Servico(db).CorrigirAsync(Correcao(id, " 33 "))));
        Assert.Empty(await db.ReparacoesInternasRasto.ToListAsync());
    }

    [Fact]
    public async Task Anular_OutraPessoa_SaiDaListaEDasContagens_FicaNaBase_ComRasto()
    {
        using var db = CreateContext();
        var fica = await RegistadaAsync(db, "1");
        var sai = await RegistadaAsync(db, "2");
        await RegistadaAsync(db, "noutra", maquina: 2);

        var saida = await Servico(db, Ana, Manha.AddMinutes(9)).AnularAsync(Anulacao(sai, "máquina errada"));

        Assert.Equal((CorrecaoResultado.Anulado, "2"), Par(saida));
        var lista = await Servico(db).ListarAsync(1, 11, null);
        Assert.Equal((1, 1, fica), (lista.TotalCount, lista.TotalPages, Assert.Single(lista.Items).Id));
        var anulada = await db.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.Id == sai);
        Assert.Equal(("2", (DateTime?)Manha.UtcDateTime.AddMinutes(9)), (anulada.Numero, anulada.AnuladaEmUtc));
        var rasto = await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync();
        Assert.Equal((sai, ReparacaoAcao.Anulado, "2", (string?)null, (Guid?)Ana, Manha.UtcDateTime.AddMinutes(9), "máquina errada"),
            (rasto.ReparacaoId, rasto.Acao, rasto.NumeroAntes, rasto.NumeroDepois, rasto.PorUserId, rasto.EmUtc, rasto.Motivo));
    }

    [Fact]
    public async Task AsAnuladas_NaoContamNasPaginas()
    {
        using var db = CreateContext();
        var ids = new List<int>();
        for (var i = 1; i <= 27; i++)
        {
            ids.Add(await RegistadaAsync(db, i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        await Servico(db).AnularAsync(Anulacao(ids[^1]));
        await Servico(db).AnularAsync(Anulacao(ids[0]));

        var primeira = await Servico(db).ListarAsync(1, 11, null);
        Assert.Equal((25, 25, 1), (primeira.TotalCount, primeira.Items.Count, primeira.TotalPages));
        Assert.Equal("26", primeira.Items[0].Numero);
        Assert.Equal("2", primeira.Items[^1].Numero);
    }

    [Fact]
    public async Task UmaAnulada_NaoSeCorrigeNemSeAnulaOutraVez()
    {
        using var db = CreateContext();
        var id = await RegistadaAsync(db, "5");
        await Servico(db, Ana).AnularAsync(Anulacao(id));

        Assert.Equal((CorrecaoResultado.JaAnulada, "5"), Par(await Servico(db).CorrigirAsync(Correcao(id, "6"))));
        Assert.Equal((CorrecaoResultado.JaAnulada, "5"), Par(await Servico(db).AnularAsync(Anulacao(id))));
        Assert.False((await Servico(db).AnularAsync(Anulacao(id))).Gravou);
        Assert.Equal("5", (await db.ReparacoesInternas.AsNoTracking().SingleAsync()).Numero);
        Assert.Single(await db.ReparacoesInternasRasto.ToListAsync());
    }

    [Fact]
    public async Task UmaQueJaNaoExiste_NadaMuda()
    {
        using var db = CreateContext();

        Assert.Equal(CorrecaoResultado.NaoExiste, (await Servico(db).CorrigirAsync(Correcao(999, "1"))).Resultado);
        Assert.Equal(CorrecaoResultado.NaoExiste, (await Servico(db).AnularAsync(Anulacao(999))).Resultado);
        Assert.Empty(await db.ReparacoesInternasRasto.ToListAsync());
    }

    [Fact]
    public async Task OMesmoEnvioDuasVezes_AplicaUmaVez()
    {
        using var db = CreateContext();
        var corrigida = await RegistadaAsync(db, "1");
        var anulada = await RegistadaAsync(db, "2");
        var correcao = Correcao(corrigida, "10");
        var anulacao = Anulacao(anulada);

        Assert.Equal(CorrecaoResultado.Corrigido, (await Servico(db).CorrigirAsync(correcao)).Resultado);
        var repetida = await Servico(db).CorrigirAsync(correcao);
        Assert.Equal(CorrecaoResultado.Anulado, (await Servico(db).AnularAsync(anulacao)).Resultado);
        var repetidaAnulacao = await Servico(db).AnularAsync(anulacao);

        Assert.Equal((CorrecaoResultado.JaFeito, "10"), Par(repetida));
        Assert.True(repetida.Gravou);
        Assert.Equal((CorrecaoResultado.JaFeito, "2"), Par(repetidaAnulacao));
        Assert.Equal(2, await db.ReparacoesInternasRasto.CountAsync());
        Assert.Equal(2, await db.OperacoesRegistadas.CountAsync(o => o.Tipo == ReparacoesInternas.OperacaoCorrigir || o.Tipo == ReparacoesInternas.OperacaoAnular));
    }

    [Fact]
    public async Task DuasCorrecoes_DoisRastos_PelaOrdem()
    {
        using var db = CreateContext();
        var id = await RegistadaAsync(db, "1");

        await Servico(db, Ana, Manha.AddMinutes(1)).CorrigirAsync(Correcao(id, "2"));
        await Servico(db, Rui, Manha.AddMinutes(2)).CorrigirAsync(Correcao(id, "3"));
        await Servico(db, Ana, Manha.AddMinutes(3)).AnularAsync(Anulacao(id));

        Assert.Equal(
            new[] { (ReparacaoAcao.Corrigido, "1", "2", (Guid?)Ana), (ReparacaoAcao.Corrigido, "2", "3", (Guid?)Rui), (ReparacaoAcao.Anulado, "3", (string?)null, (Guid?)Ana) },
            (await db.ReparacoesInternasRasto.AsNoTracking().OrderBy(r => r.Id).ToListAsync()).Select(r => (r.Acao, r.NumeroAntes, r.NumeroDepois, r.PorUserId)));
    }

    [Fact]
    public async Task EliminarOJobOn_ContaTodas_ELevaAsReparacoesEORasto()
    {
        using var db = CreateContext();
        var corrigida = await RegistadaAsync(db, "1");
        var anulada = await RegistadaAsync(db, "2");
        var noutra = await RegistadaAsync(db, "3", maquina: 2);
        await Servico(db).CorrigirAsync(Correcao(corrigida, "10"));
        await Servico(db).AnularAsync(Anulacao(anulada));
        await Servico(db).CorrigirAsync(Correcao(noutra, "30"));

        // As anuladas também se perdem com a produção: contam no aviso.
        Assert.Equal(2, (await new DeleteJobOnHandler(db).PreviewAsync(11, new DateOnly(2030, 3, 11)))!.ReparacoesInternas);
        Assert.Equal(DeleteJobOnResult.Success, await new DeleteJobOnHandler(db).HandleAsync(new DeleteJobOn(11)));

        Assert.Equal(noutra, Assert.Single(await db.ReparacoesInternas.ToListAsync()).Id);
        Assert.Equal(noutra, Assert.Single(await db.ReparacoesInternasRasto.ToListAsync()).ReparacaoId);
    }

    private static (CorrecaoResultado, string?) Par(CorrecaoSaida saida) => (saida.Resultado, saida.Numero);

    private sealed class SemHora : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class Relogio(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class Sessao(Guid quem) : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = quem, DisplayName = "Operador" };
    }
}
