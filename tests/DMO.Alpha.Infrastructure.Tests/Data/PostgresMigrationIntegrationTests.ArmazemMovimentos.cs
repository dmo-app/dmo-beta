using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Os movimentos do Armazém na base (0.2.0-beta.9, migração
/// ArmazemMovimentos): a mesma ferramenta entra ao mesmo tempo em quatro
/// posições e fica numa só (as outras leem «já está no armazém»); o mesmo
/// envio ao mesmo tempo grava um movimento; a procura e a Saída traduzem-se
/// no PostgreSQL; e a base recusa um movimento com os dois reparadores.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Armazem_AMesmaFerramentaEntraAoMesmoTempo_FicaNumaPosicaoSo()
    {
        SkipWithoutConnection();
        var (toolId, codigos) = await FerramentaEPosicoesAsync(4);
        try
        {
            var resultados = await Task.WhenAll(codigos.Select(async codigo =>
            {
                await using var db = CreateContext();
                return (await Movimentos(db).EntradaAsync(new PedidoDeEntrada(
                    Guid.NewGuid(), toolId, codigo, ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, null, null))).Resultado;
            }));

            Assert.Single(resultados, r => r == ArmazemMovimentoResultado.Gravado);
            Assert.All(resultados.Where(r => r != ArmazemMovimentoResultado.Gravado), r => Assert.Equal(ArmazemMovimentoResultado.JaNoArmazem, r));
            await using var leitura = CreateContext();
            Assert.Equal(1, await leitura.ArmazemMovimentos.CountAsync(m => m.ToolId == toolId));
            Assert.Equal(1, await leitura.ArmazemOcupacoes.CountAsync(o => o.ToolId == toolId));
        }
        finally
        {
            await LimparAsync(toolId, codigos);
        }
    }

    [SkippableFact]
    public async Task Armazem_OMesmoEnvioAoMesmoTempo_GravaUmMovimento()
    {
        SkipWithoutConnection();
        var (toolId, codigos) = await FerramentaEPosicoesAsync(1);
        try
        {
            var pedido = new PedidoDeEntrada(Guid.NewGuid(), toolId, codigos[0], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null);
            var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var db = CreateContext();
                return (await Movimentos(db).EntradaAsync(pedido)).Resultado;
            }));

            Assert.Single(resultados, r => r == ArmazemMovimentoResultado.Gravado);
            Assert.All(resultados.Where(r => r != ArmazemMovimentoResultado.Gravado), r => Assert.Equal(ArmazemMovimentoResultado.JaRegistado, r));
            await using var leitura = CreateContext();
            Assert.Equal(1, await leitura.ArmazemMovimentos.CountAsync(m => m.ToolId == toolId));
            Assert.Equal(1, await leitura.FerramentaEstados.CountAsync(e => e.ToolId == toolId));
        }
        finally
        {
            await LimparAsync(toolId, codigos);
        }
    }

    [SkippableFact]
    public async Task Armazem_ProcurarESaida_NoPostgreSql()
    {
        SkipWithoutConnection();
        var (toolId, codigos) = await FerramentaEPosicoesAsync(1);
        try
        {
            await using var db = CreateContext();
            await Movimentos(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), toolId, codigos[0], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null));

            var pelaPosicao = await Movimentos(db).ProcurarAsync(codigos[0]);
            var saida = await Movimentos(db).SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), toolId, ArmazemDestino.Producao, null, null, null, null, null));
            var situacao = await Movimentos(db).SituacaoAsync(toolId);

            Assert.Contains(pelaPosicao, e => e.ToolId == toolId && e.NoArmazem);
            Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
            Assert.Equal((ArmazemOrigem.Producao, FerramentaEstadoTipo.PorReparar), (situacao!.OrigemProposta!.Value, situacao.EstadoProposto!.Value));
        }
        finally
        {
            await LimparAsync(toolId, codigos);
        }
    }

    [SkippableFact]
    public async Task Armazem_ABaseRecusaUmMovimentoComOsDoisReparadores()
    {
        SkipWithoutConnection();
        var (toolId, codigos) = await FerramentaEPosicoesAsync(0);
        try
        {
            await using var db = CreateContext();
            var reparador = new Reparador { Nome = "Dois " + Guid.NewGuid().ToString("N")[..8] };
            var bq = new DMO.Alpha.Core.Boquilhas.BqRepairer { Name = "Dois BQ " + Guid.NewGuid().ToString("N")[..8] };
            db.Reparadores.Add(reparador);
            db.BqRepairers.Add(bq);
            await db.SaveChangesAsync();
            db.ArmazemMovimentos.Add(new ArmazemMovimento
            {
                Tipo = ArmazemMovimentoTipo.Saida, ToolId = toolId, Destino = ArmazemDestino.Reparacao,
                ReparadorId = reparador.Id, BqReparadorId = bq.Id, OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow,
                OperacaoId = Guid.NewGuid()
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Reparadores.Remove(reparador);
            db.BqRepairers.Remove(bq);
            await db.SaveChangesAsync();
        }
        finally
        {
            await LimparAsync(toolId, codigos);
        }
    }

    private ArmazemMovimentos Movimentos(DMO.Alpha.Infrastructure.Data.DmoDbContext db) =>
        new(db, new DMO.Alpha.Infrastructure.Data.OperacoesRegistadas(db), new SemSessao());

    private async Task<(string ToolId, string[] Codigos)> FerramentaEPosicoesAsync(int posicoes)
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marca = Random.Shared.Next(100000, 999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var toolId = "arm-" + Guid.NewGuid().ToString("N");
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = "9" + marca + "T1", Lot = "1" });
        var codigos = Enumerable.Range(0, posicoes).Select(i => $"M{marca}{i}").ToArray();
        db.ArmazemPosicoes.AddRange(codigos.Select(c => new ArmazemPosicao { Codigo = c, CriadaEmUtc = DateTime.UtcNow }));
        await db.SaveChangesAsync();
        return (toolId, codigos);
    }

    private async Task LimparAsync(string toolId, string[] codigos)
    {
        await using var db = CreateContext();
        await db.FerramentaEstados.Where(e => e.ToolId == toolId).ExecuteDeleteAsync();
        await db.FerramentaLocalizacoes.Where(l => l.ToolId == toolId).ExecuteDeleteAsync();
        await db.ArmazemOcupacoes.Where(o => o.ToolId == toolId).ExecuteDeleteAsync();
        var operacoes = db.ArmazemMovimentos.Where(m => m.ToolId == toolId).Select(m => m.OperacaoId);
        await db.OperacoesRegistadas.Where(o => operacoes.Contains(o.Id)).ExecuteDeleteAsync();
        await db.ArmazemMovimentos.Where(m => m.ToolId == toolId).ExecuteDeleteAsync();
        await db.ArmazemPosicoes.Where(p => codigos.Contains(p.Codigo)).ExecuteDeleteAsync();
        await db.Tools.Where(t => t.ToolId == toolId).ExecuteDeleteAsync();
    }
}
