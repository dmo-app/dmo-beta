using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Remover um reparador das Boquilhas que já recebeu uma BQ pelo Armazém
/// (vistoria BD-02, 27/10/2026) contra um PostgreSQL real: a FK das saídas do
/// Armazém (Restrict) recusava e a página dava erro. O handler larga também
/// essa ligação; o movimento fica com o nome que guardou na saída.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task RemoverReparadorUsadoNoArmazem_OMovimentoFicaComONome()
    {
        SkipWithoutConnection();
        var (toolId, codigos) = await FerramentaEPosicoesAsync(0);
        var marker = "RA-" + Guid.NewGuid().ToString("N")[..8];
        int? repairerId = null;
        try
        {
            await using var db = CreateContext();
            var repairer = new BqRepairer { Name = marker };
            db.BqRepairers.Add(repairer);
            await db.SaveChangesAsync();
            repairerId = repairer.Id;
            db.ArmazemMovimentos.Add(new ArmazemMovimento
            {
                Tipo = ArmazemMovimentoTipo.Saida, ToolId = toolId, Destino = ArmazemDestino.Reparacao,
                BqReparadorId = repairer.Id, ReparadorNome = marker,
                OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var outcome = await new DeleteBqRepairerHandler(db).HandleAsync(new DeleteBqRepairer(repairer.Id));

            Assert.Equal(BqRepairerWriteResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            Assert.False(await db.BqRepairers.AnyAsync(r => r.Id == repairer.Id));
            var saida = await db.ArmazemMovimentos.AsNoTracking().SingleAsync(m => m.ToolId == toolId);
            Assert.Null(saida.BqReparadorId);
            Assert.Equal(marker, saida.ReparadorNome);
        }
        finally
        {
            await LimparAsync(toolId, codigos);
            if (repairerId is { } id)
            {
                await using var db = CreateContext();
                await db.BqRepairers.Where(r => r.Id == id).ExecuteDeleteAsync();
            }
        }
    }
}
