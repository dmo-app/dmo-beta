using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Os próximos Job On do Armazém no PostgreSQL (0.2.0-beta.13): o período, a
/// máquina e as ferramentas em uso traduzem-se na base.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Armazem_OsProximosJobOn_NoPostgreSql()
    {
        SkipWithoutConnection();
        var (cm, codigos) = await FerramentaEPosicoesAsync(1);
        await using var db = CreateContext();
        var marca = Guid.NewGuid().ToString("N")[..8];
        var maquina = new Machine { Code = $"PX-{marca}" };
        db.Machines.Add(maquina);
        await db.SaveChangesAsync();
        // Um dia longe, para não apanhar outras produções da base de testes.
        var inicio = new DateOnly(2091, 3, 10);
        var jobOn = new JobOn { Reference = $"PX{marca}", ProductionNumber = $"PX-{marca}", MachineId = maquina.Id, InicioEm = inicio };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = cm });
        await db.SaveChangesAsync();
        try
        {
            await Movimentos(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), cm, codigos[0], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null));

            var lista = await new ArmazemProximos(db).ListarAsync(inicio.AddDays(-3), 7);
            var depois = await new ArmazemProximos(db).ListarAsync(inicio.AddDays(1), 7);

            var producao = Assert.Single(lista, j => j.JobOnId == jobOn.Id);
            Assert.Equal(maquina.Code, producao.Maquina);
            var ferramenta = Assert.Single(producao.Ferramentas);
            Assert.Equal((cm, ArmazemProximoAcao.Tirar, $"Armazém · posição {codigos[0]}"), (ferramenta.ToolId, ferramenta.Acao, ferramenta.OndeEsta));
            Assert.DoesNotContain(depois, j => j.JobOnId == jobOn.Id);
        }
        finally
        {
            await db.CmContexts.Where(c => c.JobOnId == jobOn.Id).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.Id == jobOn.Id).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == maquina.Id).ExecuteDeleteAsync();
            await LimparAsync(cm, codigos);
        }
    }
}
