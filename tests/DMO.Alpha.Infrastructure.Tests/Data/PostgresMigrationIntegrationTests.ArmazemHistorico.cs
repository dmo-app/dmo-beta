using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O Histórico do Armazém no PostgreSQL (0.2.0-beta.12): o texto (referência,
/// lote e posição), o movimento, o destino e os dias traduzem-se na base.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Armazem_OHistoricoFiltraNoPostgreSql()
    {
        SkipWithoutConnection();
        var (toolId, codigos) = await FerramentaEPosicoesAsync(1);
        try
        {
            await using var db = CreateContext();
            var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
            await Movimentos(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), toolId, codigos[0], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null));
            await Movimentos(db).SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), toolId, ArmazemDestino.Producao, null, null, null, null, null));
            var referencia = db.Tools.Single(t => t.ToolId == toolId).Reference;
            var historico = new ArmazemHistorico(db);

            var pelaReferencia = await historico.ListarAsync(new ArmazemHistoricoFiltro(Texto: referencia[..5]), 1);
            var pelaPosicao = await historico.ListarAsync(new ArmazemHistoricoFiltro(Texto: codigos[0], ToolId: toolId), 1);
            var saidas = await historico.ListarAsync(
                new ArmazemHistoricoFiltro(ToolId: toolId, Tipo: ArmazemMovimentoTipo.Saida, Destino: ArmazemDestino.Producao, De: hoje, Ate: hoje), 1);
            var amanha = await historico.ListarAsync(new ArmazemHistoricoFiltro(ToolId: toolId, De: hoje.AddDays(1)), 1);

            Assert.Equal(2, pelaReferencia.Items.Count(l => l.ToolId == toolId));
            Assert.Equal(2, pelaPosicao.TotalCount);
            Assert.Equal($"Saída {codigos[0]} para Produção", Assert.Single(saidas.Items).Descricao);
            Assert.Equal(0, amanha.TotalCount);
        }
        finally
        {
            await LimparAsync(toolId, codigos);
        }
    }
}
