using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A Reparação Interna na base (migração ReparacaoInterna): o mesmo envio ao
/// mesmo tempo grava uma reparação; a base recusa uma CM com o mf_id, uma
/// reparação sem contexto e um número com espaços nas pontas; e eliminar o
/// Job On leva as reparações dele, com as chaves reais.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ReparacaoInterna_OMesmoEnvioAoMesmoTempo_GravaUmaReparacao()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        try
        {
            var pedido = new PedidoDeReparacao(Guid.NewGuid(), cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Mf, cenario.MfId, " 17 ");
            var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var db = CreateContext();
                return (await Reparacoes(db).RegistarAsync(pedido)).Resultado;
            }));

            Assert.Single(resultados, r => r == ReparacaoResultado.Gravado);
            Assert.All(resultados.Where(r => r != ReparacaoResultado.Gravado), r => Assert.Equal(ReparacaoResultado.JaRegistado, r));
            await using var leitura = CreateContext();
            var gravada = await leitura.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.JobOnId == cenario.JobOnId);
            Assert.Equal(("17", ReparacaoTipo.Mf, (int?)null, (int?)cenario.MfId), (gravada.Numero, gravada.Tipo, gravada.CmContextId, gravada.MfContextId));
            Assert.Equal(gravada.Id, (await leitura.OperacoesRegistadas.AsNoTracking().SingleAsync(o => o.Id == pedido.OperacaoId)).ResultadoId);
            var lista = await Reparacoes(leitura).ListarAsync(cenario.MachineId, cenario.JobOnId, null);
            Assert.Equal("17", Assert.Single(lista.Items).Numero);
        }
        finally
        {
            await LimparReparacaoAsync(cenario);
        }
    }

    [SkippableFact]
    public async Task ReparacaoInterna_ABaseRecusaContextoErradoENumeroComEspacos()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        try
        {
            foreach (var errada in new[]
            {
                Linha(cenario, ReparacaoTipo.Cm, null, cenario.MfId, "1"),
                Linha(cenario, ReparacaoTipo.Mf, cenario.CmId, cenario.MfId, "2"),
                Linha(cenario, ReparacaoTipo.Cm, null, null, "3"),
                Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, " 4"),
                Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, " ")
            })
            {
                await using var db = CreateContext();
                db.ReparacoesInternas.Add(errada);
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            }

            await using var certa = CreateContext();
            certa.ReparacoesInternas.Add(Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, "5"));
            await certa.SaveChangesAsync();
            Assert.Equal(1, await certa.ReparacoesInternas.CountAsync(r => r.JobOnId == cenario.JobOnId));
        }
        finally
        {
            await LimparReparacaoAsync(cenario);
        }
    }

    [SkippableFact]
    public async Task ReparacaoInterna_EliminarOJobOn_LevaAsReparacoes()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        var envios = new[] { Guid.NewGuid(), Guid.NewGuid() };
        try
        {
            await using (var db = CreateContext())
            {
                Assert.True((await Reparacoes(db).RegistarAsync(new PedidoDeReparacao(envios[0], cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Cm, cenario.CmId, "8"))).Gravou);
                Assert.True((await Reparacoes(db).RegistarAsync(new PedidoDeReparacao(envios[1], cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Mf, cenario.MfId, "8"))).Gravou);
            }

            await using (var outra = CreateContext())
            {
                var preview = await new DeleteJobOnHandler(outra).PreviewAsync(cenario.JobOnId, DateOnly.FromDateTime(DateTime.UtcNow));
                Assert.Equal(2, preview!.ReparacoesInternas);
                Assert.Equal(DeleteJobOnResult.Success, await new DeleteJobOnHandler(outra).HandleAsync(new DeleteJobOn(cenario.JobOnId, "Diogo")));
            }

            await using var leitura = CreateContext();
            Assert.False(await leitura.JobOns.AnyAsync(j => j.Id == cenario.JobOnId));
            Assert.False(await leitura.ReparacoesInternas.AnyAsync(r => r.JobOnId == cenario.JobOnId));
        }
        finally
        {
            await using (var limpar = CreateContext())
            {
                await limpar.OperacoesRegistadas.Where(o => envios.Contains(o.Id)).ExecuteDeleteAsync();
            }

            await LimparReparacaoAsync(cenario);
        }
    }

    private sealed record ReparacaoCenario(int MachineId, int JobOnId, int CmId, int MfId, string Marca);

    private ReparacoesInternas Reparacoes(DMO.Alpha.Infrastructure.Data.DmoDbContext db) =>
        new(db, new SemHoraDeViragem(), new DMO.Alpha.Infrastructure.Data.OperacoesRegistadas(db), new SemSessao());

    private static Reparacao Linha(ReparacaoCenario cenario, ReparacaoTipo tipo, int? cm, int? mf, string numero) => new()
    {
        JobOnId = cenario.JobOnId, MachineId = cenario.MachineId, Tipo = tipo, CmContextId = cm, MfContextId = mf,
        Numero = numero, RegistadoEmUtc = DateTime.UtcNow, DiaOperacional = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    // Uma máquina só deste teste, com uma produção que cobre o dia de hoje
    // e o de ontem (o dia operacional antes das 8h), com a CM e o MF.
    private async Task<ReparacaoCenario> ReparacaoCenarioAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marca = "RI" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var maquina = new Machine { Code = marca };
        db.Machines.Add(maquina);
        db.Tools.AddRange(
            new Tool { ToolId = marca + "-cm", Type = ToolType.Cm, Reference = marca, Lot = "1" },
            new Tool { ToolId = marca + "-mf", Type = ToolType.Mf, Reference = marca, Lot = "1" });
        var jobOn = new JobOn { Machine = maquina, Reference = marca, ProductionNumber = "1", InicioEm = hoje.AddDays(-2), FimEm = hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        var cm = new CmContext { JobOnId = jobOn.Id, ToolId = marca + "-cm" };
        var mf = new MfContext { JobOnId = jobOn.Id, ToolId = marca + "-mf" };
        db.CmContexts.Add(cm);
        db.MfContexts.Add(mf);
        await db.SaveChangesAsync();
        return new ReparacaoCenario(maquina.Id, jobOn.Id, cm.Id, mf.Id, marca);
    }

    private async Task LimparReparacaoAsync(ReparacaoCenario cenario)
    {
        await using var db = CreateContext();
        var ids = db.ReparacoesInternas.Where(r => r.MachineId == cenario.MachineId).Select(r => (int?)r.Id);
        await db.OperacoesRegistadas.Where(o => o.ResultadoTipo == ReparacoesInternas.ResultadoTipo && ids.Contains(o.ResultadoId)).ExecuteDeleteAsync();
        await db.ReparacoesInternas.Where(r => r.MachineId == cenario.MachineId).ExecuteDeleteAsync();
        await db.CmContexts.Where(c => c.JobOnId == cenario.JobOnId).ExecuteDeleteAsync();
        await db.MfContexts.Where(c => c.JobOnId == cenario.JobOnId).ExecuteDeleteAsync();
        await db.JobOns.Where(j => j.MachineId == cenario.MachineId).ExecuteDeleteAsync();
        await db.Tools.Where(t => t.Reference == cenario.Marca).ExecuteDeleteAsync();
        await db.Machines.Where(m => m.Id == cenario.MachineId).ExecuteDeleteAsync();
    }

    private sealed class SemHoraDeViragem : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
