using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A Entrada da CM e do MF juntos na base (0.2.0-beta.11): grava os dois na
/// mesma transação; e quando, ao mesmo tempo, alguém dá entrada só ao MF
/// noutra posição, o MF fica numa posição só e nada fica a meio.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Armazem_ACmEOMfEntramJuntos_NoPostgreSql()
    {
        SkipWithoutConnection();
        var (cm, mf, codigos) = await ParEPosicoesAsync(1);
        try
        {
            await using var db = CreateContext();
            var saida = await Movimentos(db).EntradaAsync(new PedidoDeEntrada(
                Guid.NewGuid(), cm, codigos[0], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null, ComPar: true));

            Assert.True(saida.ComPar);
            await using var leitura = CreateContext();
            Assert.Equal(2, await leitura.ArmazemOcupacoes.CountAsync(o => (o.ToolId == cm || o.ToolId == mf) && o.LibertadaEmUtc == null));
            Assert.Equal(2, await leitura.FerramentaEstados.CountAsync(e => e.ToolId == cm || e.ToolId == mf));
        }
        finally
        {
            await LimparAsync(mf, []);
            await LimparAsync(cm, codigos);
        }
    }

    [SkippableFact]
    public async Task Armazem_OParEOMfSozinhoAoMesmoTempo_OMfFicaNumaPosicaoSo()
    {
        SkipWithoutConnection();
        var (cm, mf, codigos) = await ParEPosicoesAsync(2);
        try
        {
            var comPar = Task.Run(async () =>
            {
                await using var db = CreateContext();
                return await Movimentos(db).EntradaAsync(new PedidoDeEntrada(
                    Guid.NewGuid(), cm, codigos[0], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null, ComPar: true));
            });
            var sozinho = Task.Run(async () =>
            {
                await using var db = CreateContext();
                return await Movimentos(db).EntradaAsync(new PedidoDeEntrada(
                    Guid.NewGuid(), mf, codigos[1], ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, null, null));
            });
            await Task.WhenAll(comPar, sozinho);

            await using var leitura = CreateContext();
            Assert.Equal(1, await leitura.ArmazemOcupacoes.CountAsync(o => o.ToolId == mf && o.LibertadaEmUtc == null));
            Assert.Equal(1, await leitura.ArmazemOcupacoes.CountAsync(o => o.ToolId == cm && o.LibertadaEmUtc == null));
            // Cada ocupação tem o seu movimento: nada ficou a meio.
            var movimentos = await leitura.ArmazemMovimentos.CountAsync(m => m.ToolId == cm || m.ToolId == mf);
            Assert.Equal(2, movimentos);
        }
        finally
        {
            await LimparAsync(mf, []);
            await LimparAsync(cm, codigos);
        }
    }

    private async Task<(string Cm, string Mf, string[] Codigos)> ParEPosicoesAsync(int posicoes)
    {
        var (cm, codigos) = await FerramentaEPosicoesAsync(posicoes);
        await using var db = CreateContext();
        var tool = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == cm);
        var mf = "arm-" + Guid.NewGuid().ToString("N");
        db.Tools.Add(new Tool { ToolId = mf, Type = ToolType.Mf, Reference = tool.Reference, Lot = tool.Lot });
        await db.SaveChangesAsync();
        return (cm, mf, codigos);
    }
}
