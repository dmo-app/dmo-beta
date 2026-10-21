using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A utilização pelo SAP no PostgreSQL: a migração guarda a percentagem com
/// duas casas e as peças, a leitura fecha o lembrete na mesma gravação, e a
/// base não deixa dois lembretes abertos da mesma ferramenta.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task UsoSap_ImportarFechaOLembreteEUmSoAberto_NoPostgreSql()
    {
        SkipWithoutConnection();
        await using var db = CreateContext();
        var marca = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var nome = $"MZZ{marca}MPPSX";
        var tool = new Tool { ToolId = $"T{marca}", Type = ToolType.Cm, Reference = $"ZZ{marca}", Lot = "7", TechnicalName = nome, RegisteredAtUtc = DateTime.UtcNow };
        db.Tools.Add(tool);
        await db.SaveChangesAsync();
        try
        {
            var servico = new FerramentasUsoSap(db, new SessaoUsoSap());
            Assert.True(await servico.PedirAsync(tool.ToolId));
            Assert.Contains($"{nome}07", await servico.CodigosAsync());
            var lista = $"<table><tr><th>Equipamento</th><th>Expected life</th><th>Consumption [million units]</th><th>% Use</th></tr><tr><td>{nome}07</td><td>43.200.000</td><td>38.962.451</td><td>90,25</td></tr></table>";

            var outcome = await servico.ImportarAsync(lista, "percentagem.HTML", DateTime.UtcNow);

            Assert.Equal(1, outcome.Atualizadas);
            await using var leitura = CreateContext();
            var uso = await leitura.FerramentaUtilizacoes.AsNoTracking().SingleAsync(u => u.ToolId == tool.ToolId);
            Assert.Equal((90.25m, 38_962_451L, 43_200_000L), (uso.Percentagem, uso.Consumo!.Value, uso.VidaPrevista!.Value));
            var lembrete = await leitura.FerramentaLembretesUtilizacao.AsNoTracking().SingleAsync(l => l.ToolId == tool.ToolId);
            Assert.Equal(uso.Id, lembrete.UtilizacaoId);
            Assert.DoesNotContain($"{nome}07", await new FerramentasUsoSap(leitura, new SessaoUsoSap()).CodigosAsync());

            leitura.FerramentaLembretesUtilizacao.AddRange(
                new FerramentaLembreteUtilizacao { ToolId = tool.ToolId, CriadoEmUtc = DateTime.UtcNow },
                new FerramentaLembreteUtilizacao { ToolId = tool.ToolId, CriadoEmUtc = DateTime.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => leitura.SaveChangesAsync());
        }
        finally
        {
            await using var limpar = CreateContext();
            await limpar.FerramentaLembretesUtilizacao.Where(l => l.ToolId == tool.ToolId).ExecuteDeleteAsync();
            await limpar.FerramentaUtilizacoes.Where(u => u.ToolId == tool.ToolId).ExecuteDeleteAsync();
            await limpar.Tools.Where(t => t.ToolId == tool.ToolId).ExecuteDeleteAsync();
        }
    }

    private sealed class SessaoUsoSap : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "Rui", OperatorId = "2165" };
    }
}
