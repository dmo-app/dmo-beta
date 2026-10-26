using System.Data.Common;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // PERF-02: o painel da Reparação Interna lia a produção do dia uma linha
    // (máquina) de cada vez, uma consulta por linha. Agora é uma só leitura
    // para todas, com a mesma regra e o mesmo resultado da leitura de uma
    // máquina (que fica para o registo). Antes: 1 consulta por linha (6 linhas
    // = 6, 30 linhas = 30); depois: 1 para qualquer número de linhas.
    [SkippableFact]
    public async Task ReparacaoInterna_ProducoesDoDia_NumaSoLeituraComOMesmoResultadoQueUmaPorUma()
    {
        SkipWithoutConnection();
        var marker = "P2-" + Guid.NewGuid().ToString("N")[..8];
        var dia = new DateOnly(2030, 3, 11);
        var (cm, mf) = (marker + "-CM", marker + "-MF");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.AddRange(
            new Tool { ToolId = cm, Type = ToolType.Cm, Reference = "5538T324", Lot = "2" },
            new Tool { ToolId = mf, Type = ToolType.Mf, Reference = "5538T324", Lot = "2" });
        var maquinas = Enumerable.Range(0, 30).Select(i => new Machine { Code = $"{marker}-{i:00}" }).ToList();
        db.Machines.AddRange(maquinas);
        await db.SaveChangesAsync();

        // Por máquina (i % 5): 0 sem produções, 1 uma, 2 várias (com CM e MF),
        // 3 só fora do dia ou sem datas, 4 uma sem fim e uma que acabou ontem.
        var jobOns = new List<JobOn>();
        for (var i = 0; i < maquinas.Count; i++)
        {
            var m = maquinas[i].Id;
            JobOn Novo(string n, DateOnly? inicio, DateOnly? fim) =>
                new() { MachineId = m, Reference = "5538T324", ProductionNumber = $"{marker}-{i:00}-{n}", InicioEm = inicio, FimEm = fim };
            switch (i % 5)
            {
                case 1:
                    jobOns.Add(Novo("a", dia, dia));
                    break;
                case 2:
                    jobOns.AddRange([Novo("a", dia.AddDays(-2), dia.AddDays(2)), Novo("b", dia, null), Novo("c", dia.AddDays(-9), dia)]);
                    break;
                case 3:
                    jobOns.AddRange([Novo("a", null, null), Novo("b", dia.AddDays(-9), dia.AddDays(-1)), Novo("c", dia.AddDays(1), null)]);
                    break;
                case 4:
                    jobOns.AddRange([Novo("a", dia.AddDays(-5), null), Novo("b", dia.AddDays(-5), dia.AddDays(-1))]);
                    break;
            }
        }

        db.JobOns.AddRange(jobOns);
        await db.SaveChangesAsync();
        db.CmContexts.AddRange(jobOns.Where(j => j.ProductionNumber.EndsWith("-b", StringComparison.Ordinal))
            .Select(j => new CmContext { JobOnId = j.Id, ToolId = cm }));
        db.MfContexts.AddRange(jobOns.Where(j => j.ProductionNumber.EndsWith("-a", StringComparison.Ordinal))
            .Select(j => new MfContext { JobOnId = j.Id, ToolId = mf }));
        await db.SaveChangesAsync();
        try
        {
            var linhas = maquinas.Select(m => m.Id).ToList();
            var porUma = new Dictionary<int, RiProducaoDoDia>();
            var contaUma = new ContaConsultas();
            await using (var ctx = CreateContextWith(contaUma))
            {
                var servico = Servico(ctx);
                foreach (var id in linhas)
                {
                    porUma[id] = await servico.ProducaoDoDiaAsync(id, dia);
                }
            }

            Assert.Equal(linhas.Count, contaUma.Vezes);

            foreach (var quantas in new[] { 0, 1, 6, 30 })
            {
                var pedidas = linhas.Take(quantas).ToList();
                var conta = new ContaConsultas();
                IReadOnlyDictionary<int, RiProducaoDoDia> juntas;
                await using (var ctx = CreateContextWith(conta))
                {
                    juntas = await Servico(ctx).ProducoesDoDiaAsync(pedidas, dia);
                }

                Assert.Equal(quantas == 0 ? 0 : 1, conta.Vezes);
                Assert.Equal(pedidas.OrderBy(x => x), juntas.Keys.OrderBy(x => x));
                foreach (var id in pedidas)
                {
                    Assert.Equal(porUma[id].Dia, juntas[id].Dia);
                    Assert.Equal(porUma[id].Candidatas, juntas[id].Candidatas);
                }
            }

            // Os casos que importam existem mesmo: nenhuma, uma e várias candidatas.
            Assert.Equal([0, 1, 3, 0, 1, 0], linhas.Take(6).Select(id => porUma[id].Candidatas.Count));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"CmContexts\" WHERE \"ToolId\" IN ({0}, {1})", cm, mf);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"MfContexts\" WHERE \"ToolId\" IN ({0}, {1})", cm, mf);
            foreach (var m in maquinas)
            {
                await DeleteJobOnAsync(db, m.Id);
            }

            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" IN ({cm}, {mf})");
        }
    }

    private static ReparacoesInternas Servico(DmoDbContext ctx) =>
        new(ctx, new NoRouteSettings(), new OperacoesRegistadas(ctx), new SessaoSemConta());

    private sealed class SessaoSemConta : DMO.Alpha.Core.Runtime.ICurrentAccountContext
    {
        public DMO.Alpha.Core.Runtime.ActorContext Current { get; } = new() { IsAuthenticated = false };
    }

    /// <summary>Conta as leituras (SELECT) enviadas à base.</summary>
    private sealed class ContaConsultas : DbCommandInterceptor
    {
        public int Vezes { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Vezes++;
            return ValueTask.FromResult(result);
        }
    }
}
