using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Hora da reparação na base (migração ReparacaoInternaHora): o rasto fica
/// fixo também nas horas antes e depois.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ReparacaoInternaHora_ORastoNaoMudaAsHoras()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        var quem = Guid.NewGuid();
        try
        {
            await using (var db = CreateContext())
            {
                db.Users.Add(new User { Id = quem, Name = "Rui " + cenario.Marca, OperatorId = Random.Shared.Next(1000, 10000).ToString(System.Globalization.CultureInfo.InvariantCulture), ProviderUserId = "h-" + cenario.Marca });
                await db.SaveChangesAsync();
            }

            int id;
            await using (var db = CreateContext())
            {
                var servico = ReparacoesDe(db, quem);
                id = (await servico.RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), cenario.MachineId, cenario.JobOnId, ReparacaoTipo.Cm, cenario.CmId, "1"))).ReparacaoId!.Value;
                Assert.Equal(CorrecaoResultado.Corrigido, (await servico.CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), id, "2", "trocado"))).Resultado);
            }

            await using (var db = CreateContext())
            {
                foreach (var coluna in new[] { "HoraAntes", "HoraDepois" })
                {
                    var erro = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                        $"UPDATE \"ReparacoesInternasRasto\" SET \"{coluna}\" = TIME '13:37' WHERE \"ReparacaoId\" = {{0}}", id));
                    Assert.Equal(PostgresErrorCodes.CheckViolation, erro.SqlState);
                }
            }
        }
        finally
        {
            await LimparReparacaoAsync(cenario);
            await using var limpar = CreateContext();
            await limpar.Users.Where(u => u.Id == quem).ExecuteDeleteAsync();
        }
    }
}
