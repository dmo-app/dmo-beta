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
/// A hora da reparação (Diogo, 23/10/2026): vem com a hora local de agora,
/// a pessoa pode mudá-la e fica gravada com a reparação; o RegistadoEmUtc
/// continua a ser o carimbo de quando se registou. Corrigir muda a hora,
/// com rasto (antes e depois). As reparações de antes ficam sem hora.
/// </summary>
public sealed class ReparacoesInternasHoraTests
{
    // 14:37 em Lisboa no verão (UTC+1): 13:37 UTC.
    private static readonly DateTimeOffset Tarde = new(2030, 7, 11, 13, 37, 25, TimeSpan.Zero);

    private static readonly Guid Rui = Guid.Parse("8f0c4c2e-1d1b-4b8e-9a52-0f6d6f1a0b11");

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-ri-hora-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.Add(new Machine { Id = 1, Code = "B1" });
        db.Users.Add(new User { Id = Rui, Name = "Rui Costa", OperatorId = "2165", ProviderUserId = "p-2165" });
        db.Tools.Add(new Tool { ToolId = "cm-a", Type = ToolType.Cm, Reference = "5538T324", Lot = "2" });
        db.JobOns.Add(new JobOn { Id = 11, MachineId = 1, Reference = "5538T324", ProductionNumber = "202511", InicioEm = new DateOnly(2030, 7, 1) });
        db.CmContexts.Add(new CmContext { Id = 101, JobOnId = 11, ToolId = "cm-a" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ReparacoesInternas Servico(DmoDbContext db) =>
        new(db, new SemHora(), new OperacoesRegistadas(db), new Sessao(Rui), new Relogio(Tarde));

    private static PedidoDeReparacao Pedido(string numero, TimeOnly? hora = null) =>
        new(Guid.NewGuid(), 1, null, ReparacaoTipo.Cm, null, numero, Hora: hora);

    [Fact]
    public async Task Registar_SemHora_GravaAHoraLocalDeAgora_AoMinuto_EORegistadoEmFicaIgual()
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(Pedido("12"));

        Assert.Equal(ReparacaoResultado.Gravado, saida.Resultado);
        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal(new TimeOnly(14, 37), r.Hora);
        Assert.Equal(Tarde.UtcDateTime, r.RegistadoEmUtc);
    }

    [Fact]
    public async Task Registar_ComAHoraMudada_GravaAEscrita()
    {
        using var db = CreateContext();

        await Servico(db).RegistarAsync(Pedido("12", new TimeOnly(9, 5)));

        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal(new TimeOnly(9, 5), r.Hora);
        Assert.Equal(Tarde.UtcDateTime, r.RegistadoEmUtc);
        var linha = Assert.Single((await Servico(db).ListarAsync(1, 11, null)).Items);
        Assert.Equal(new TimeOnly(9, 5), linha.Hora);
    }

    [Fact]
    public async Task Corrigir_SoAHora_MudaAHora_ComRasto_EONumeroFica()
    {
        using var db = CreateContext();
        var id = (await Servico(db).RegistarAsync(Pedido("12", new TimeOnly(9, 5)))).ReparacaoId!.Value;

        var saida = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), id, "12", Hora: new TimeOnly(10, 30)));

        Assert.Equal(CorrecaoResultado.Corrigido, saida.Resultado);
        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal(("12", (TimeOnly?)new TimeOnly(10, 30)), (r.Numero, r.Hora));
        var rasto = await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync();
        Assert.Equal(("12", "12", (TimeOnly?)new TimeOnly(9, 5), (TimeOnly?)new TimeOnly(10, 30)),
            (rasto.NumeroAntes, rasto.NumeroDepois, rasto.HoraAntes, rasto.HoraDepois));
    }

    [Fact]
    public async Task Corrigir_SemHora_DeixaAHora_ENumeroEHoraIguais_NadaMuda()
    {
        using var db = CreateContext();
        var id = (await Servico(db).RegistarAsync(Pedido("12", new TimeOnly(9, 5)))).ReparacaoId!.Value;

        var numero = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), id, "13"));
        var igual = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), id, "13", Hora: new TimeOnly(9, 5)));

        Assert.Equal((CorrecaoResultado.Corrigido, CorrecaoResultado.Igual), (numero.Resultado, igual.Resultado));
        Assert.Equal(new TimeOnly(9, 5), (await db.ReparacoesInternas.AsNoTracking().SingleAsync()).Hora);
        var rasto = await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync();
        Assert.Equal(((TimeOnly?)null, (TimeOnly?)null), (rasto.HoraAntes, rasto.HoraDepois));
    }

    [Fact]
    public async Task UmaDeAntes_SemHora_ListaComHoraNula_ECorrigirDaLheHora()
    {
        using var db = CreateContext();
        db.ReparacoesInternas.Add(new Reparacao
        {
            Id = 50, JobOnId = 11, MachineId = 1, Tipo = ReparacaoTipo.Cm, CmContextId = 101, Numero = "7",
            RegistadoPorUserId = Rui, RegistadoEmUtc = Tarde.UtcDateTime.AddDays(-1), DiaOperacional = new DateOnly(2030, 7, 10)
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Null(Assert.Single((await Servico(db).ListarAsync(1, 11, null)).Items).Hora);
        await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), 50, "7", Hora: new TimeOnly(8, 0)));

        Assert.Equal(new TimeOnly(8, 0), (await db.ReparacoesInternas.AsNoTracking().SingleAsync()).Hora);
        var rasto = await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync();
        Assert.Equal(((TimeOnly?)null, (TimeOnly?)new TimeOnly(8, 0)), (rasto.HoraAntes, rasto.HoraDepois));
    }

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
