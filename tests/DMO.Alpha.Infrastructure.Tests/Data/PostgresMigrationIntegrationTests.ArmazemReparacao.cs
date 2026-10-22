using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Infrastructure.Armazem;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A Reparação do Armazém no PostgreSQL (Diogo, 22/10/2026): o último
/// movimento de cada ferramenta e os filtros novos dos movimentos (tipo,
/// referência, lote, Linha e reparador, com a subconsulta da Entrada que
/// volta da Reparação) traduzem-se na base.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Armazem_AReparacaoLeNoPostgreSql()
    {
        SkipWithoutConnection();
        var (toolId, codigos) = await FerramentaEPosicoesAsync(1);
        var reparador = new Reparador { Nome = "Rep " + Guid.NewGuid().ToString("N")[..8] };
        try
        {
            await using var db = CreateContext();
            db.Reparadores.Add(reparador);
            await db.SaveChangesAsync();
            var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
            await Movimentos(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), toolId, codigos[0], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null));
            await Movimentos(db).SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), toolId, ArmazemDestino.Reparacao, reparador.Id, null, null, null, null));
            var reparacao = new ArmazemReparacao(db);
            var historico = new ArmazemHistorico(db);

            var fora = await reparacao.ForaAsync();
            var hojeLista = await historico.ListarAsync(new ArmazemHistoricoFiltro(ToolId: toolId, De: hoje, Ate: hoje, Reparador: reparador.Nome), 1);
            var amanha = await historico.ListarAsync(new ArmazemHistoricoFiltro(ToolId: toolId, De: hoje.AddDays(1), TipoFerramenta: DMO.Alpha.Core.Tools.ToolType.Cm, Referencia: "9", Lote: "1"), 1);

            Assert.Equal(reparador.Nome, Assert.Single(fora, f => f.ToolId == toolId).Reparador);
            Assert.Equal(ArmazemMovimentoTipo.Saida, Assert.Single(hojeLista.Items).Movimento);
            Assert.NotEmpty(await historico.ReparadoresAsync());
            await historico.LinhasAsync();
            await historico.ListarAsync(new ArmazemHistoricoFiltro(Linha: "L-nenhuma"), 1);
            Assert.DoesNotContain(amanha.Items, m => m.ToolId == toolId);

            await Movimentos(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), toolId, codigos[0], ArmazemOrigem.Reparacao, FerramentaEstadoTipo.Reparado, null, null));
            var depois = await historico.ListarAsync(new ArmazemHistoricoFiltro(ToolId: toolId, De: hoje, Ate: hoje, Reparador: reparador.Nome), 1);
            Assert.DoesNotContain(await reparacao.ForaAsync(), f => f.ToolId == toolId);
            var entrada = depois.Items.First(m => m.ToolId == toolId);
            Assert.Equal((ArmazemMovimentoTipo.Entrada, reparador.Nome, codigos[0]), (entrada.Movimento, entrada.Reparador, entrada.Posicao));
        }
        finally
        {
            await LimparAsync(toolId, codigos);
            await using var db = CreateContext();
            db.Reparadores.Remove(reparador);
            await db.SaveChangesAsync();
        }
    }
}
