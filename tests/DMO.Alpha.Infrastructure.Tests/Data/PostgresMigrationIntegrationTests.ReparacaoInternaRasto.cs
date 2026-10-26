using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Corrigir e anular na base (migração ReparacaoInternaRasto): o rasto só se
/// acrescenta (a base recusa mudar uma linha, menos o SET NULL de quem ao
/// eliminar o utilizador) e as regras de cada ação; anular ao mesmo tempo
/// anula uma vez; e eliminar o Job On continua a levar as reparações, agora
/// com o rasto delas.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ReparacaoInternaRasto_SoSeAcrescenta_EABaseRecusaUmaAcaoErrada()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        var quem = Guid.NewGuid();
        try
        {
            await using (var db = CreateContext())
            {
                db.Users.Add(new User { Id = quem, Name = "Ana " + cenario.Marca, OperatorId = Random.Shared.Next(1000, 10000).ToString(System.Globalization.CultureInfo.InvariantCulture), ProviderUserId = "p-" + cenario.Marca });
                await db.SaveChangesAsync();
            }

            int id;
            await using (var db = CreateContext())
            {
                var servico = ReparacoesDe(db, quem);
                id = (await servico.RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Cm, cenario.CmId, "1"))).ReparacaoId!.Value;
                Assert.Equal(CorrecaoResultado.Corrigido, (await servico.CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), id, "2", "trocado"))).Resultado);
            }

            // Mudar uma linha do rasto: a base recusa.
            await using (var db = CreateContext())
            {
                var erro = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                    "UPDATE \"ReparacoesInternasRasto\" SET \"Motivo\" = 'outro' WHERE \"ReparacaoId\" = {0}", id));
                Assert.Equal(PostgresErrorCodes.CheckViolation, erro.SqlState);
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                    "UPDATE \"ReparacoesInternasRasto\" SET \"PorUserId\" = gen_random_uuid() WHERE \"ReparacaoId\" = {0}", id));
            }

            // Uma correção sem o número depois, uma anulação com ele, um motivo com espaços: a base recusa.
            foreach (var errada in new[]
            {
                Rasto(id, ReparacaoAcao.Corrigido, null, null),
                Rasto(id, ReparacaoAcao.Anulado, "3", null),
                Rasto(id, ReparacaoAcao.Anulado, null, " motivo"),
                Rasto(id, ReparacaoAcao.Anulado, null, " ")
            })
            {
                await using var db = CreateContext();
                db.ReparacoesInternasRasto.Add(errada);
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            }

            // Eliminar o utilizador deixa o rasto sem autor (o SET NULL passa).
            await using (var db = CreateContext())
            {
                await db.Users.Where(u => u.Id == quem).ExecuteDeleteAsync();
            }

            await using var leitura = CreateContext();
            var rasto = await leitura.ReparacoesInternasRasto.AsNoTracking().SingleAsync(r => r.ReparacaoId == id);
            Assert.Equal((ReparacaoAcao.Corrigido, "1", "2", "trocado", (Guid?)null), (rasto.Acao, rasto.NumeroAntes, rasto.NumeroDepois, rasto.Motivo, rasto.PorUserId));
        }
        finally
        {
            await LimparReparacaoAsync(cenario);
            await using var limpar = CreateContext();
            await limpar.Users.Where(u => u.Id == quem).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task ReparacaoInternaRasto_AnularAoMesmoTempo_AnulaUmaVez()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        try
        {
            int id;
            await using (var db = CreateContext())
            {
                id = (await Reparacoes(db).RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Mf, cenario.MfId, "9"))).ReparacaoId!.Value;
            }

            var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var db = CreateContext();
                return (await Reparacoes(db).AnularAsync(new PedidoDeAnulacao(Guid.NewGuid(), id, Chefia: true))).Resultado;
            }));

            Assert.Single(resultados, r => r == CorrecaoResultado.Anulado);
            Assert.All(resultados.Where(r => r != CorrecaoResultado.Anulado), r => Assert.Equal(CorrecaoResultado.JaAnulada, r));
            await using var leitura = CreateContext();
            Assert.Equal(1, await leitura.ReparacoesInternasRasto.CountAsync(r => r.ReparacaoId == id));
            Assert.NotNull((await leitura.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.Id == id)).AnuladaEmUtc);
            Assert.Empty((await Reparacoes(leitura).ListarAsync(cenario.MachineId, cenario.JobOnId, null)).Items);
        }
        finally
        {
            await LimparReparacaoAsync(cenario);
        }
    }

    [SkippableFact]
    public async Task ReparacaoInternaRasto_EliminarOJobOn_LevaAsReparacoesEORasto()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        var envios = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        try
        {
            await using (var db = CreateContext())
            {
                var servico = Reparacoes(db);
                var corrigida = (await servico.RegistarAsync(new PedidoDeReparacao(envios[0], cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Cm, cenario.CmId, "1"))).ReparacaoId!.Value;
                var anulada = (await servico.RegistarAsync(new PedidoDeReparacao(envios[1], cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Mf, cenario.MfId, "2"))).ReparacaoId!.Value;
                Assert.True((await servico.CorrigirAsync(new PedidoDeCorrecao(envios[2], corrigida, "10", Chefia: true))).Gravou);
                Assert.True((await servico.AnularAsync(new PedidoDeAnulacao(envios[3], anulada, "errada", Chefia: true))).Gravou);
            }

            await using (var outra = CreateContext())
            {
                // As anuladas também se perdem: o aviso conta as duas.
                Assert.Equal(2, (await new DeleteJobOnHandler(outra).PreviewAsync(cenario.JobOnId, DateOnly.FromDateTime(DateTime.UtcNow)))!.ReparacoesInternas);
                Assert.Equal(DeleteJobOnResult.Success, await new DeleteJobOnHandler(outra).HandleAsync(new DeleteJobOn(cenario.JobOnId, "Diogo")));
            }

            await using var leitura = CreateContext();
            Assert.False(await leitura.JobOns.AnyAsync(j => j.Id == cenario.JobOnId));
            Assert.False(await leitura.ReparacoesInternas.AnyAsync(r => r.JobOnId == cenario.JobOnId));
            Assert.False(await leitura.ReparacoesInternasRasto.AnyAsync(r => !leitura.ReparacoesInternas.Any(p => p.Id == r.ReparacaoId)));
        }
        finally
        {
            // As operações ficam sem a reparação: saem pelos envios deste teste.
            await using (var limpar = CreateContext())
            {
                await limpar.OperacoesRegistadas.Where(o => envios.Contains(o.Id)).ExecuteDeleteAsync();
            }

            await LimparReparacaoAsync(cenario);
        }
    }

    private static ReparacoesInternas ReparacoesDe(DMO.Alpha.Infrastructure.Data.DmoDbContext db, Guid quem) =>
        new(db, new SemHoraDeViragem(), new DMO.Alpha.Infrastructure.Data.OperacoesRegistadas(db), new ComSessao(quem));

    private static ReparacaoRasto Rasto(int reparacaoId, ReparacaoAcao acao, string? depois, string? motivo) => new()
    {
        ReparacaoId = reparacaoId, Acao = acao, NumeroAntes = "1", NumeroDepois = depois, Motivo = motivo, EmUtc = DateTime.UtcNow
    };

    private sealed class ComSessao(Guid quem) : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = quem, DisplayName = "Ana" };
    }
}
