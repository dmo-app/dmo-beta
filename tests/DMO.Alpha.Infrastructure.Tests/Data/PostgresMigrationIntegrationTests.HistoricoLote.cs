using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // PERF-03: a procura do Histórico lia as produções de cada ferramenta
    // (até 40) uma a uma, com 4 leituras por ferramenta, e filtrava depois de
    // ler. Agora lê as relações, os Job On (já filtrados por linha e dias), o
    // SAP e as leituras de uso num número fixo de consultas. As produções são
    // as mesmas, pela mesma ordem, que as da ficha (uma a uma).
    [SkippableFact]
    public async Task HistoricoFerramentas_ProducoesEmLote_IguaisAUmaAUma_ComConsultasFixas()
    {
        SkipWithoutConnection();
        var marker = "P3" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        const int quantas = 41;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var maquinas = Enumerable.Range(0, 3).Select(i => new Machine { Code = $"{marker}M{i}" }).ToList();
        db.Machines.AddRange(maquinas);
        var tipos = new[] { ToolType.Cm, ToolType.Mf, ToolType.Bq };
        var ferramentas = Enumerable.Range(0, quantas).Select(i => new Tool
        {
            ToolId = $"{marker}-T{i:00}",
            Type = tipos[i % 3],
            Reference = (i < 5 ? marker + "A" : marker + "B") + i.ToString("00"),
            Lot = (i % 4).ToString()
        }).ToList();
        db.Tools.AddRange(ferramentas);
        await db.SaveChangesAsync();

        var inicio = new DateOnly(2030, 3, 1);
        var jobOns = new List<JobOn>();
        for (var i = 0; i < quantas; i++)
        {
            var m = maquinas[i % 3].Id;
            var r = $"{marker}R{i:00}";
            jobOns.Add(new JobOn { MachineId = m, Reference = r, ProductionNumber = $"{marker}-{i:00}-a", InicioEm = inicio.AddDays(i % 5), FimEm = inicio.AddDays(10) });
            jobOns.Add(new JobOn { MachineId = m, Reference = r, ProductionNumber = $"{marker}-{i:00}-b", InicioEm = inicio.AddDays(12), FimEm = null });
            jobOns.Add(new JobOn { MachineId = m, Reference = r, ProductionNumber = $"{marker}-{i:00}-c", InicioEm = null, FimEm = null });
        }

        db.JobOns.AddRange(jobOns);
        await db.SaveChangesAsync();
        JobOn Jo(int i, string n) => jobOns.Single(j => j.ProductionNumber == $"{marker}-{i:00}-{n}");
        for (var i = 0; i < quantas; i++)
        {
            var t = ferramentas[i];
            foreach (var (n, trocada) in new[] { ("a", false), ("b", false), ("c", false) })
            {
                var id = Jo(i, n).Id;
                switch (t.Type)
                {
                    case ToolType.Cm: db.CmContexts.Add(new CmContext { JobOnId = id, ToolId = t.ToolId }); break;
                    case ToolType.Mf: db.MfContexts.Add(new MfContext { JobOnId = id, ToolId = t.ToolId }); break;
                    default: db.BqContexts.Add(new BqContext { JobOnId = id, ToolId = t.ToolId }); break;
                }

                _ = trocada;
            }

            // Trocada a meio do Job On «a» da ferramenta seguinte (lote diferente).
            var vizinha = Jo((i + 1) % quantas, "a").Id;
            switch (t.Type)
            {
                case ToolType.Cm: db.CmContexts.Add(new CmContext { ReplacedJobOnId = vizinha, ReplacedAtUtc = DateTime.UtcNow, ToolId = t.ToolId }); break;
                case ToolType.Mf: db.MfContexts.Add(new MfContext { ReplacedJobOnId = vizinha, ReplacedAtUtc = DateTime.UtcNow, ToolId = t.ToolId }); break;
                default: db.BqContexts.Add(new BqContext { ReplacedJobOnId = vizinha, ReplacedAtUtc = DateTime.UtcNow, ToolId = t.ToolId }); break;
            }

            // SAP: uma linha dentro do Job On «a» e outra aberta para o «b»; i%7 sem peças líquidas, i%11 sem SAP.
            if (i % 11 != 0)
            {
                db.SapFabricos.Add(new SapFabrico
                {
                    Material = $"{marker}R{i:00}BRA", Linha = maquinas[i % 3].Code, InicioEm = inicio.AddDays(i % 5), FimEm = inicio.AddDays(10),
                    ProducaoBruta = 1000 + i, ProducaoLiquida = i % 7 == 0 ? null : 900 + i, LidoEmUtc = DateTime.UtcNow
                });
                db.SapFabricos.Add(new SapFabrico
                {
                    Material = $"{marker}R{i:00}BRA", Linha = maquinas[i % 3].Code, InicioEm = inicio.AddDays(12), FimEm = inicio.AddDays(14),
                    ProducaoBruta = 50 + i, ProducaoLiquida = 40 + i, LidoEmUtc = DateTime.UtcNow
                });
            }

            // Leituras de uso: antes do início, no dia do início, no fim e depois; algumas ferramentas sem leituras.
            if (i % 3 != 2)
            {
                foreach (var (dia, p) in new[] { (inicio.AddDays(-2), 5m), (inicio.AddDays(i % 5), 6m + i % 4), (inicio.AddDays(10), 9m + i % 4), (inicio.AddDays(13), 11m) })
                {
                    db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao
                    {
                        ToolId = t.ToolId, Percentagem = p,
                        RegistadaEmUtc = DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(11, 0)), DateTimeKind.Utc)
                    });
                }
            }
        }

        await db.SaveChangesAsync();
        var ids = ferramentas.Select(f => f.ToolId).ToList();
        try
        {
            var filtros = new HistoricoFiltro?[]
            {
                null,
                new(null, null, maquinas[1].Code.ToLowerInvariant(), null, null),
                new(null, null, null, inicio.AddDays(11), null),
                new(null, null, null, null, inicio.AddDays(1)),
                new(null, null, maquinas[2].Code, inicio.AddDays(3), inicio.AddDays(13))
            };
            foreach (var filtro in filtros)
            {
                foreach (var n in new[] { 5, 40 })
                {
                    var pedidas = ids.Take(n).ToList();
                    var antigas = new Dictionary<string, List<FerramentaProducao>>();
                    var contaAntes = new ContaConsultas();
                    await using (var ctx = CreateContextWith(contaAntes))
                    {
                        var fabrico = new FerramentasFabricoSap(ctx, new SessaoUsoSap());
                        foreach (var id in pedidas)
                        {
                            var itens = (await fabrico.ProducoesAsync(id, 1, 100_000)).Pagina.Items.Where(p => CabeNoFiltro(p, filtro)).ToList();
                            if (itens.Count > 0)
                            {
                                antigas[id] = itens;
                            }
                        }
                    }

                    var contaDepois = new ContaConsultas();
                    IReadOnlyDictionary<string, IReadOnlyList<FerramentaProducao>> novas;
                    await using (var ctx = CreateContextWith(contaDepois))
                    {
                        novas = await new FerramentasFabricoSap(ctx, new SessaoUsoSap()).ProducoesDeFerramentasAsync(pedidas, filtro);
                    }

                    Assert.Equal(antigas.Keys.OrderBy(x => x, StringComparer.Ordinal), novas.Keys.OrderBy(x => x, StringComparer.Ordinal));
                    foreach (var (id, itens) in antigas)
                    {
                        Assert.Equal(itens, novas[id]);
                    }

                    // Antes: 5 leituras por ferramenta (com os movimentos do Armazém, DMO-571); depois: um número fixo (máx. 6).
                    Assert.Equal(n * 5, contaAntes.Vezes);
                    Assert.True(contaDepois.Vezes <= 6, $"{contaDepois.Vezes} consultas");
                    if (filtro is null)
                    {
                        Assert.Equal(6, contaDepois.Vezes);
                    }
                }
            }

            // Os casos que importam existem: em curso, sem SAP, sem leituras, trocada.
            var todas = await new FerramentasFabricoSap(db, new SessaoUsoSap()).ProducoesDeFerramentasAsync(ids, null);
            var todasItens = todas.Values.SelectMany(x => x).ToList();
            Assert.Contains(todasItens, p => p.FimEm is null && p.InicioEm is not null && p.ProducaoBruta is not null);
            Assert.Contains(todasItens, p => p.ProducaoBruta is null && p.InicioEm is not null);
            Assert.Contains(todasItens, p => p.InicioEm is null);
            Assert.Contains(todasItens, p => p.UtilizacaoAntes is null && p.InicioEm is not null);
            Assert.Contains(todasItens, p => p.UtilizacaoAntes is not null && p.UtilizacaoDepois is not null);

            // A procura completa: a mesma conta de consultas com 5 ou com 40 ferramentas, e o corte avisado.
            var contagens = new Dictionary<int, int>();
            foreach (var (referencia, esperadas, cortada) in new[] { (marker + "A", 5, false), (marker, 40, true) })
            {
                var conta = new ContaConsultas();
                await using var ctx = CreateContextWith(conta);
                var procura = await new FerramentaHistoricoQuery(ctx, new FerramentasFabricoSap(ctx, new SessaoUsoSap()))
                    .ProcurarAsync(new HistoricoFiltro(referencia, null, null, null, null));
                Assert.Equal(cortada, procura.Cortada);
                Assert.Equal(esperadas, procura.Linhas.Select(l => l.Ferramenta.ToolId).Distinct().Count());
                Assert.Equal(
                    procura.Linhas.OrderByDescending(l => l.InicioEm.HasValue).ThenByDescending(l => l.InicioEm).ThenByDescending(l => l.JobOnId).Select(l => l.JobOnId),
                    procura.Linhas.Select(l => l.JobOnId));
                contagens[esperadas] = conta.Vezes;
            }

            // Antes: 1 + 4 por ferramenta + 5 (5 → 26, 40 → 166); depois: igual para 5 e para 40.
            Assert.Equal(contagens[5], contagens[40]);
            Assert.Equal(12, contagens[40]);
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"FerramentaUtilizacoes\" WHERE \"ToolId\" LIKE {marker + "-T%"}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"SapFabricos\" WHERE \"Material\" LIKE {marker + "R%"}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"CmContexts\" WHERE \"ToolId\" LIKE {marker + "-T%"}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"MfContexts\" WHERE \"ToolId\" LIKE {marker + "-T%"}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"BqContexts\" WHERE \"ToolId\" LIKE {marker + "-T%"}");
            foreach (var m in maquinas)
            {
                await DeleteJobOnAsync(db, m.Id);
            }

            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" LIKE {marker + "-T%"}");
        }
    }

    /// <summary>O filtro que a procura aplicava depois de ler (linha e dias do Job On).</summary>
    private static bool CabeNoFiltro(FerramentaProducao p, HistoricoFiltro? filtro) =>
        filtro is null
        || ((string.IsNullOrWhiteSpace(filtro.Linha) || string.Equals(p.Maquina, filtro.Linha.Trim(), StringComparison.OrdinalIgnoreCase))
            && (filtro.De is not { } de || p.FimEm is null || p.FimEm >= de)
            && (filtro.Ate is not { } ate || (p.InicioEm is { } inicio && inicio <= ate)));
}
