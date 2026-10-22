using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A ferramenta escrita na base (migração ReparacaoInternaOutraFerramenta):
/// a base aceita o contexto do Job On ou a referência e o lote escritos sem
/// contexto (com ou sem Job On) e recusa a mistura, a falta das duas e a
/// escrita com espaços; o índice das por associar é filtrado; o rasto aceita
/// a associação e continua sem se mudar; associar ao mesmo tempo associa uma
/// vez; registar sem Job On e a Consulta delas traduzem-se na base real; e
/// eliminar a ferramenta associada passa-a para a REMOVED.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ReparacaoInternaOutraFerramenta_ABaseAceitaAsDuasFormas_ERecusaAMistura()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        try
        {
            foreach (var errada in new[]
            {
                // O contexto e a escrita juntos; nem uma nem outra; só a referência; só o lote.
                Escrita(Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, "1"), "5447T173", "4"),
                Escrita(Linha(cenario, ReparacaoTipo.Cm, null, null, "2"), null, null),
                Escrita(Linha(cenario, ReparacaoTipo.Cm, null, null, "3"), "5447T173", null),
                Escrita(Linha(cenario, ReparacaoTipo.Mf, null, null, "4"), null, "4"),
                // Sem Job On com o contexto; a escrita com espaços ou vazia.
                SemJobOn(Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, "5")),
                Escrita(Linha(cenario, ReparacaoTipo.Cm, null, null, "6"), " 5447T173", "4"),
                Escrita(Linha(cenario, ReparacaoTipo.Cm, null, null, "7"), "5447T173", " "),
                // Associada sem quando, ou numa reparação do Job On.
                Associada(Escrita(Linha(cenario, ReparacaoTipo.Cm, null, null, "8"), "5447T173", "4"), cenario.Marca + "-cm", null),
                Associada(Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, "9"), cenario.Marca + "-cm", DateTime.UtcNow)
            })
            {
                await using var db = CreateContext();
                db.ReparacoesInternas.Add(errada);
                var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(erro.InnerException).SqlState);
            }

            await using (var certa = CreateContext())
            {
                certa.ReparacoesInternas.AddRange(
                    Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, "10"),
                    Escrita(Linha(cenario, ReparacaoTipo.Cm, null, null, "11"), "5447T173", "4"),
                    SemJobOn(Escrita(Linha(cenario, ReparacaoTipo.Mf, null, null, "12"), "5447T173", "4")),
                    Associada(Escrita(Linha(cenario, ReparacaoTipo.Cm, null, null, "13"), "5447T173", "4"), cenario.Marca + "-cm", DateTime.UtcNow));
                await certa.SaveChangesAsync();
            }

            await using var leitura = CreateContext();
            Assert.Equal(4, await leitura.ReparacoesInternas.CountAsync(r => r.MachineId == cenario.MachineId));
            // O índice das por associar, filtrado.
            var indice = await leitura.Database.SqlQueryRaw<string>(
                "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'ReparacoesInternas' AND indexname = 'IX_ReparacoesInternas_PorAssociar'").SingleAsync();
            Assert.Contains("WHERE", indice);
            Assert.Contains("\"ReferenciaEscrita\" IS NOT NULL", indice);
            Assert.Contains("\"AssociadaToolId\" IS NULL", indice);
            Assert.Contains("\"AnuladaEmUtc\" IS NULL", indice);
        }
        finally
        {
            await LimparReparacaoAsync(cenario);
        }
    }

    [SkippableFact]
    public async Task ReparacaoInternaOutraFerramenta_SemJobOn_AssociarEORasto_NaBaseReal()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        // Uma máquina sem produção, só deste teste.
        int semProducao;
        await using (var db = CreateContext())
        {
            var maquina = new DMO.Alpha.Core.Production.Machine { Code = cenario.Marca + "S" };
            db.Machines.Add(maquina);
            db.Tools.Add(new Tool { ToolId = cenario.Marca + "-cm2", Type = ToolType.Cm, Reference = cenario.Marca, Lot = "2" });
            await db.SaveChangesAsync();
            semProducao = maquina.Id;
        }

        try
        {
            int id;
            await using (var db = CreateContext())
            {
                var saida = await Reparacoes(db).RegistarAsync(new PedidoDeReparacao(
                    Guid.NewGuid(), semProducao, null, ReparacaoTipo.Cm, null, "21", false, cenario.Marca.ToLowerInvariant(), "2"));
                Assert.Equal((ReparacaoResultado.Gravado, true), (saida.Resultado, saida.SemJobOn));
                id = saida.ReparacaoId!.Value;
                Assert.Equal("21", Assert.Single((await Reparacoes(db).ListarSemProducaoAsync(semProducao, await Reparacoes(db).DiaOperacionalAsync(), null)).Items).Numero);
            }

            await using (var db = CreateContext())
            {
                // A Consulta: sem Job On, com as ferramentas que servem (as do lote 2).
                var linha = Assert.Single((await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(MachineId: semProducao, PorAssociar: true), null)).Items);
                Assert.Equal(((int?)null, (string?)null, cenario.Marca), (linha.JobOnId, linha.Producao, linha.Escrita!.Referencia));
                Assert.Equal(new[] { cenario.Marca + "-cm2" }, linha.Escrita.Candidatas!.Select(c => c.ToolId));
            }

            // Associar ao mesmo tempo, com envios diferentes: associa uma vez.
            var resultados = await Task.WhenAll(new[] { cenario.Marca + "-cm2", cenario.Marca + "-cm", cenario.Marca + "-cm2" }.Select(async toolId =>
            {
                await using var db = CreateContext();
                return (await Reparacoes(db).AssociarAsync(new PedidoDeAssociacao(Guid.NewGuid(), id, toolId))).Resultado;
            }));
            Assert.Single(resultados, r => r == AssociacaoResultado.Associado);
            Assert.All(resultados.Where(r => r != AssociacaoResultado.Associado), r => Assert.Equal(AssociacaoResultado.JaAssociada, r));

            await using (var db = CreateContext())
            {
                var rasto = await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync(r => r.ReparacaoId == id);
                Assert.Equal((ReparacaoAcao.Associado, (string?)null), (rasto.Acao, rasto.NumeroDepois));
                Assert.Equal((await db.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.Id == id)).AssociadaToolId, rasto.ToolIdAssociado);
                // A ferramenta do rasto não se muda; uma associação sem ferramenta não entra.
                var erro = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                    "UPDATE \"ReparacoesInternasRasto\" SET \"ToolIdAssociado\" = 'outra' WHERE \"ReparacaoId\" = {0}", id));
                Assert.Equal(PostgresErrorCodes.CheckViolation, erro.SqlState);
                db.ReparacoesInternasRasto.Add(new ReparacaoRasto { ReparacaoId = id, Acao = ReparacaoAcao.Associado, NumeroAntes = "21", EmUtc = DateTime.UtcNow });
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            }

            // Eliminar a ferramenta associada: a reparação passa para a REMOVED (a FK não deixa ficar pendurada).
            var associada = (await LerAsync(id)).AssociadaToolId!;
            await using (var db = CreateContext())
            {
                Assert.Equal(DeleteToolResult.Success, (await new DeleteToolHandler(db).HandleAsync(new DeleteTool(associada))).Result);
            }

            Assert.Equal(ToolRegistryConstants.RemovedToolId, (await LerAsync(id)).AssociadaToolId);
        }
        finally
        {
            await using (var limpar = CreateContext())
            {
                var ids = limpar.ReparacoesInternas.Where(r => r.MachineId == semProducao).Select(r => (int?)r.Id);
                await limpar.OperacoesRegistadas.Where(o => o.ResultadoTipo == ReparacoesInternas.ResultadoTipo && ids.Contains(o.ResultadoId)).ExecuteDeleteAsync();
                await limpar.ReparacoesInternas.Where(r => r.MachineId == semProducao).ExecuteDeleteAsync();
                await limpar.Machines.Where(m => m.Id == semProducao).ExecuteDeleteAsync();
            }

            await LimparReparacaoAsync(cenario);
        }
    }

    private async Task<Reparacao> LerAsync(int id)
    {
        await using var db = CreateContext();
        return await db.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private static Reparacao Escrita(Reparacao reparacao, string? referencia, string? lote)
    {
        reparacao.ReferenciaEscrita = referencia;
        reparacao.LoteEscrito = lote;
        return reparacao;
    }

    private static Reparacao SemJobOn(Reparacao reparacao)
    {
        reparacao.JobOnId = null;
        return reparacao;
    }

    private static Reparacao Associada(Reparacao reparacao, string toolId, DateTime? em)
    {
        reparacao.AssociadaToolId = toolId;
        reparacao.AssociadaEmUtc = em;
        return reparacao;
    }
}
