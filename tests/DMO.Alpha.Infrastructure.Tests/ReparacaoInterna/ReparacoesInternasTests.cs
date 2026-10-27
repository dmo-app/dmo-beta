using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.ReparacaoInterna;

/// <summary>
/// A Reparação Interna, peça 1 (Diogo, 17/10/2026): a produção do dia de cada
/// máquina pelas datas do Job On e pela hora de viragem da Reparação Interna
/// (8 por omissão); uma produção sem datas não conta; com duas ou mais a de
/// maior jobon_id vem escolhida. Registar grava uma linha curta com o
/// cm_id ou o mf_id do momento, o número sem espaços nas pontas (até 40) e
/// quem registou; o mesmo envio duas vezes grava uma. Registar não mexe em
/// mais nada.
/// </summary>
public sealed class ReparacoesInternasTests
{
    // 10:00 em Lisboa (inverno, UTC+0): o dia operacional é 11/03.
    private static readonly DateTimeOffset Manha = new(2030, 3, 11, 10, 0, 0, TimeSpan.Zero);

    // 07:30: antes da viragem das 8h ainda é 10/03.
    private static readonly DateTimeOffset AntesDaViragem = new(2030, 3, 11, 7, 30, 0, TimeSpan.Zero);

    private static readonly Guid Rui = Guid.Parse("8f0c4c2e-1d1b-4b8e-9a52-0f6d6f1a0b01");

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-reparacao-interna-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.AddRange(new Machine { Id = 1, Code = "B1" }, new Machine { Id = 2, Code = "B2" }, new Machine { Id = 3, Code = "C1" });
        db.Users.Add(new User { Id = Rui, Name = "Rui Costa", OperatorId = "2165", ProviderUserId = "p-2165" });
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = "cm-a", Type = ToolType.Cm, Reference = "5538T324", Lot = "2" },
            new Tool { ToolId = "mf-a", Type = ToolType.Mf, Reference = "5538T324", Lot = "2" },
            new Tool { ToolId = "cm-b", Type = ToolType.Cm, Reference = "6012A118", Lot = "4" },
            new Tool { ToolId = "mf-b", Type = ToolType.Mf, Reference = "6012A118", Lot = "4" });
        db.JobOns.AddRange(
            // B1: a que acabou ontem, a de hoje sem fim e uma sem datas.
            JobOn(10, 1, "202510", new DateOnly(2030, 3, 8), new DateOnly(2030, 3, 10)),
            JobOn(11, 1, "202511", new DateOnly(2030, 3, 11), null),
            JobOn(12, 1, "202512", null, null),
            // B2: duas que cobrem hoje.
            JobOn(13, 2, "202513", new DateOnly(2030, 3, 11), new DateOnly(2030, 3, 11)),
            JobOn(14, 2, "202514", new DateOnly(2030, 3, 9), new DateOnly(2030, 3, 15)),
            // C1: só uma sem datas e uma acabada.
            JobOn(15, 3, "202515", null, null),
            JobOn(16, 3, "202516", new DateOnly(2030, 3, 1), new DateOnly(2030, 3, 5)));
        db.CmContexts.AddRange(
            new CmContext { Id = 100, JobOnId = 10, ToolId = "cm-a" },
            new CmContext { Id = 101, JobOnId = 11, ToolId = "cm-b" },
            // A CM trocada a meio da 202511 (beta.231): o cm_id antigo fica com a produção.
            new CmContext { Id = 103, ReplacedJobOnId = 11, ReplacedAtUtc = Manha.UtcDateTime.AddDays(-1), ToolId = "cm-a" },
            new CmContext { Id = 102, JobOnId = 12, ToolId = "cm-a" },
            new CmContext { Id = 104, JobOnId = 13, ToolId = "cm-a" },
            new CmContext { Id = 105, JobOnId = 14, ToolId = "cm-b" });
        db.MfContexts.AddRange(
            new MfContext { Id = 200, JobOnId = 10, ToolId = "mf-a" },
            new MfContext { Id = 201, JobOnId = 11, ToolId = "mf-b" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static JobOn JobOn(int id, int maquina, string producao, DateOnly? inicio, DateOnly? fim) => new()
    {
        Id = id, MachineId = maquina, Reference = producao == "202511" ? "6012A118" : "5538T324", ProductionNumber = producao, InicioEm = inicio, FimEm = fim
    };

    private static ReparacoesInternas Servico(DmoDbContext db, DateTimeOffset? agora = null, string? hora = null) =>
        new(db, new Horas(hora), new OperacoesRegistadas(db), new Sessao(), new Relogio(agora ?? Manha));

    private static PedidoDeReparacao Pedido(string? numero, ReparacaoTipo? tipo = ReparacaoTipo.Cm, int? maquina = 1, int? jobOn = null, int? contexto = null) =>
        new(Guid.NewGuid(), maquina, jobOn, tipo, contexto, numero);

    [Theory]
    [InlineData("2030-03-11T07:59:00Z", null, "2030-03-10")]
    [InlineData("2030-03-11T08:00:00Z", null, "2030-03-11")]
    [InlineData("2030-07-11T06:59:00Z", null, "2030-07-10")]
    [InlineData("2030-07-11T07:00:00Z", null, "2030-07-11")]
    [InlineData("2030-03-11T06:00:00Z", "6", "2030-03-11")]
    [InlineData("2030-03-11T05:59:00Z", "6", "2030-03-10")]
    public async Task ODiaOperacional_ViraAHoraDaReparacaoInterna(string instante, string? hora, string esperado)
    {
        using var db = CreateContext();

        var dia = await Servico(db, DateTimeOffset.Parse(instante, System.Globalization.CultureInfo.InvariantCulture), hora).DiaOperacionalAsync();

        Assert.Equal(DateOnly.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), dia);
    }

    [Fact]
    public async Task AHoraDeOutroModulo_NaoMudaADaReparacaoInterna()
    {
        using var db = CreateContext();
        var soBoquilhas = new ReparacoesInternas(db, new SoBoquilhas(), new OperacoesRegistadas(db), new Sessao(), new Relogio(new DateTimeOffset(2030, 3, 11, 6, 30, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2030, 3, 10), await soBoquilhas.DiaOperacionalAsync());
    }

    [Fact]
    public async Task UmaProducao_ADeHojeSemFim_AsSemDatasNaoContam()
    {
        using var db = CreateContext();

        var doDia = await Servico(db).ProducaoDoDiaAsync(1);

        var producao = Assert.Single(doDia.Candidatas);
        Assert.Equal((new DateOnly(2030, 3, 11), 11, "202511", (DateOnly?)null), (doDia.Dia, producao.JobOnId, producao.Producao, producao.FimEm));
        Assert.False(doDia.VariasProducoes);
        // A CM em uso (não a trocada), com a referência e o lote; o MF também.
        Assert.Equal(new RiFerramenta(101, "6012A118", "4"), producao.Cm);
        Assert.Equal(new RiFerramenta(201, "6012A118", "4"), producao.Mf);
        Assert.Equal(11, doDia.Escolhida(null)!.JobOnId);
    }

    [Fact]
    public async Task AntesDaViragem_AProducaoDeOntem()
    {
        using var db = CreateContext();

        Assert.Equal(10, Assert.Single((await Servico(db, AntesDaViragem).ProducaoDoDiaAsync(1)).Candidatas).JobOnId);
        // Com a viragem às 7h, às 07:30 já é a de hoje.
        Assert.Equal(11, Assert.Single((await Servico(db, AntesDaViragem, "7").ProducaoDoDiaAsync(1)).Candidatas).JobOnId);
    }

    [Fact]
    public async Task DuasProducoes_AsDuasCandidatas_ADeMaiorJobOnIdEscolhida()
    {
        using var db = CreateContext();

        var doDia = await Servico(db).ProducaoDoDiaAsync(2);

        Assert.True(doDia.VariasProducoes);
        Assert.Equal(new[] { 14, 13 }, doDia.Candidatas.Select(c => c.JobOnId));
        Assert.Equal(14, doDia.Escolhida(null)!.JobOnId);
        Assert.Equal(13, doDia.Escolhida(13)!.JobOnId);
        // Uma produção que não é candidata não se escolhe: fica a de maior jobon_id.
        Assert.Equal(14, doDia.Escolhida(11)!.JobOnId);
        // Sem MF nesta produção (só defesa: o Job On tem sempre CM, BQ e MF).
        Assert.Null(doDia.Candidatas[0].Mf);
    }

    [Fact]
    public async Task SemProducaoDatada_SemAFerramentaEscrita_NadaSeGrava()
    {
        using var db = CreateContext();

        var doDia = await Servico(db).ProducaoDoDiaAsync(3);
        var saida = await Servico(db).RegistarAsync(Pedido("12", maquina: 3));

        Assert.True(doDia.SemProducao);
        Assert.Null(doDia.Escolhida(null));
        // Sem Job On grava-se só com a referência e o lote escritos (Diogo, 21/10/2026).
        Assert.Equal(ReparacaoResultado.SemReferencia, saida.Resultado);
        Assert.Empty(await db.ReparacoesInternas.ToListAsync());
    }

    [Fact]
    public async Task Registar_GravaALinhaCurta_ComOCmIdDoMomento()
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(Pedido("  0457 ", jobOn: 11, contexto: 101));

        Assert.Equal((ReparacaoResultado.Gravado, "0457"), (saida.Resultado, saida.Numero));
        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal((11, 1, ReparacaoTipo.Cm, (int?)101, (int?)null, "0457"), (r.JobOnId, r.MachineId, r.Tipo, r.CmContextId, r.MfContextId, r.Numero));
        Assert.Equal((Rui, Manha.UtcDateTime, new DateOnly(2030, 3, 11)), (r.RegistadoPorUserId!.Value, r.RegistadoEmUtc, r.DiaOperacional));
        Assert.Equal(r.Id, saida.ReparacaoId);
        var operacao = await db.OperacoesRegistadas.AsNoTracking().SingleAsync();
        Assert.Equal((ReparacoesInternas.OperacaoTipo, ReparacoesInternas.ResultadoTipo, (int?)r.Id), (operacao.Tipo, operacao.ResultadoTipo, operacao.ResultadoId));
    }

    [Fact]
    public async Task Registar_UmMf_GuardaSoOMfId()
    {
        using var db = CreateContext();

        await Servico(db).RegistarAsync(Pedido("33", ReparacaoTipo.Mf));

        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal((ReparacaoTipo.Mf, (int?)null, (int?)201), (r.Tipo, r.CmContextId, r.MfContextId));
    }

    [Fact]
    public async Task OMesmoEnvioDuasVezes_GravaUmaVez_OMesmoNumeroOutraVezGravaOutra()
    {
        using var db = CreateContext();
        var pedido = Pedido("88");

        var primeira = await Servico(db).RegistarAsync(pedido);
        var repetida = await Servico(db).RegistarAsync(pedido);
        var outraReparacao = await Servico(db).RegistarAsync(Pedido("88"));

        Assert.Equal(ReparacaoResultado.Gravado, primeira.Resultado);
        Assert.Equal((ReparacaoResultado.JaRegistado, primeira.ReparacaoId, "88"), (repetida.Resultado, repetida.ReparacaoId, repetida.Numero));
        Assert.True(repetida.Gravou);
        Assert.Equal(ReparacaoResultado.Gravado, outraReparacao.Resultado);
        Assert.Equal(new[] { "88", "88" }, await db.ReparacoesInternas.Select(r => r.Numero).ToListAsync());
        Assert.Equal(2, await db.OperacoesRegistadas.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SemNumero_Recusa(string? numero)
    {
        using var db = CreateContext();

        Assert.Equal(ReparacaoResultado.SemNumero, (await Servico(db).RegistarAsync(Pedido(numero))).Resultado);
        Assert.Empty(await db.ReparacoesInternas.ToListAsync());
    }

    [Fact]
    public async Task ONumero_TemAte40Caracteres_SemContarOsEspacosDasPontas()
    {
        using var db = CreateContext();
        var trinta9 = new string('7', 39);
        var quarenta = new string('7', 40);

        var abaixo = await Servico(db).RegistarAsync(Pedido(trinta9));
        var cabe = await Servico(db).RegistarAsync(Pedido("  " + quarenta + "  "));
        var longo = await Servico(db).RegistarAsync(Pedido(quarenta + "1"));

        Assert.Equal(ReparacaoResultado.Gravado, abaixo.Resultado);
        Assert.Equal(ReparacaoResultado.Gravado, cabe.Resultado);
        Assert.Equal((ReparacaoResultado.NumeroLongo, quarenta + "1"), (longo.Resultado, longo.Numero));
        Assert.Equal([trinta9, quarenta], (await db.ReparacoesInternas.OrderBy(r => r.Id).ToListAsync()).Select(r => r.Numero));
    }

    [Fact]
    public async Task OQueFaltaOuMudou_Recusa_ComONumeroEscrito()
    {
        using var db = CreateContext();

        Assert.Equal(ReparacaoResultado.SemTipo, (await Servico(db).RegistarAsync(Pedido("1", tipo: null))).Resultado);
        Assert.Equal(ReparacaoResultado.MaquinaNaoExiste, (await Servico(db).RegistarAsync(Pedido("1", maquina: 99))).Resultado);
        Assert.Equal(ReparacaoResultado.MaquinaNaoExiste, (await Servico(db).RegistarAsync(Pedido("1", maquina: null))).Resultado);
        // A página mostrou a 202510 (ontem); agora é a 202511.
        var producaoMudou = await Servico(db).RegistarAsync(Pedido("1", jobOn: 10));
        Assert.Equal((ReparacaoResultado.ProducaoMudou, "1"), (producaoMudou.Resultado, producaoMudou.Numero));
        // A página mostrou a CM trocada (103); a de agora é a 101.
        Assert.Equal(ReparacaoResultado.FerramentaMudou, (await Servico(db).RegistarAsync(Pedido("1", jobOn: 11, contexto: 103))).Resultado);
        // B2: a 202514 não tem MF.
        Assert.Equal(ReparacaoResultado.SemFerramenta, (await Servico(db).RegistarAsync(Pedido("1", ReparacaoTipo.Mf, maquina: 2))).Resultado);
        Assert.Empty(await db.ReparacoesInternas.ToListAsync());
        Assert.Empty(await db.OperacoesRegistadas.ToListAsync());
    }

    [Fact]
    public async Task DuasProducoes_GravaNaEscolhida()
    {
        using var db = CreateContext();

        await Servico(db).RegistarAsync(Pedido("5", maquina: 2, jobOn: 13));
        await Servico(db).RegistarAsync(Pedido("6", maquina: 2));

        Assert.Equal(new[] { (13, 104), (14, 105) },
            (await db.ReparacoesInternas.OrderBy(r => r.Id).ToListAsync()).Select(r => (r.JobOnId!.Value, r.CmContextId!.Value)));
    }

    [Fact]
    public async Task Registar_NaoMexeEmMaisNada()
    {
        using var db = CreateContext();
        var antes = await FotografiaAsync(db);

        await Servico(db).RegistarAsync(Pedido("1"));
        await Servico(db).RegistarAsync(Pedido("2", ReparacaoTipo.Mf));

        Assert.Equal(antes, await FotografiaAsync(db));
        Assert.Equal(2, await db.ReparacoesInternas.CountAsync());
    }

    [Fact]
    public async Task ALista_DestaMaquinaEProducao_DaMaisRecente_EmPaginasDe25()
    {
        using var db = CreateContext();
        for (var i = 1; i <= 27; i++)
        {
            await Servico(db, Manha.AddMinutes(i)).RegistarAsync(Pedido(i.ToString(System.Globalization.CultureInfo.InvariantCulture), i % 2 == 0 ? ReparacaoTipo.Mf : ReparacaoTipo.Cm));
        }

        await Servico(db).RegistarAsync(Pedido("noutra", maquina: 2));

        var primeira = await Servico(db).ListarAsync(1, 11, null);
        var segunda = await Servico(db).ListarAsync(1, 11, 2);

        Assert.Equal((27, 25, 2), (primeira.TotalCount, primeira.Items.Count, primeira.TotalPages));
        Assert.Equal(new RiLinha(primeira.Items[0].Id, "27", ReparacaoTipo.Cm, "Rui Costa", Manha.UtcDateTime.AddMinutes(27), Hora: new TimeOnly(10, 27)), primeira.Items[0]);
        Assert.Equal(ReparacaoTipo.Mf, primeira.Items[1].Tipo);
        Assert.Equal(new[] { "2", "1" }, segunda.Items.Select(l => l.Numero));
        Assert.Empty((await Servico(db).ListarAsync(2, 11, null)).Items);
    }

    [Fact]
    public async Task UmUtilizadorEliminado_FicaSemQuem()
    {
        using var db = CreateContext();
        await Servico(db).RegistarAsync(Pedido("9"));
        var reparacao = await db.ReparacoesInternas.SingleAsync();
        reparacao.RegistadoPorUserId = null;
        await db.SaveChangesAsync();

        Assert.Null(Assert.Single((await Servico(db).ListarAsync(1, 11, null)).Items).Quem);
    }

    [Fact]
    public async Task EliminarOJobOn_ContaELevaAsReparacoes()
    {
        using var db = CreateContext();
        await Servico(db).RegistarAsync(Pedido("1"));
        await Servico(db).RegistarAsync(Pedido("2", maquina: 2));

        var preview = await new DeleteJobOnHandler(db).PreviewAsync(11, new DateOnly(2030, 3, 11));
        Assert.Equal(1, preview!.ReparacoesInternas);
        Assert.Equal(DeleteJobOnResult.Success, await new DeleteJobOnHandler(db).HandleAsync(new DeleteJobOn(11)));

        Assert.Equal(14, Assert.Single(await db.ReparacoesInternas.ToListAsync()).JobOnId);
    }

    [Theory]
    [InlineData("6", SaveModuleSettingResult.Success)]
    [InlineData("23", SaveModuleSettingResult.Success)]
    [InlineData("24", SaveModuleSettingResult.BoquilhasInvalidHour)]
    [InlineData("oito", SaveModuleSettingResult.BoquilhasInvalidHour)]
    public async Task AHoraDeViragemDaReparacaoInterna_GravaSeAParte(string valor, SaveModuleSettingResult esperado)
    {
        using var db = CreateContext();

        var outcome = await new SaveModuleSettingHandler(db).HandleAsync(new SaveModuleSetting(ModuleSettingKeys.ReparacaoInternaDayChangeHour, valor));

        Assert.Equal(esperado, outcome.Result);
        Assert.Equal(esperado == SaveModuleSettingResult.Success ? valor : null,
            (await db.ModuleSettings.AsNoTracking().FirstOrDefaultAsync(s => s.SettingKey == ModuleSettingKeys.ReparacaoInternaDayChangeHour))?.SettingValue);
        Assert.False(await db.ModuleSettings.AnyAsync(s => s.SettingKey == ModuleSettingKeys.BoquilhasDayChangeHour));
    }

    private static async Task<string> FotografiaAsync(DmoDbContext db)
    {
        var tools = await db.Tools.AsNoTracking().OrderBy(t => t.ToolId).Select(t => t.ToolId + t.Reference + t.Lot).ToListAsync();
        var jobOns = await db.JobOns.AsNoTracking().OrderBy(j => j.Id).Select(j => $"{j.Id}{j.MachineId}{j.InicioEm}{j.FimEm}").ToListAsync();
        var cms = await db.CmContexts.AsNoTracking().OrderBy(c => c.Id).Select(c => $"{c.Id}{c.JobOnId}{c.ReplacedJobOnId}{c.ToolId}").ToListAsync();
        var mfs = await db.MfContexts.AsNoTracking().OrderBy(c => c.Id).Select(c => $"{c.Id}{c.JobOnId}{c.ToolId}").ToListAsync();
        var outros = (await db.BqMovements.CountAsync(), await db.ArmazemMovimentos.CountAsync(), await db.FerramentaEstados.CountAsync(), await db.JobOnChanges.CountAsync());
        return string.Join("|", tools.Concat(jobOns).Concat(cms).Concat(mfs)) + outros;
    }

    private sealed class Horas(string? hora) : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(settingKey == ModuleSettingKeys.ReparacaoInternaDayChangeHour ? hora : null);
    }

    private sealed class SoBoquilhas : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(settingKey == ModuleSettingKeys.BoquilhasDayChangeHour ? "6" : null);
    }

    private sealed class Relogio(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Rui, DisplayName = "Rui Costa", OperatorId = "2165" };
    }
}
