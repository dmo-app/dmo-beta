using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Uma importação do relatório de fabrico de cada vez (audit externo,
/// 16/11/2026, DMO-757): o script do SAP e a pasta ligada podem mandar ao
/// mesmo tempo. A segunda espera pelo trinco «sap-fabrico» da primeira, como
/// a importação da % uso. No PostgreSQL, com duas ligações.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ImportarFabricoSap_EsperaPeloTrincoDeOutraImportacao()
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var material = "TF" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        try
        {
            // Outra importação segura o trinco.
            await using var outra = CreateContext();
            await using var transacao = await outra.Database.BeginTransactionAsync();
            await DbWrite.LockAsync(outra, "sap-fabrico", CancellationToken.None);

            await using var importar = CreateContext();
            var servico = new FerramentasFabricoSap(importar, new SessaoDoSap(), NullLogger<FerramentasFabricoSap>.Instance);
            var leitura = servico.ImportarAsync(
                [new FabricoSapLinha(material, "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_000, 900)],
                "fabrico.xlsx", DateTime.UtcNow);
            await Task.Delay(500);
            Assert.False(leitura.IsCompleted, "a importação espera pelo trinco da outra");

            await transacao.CommitAsync();
            var outcome = await leitura.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(1, outcome.Novas);
            Assert.Equal(1, await setup.SapFabricos.CountAsync(s => s.Material == material));
        }
        finally
        {
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"SapFabricos\" WHERE \"Material\" = {material}");
        }
    }

    private sealed class SessaoDoSap : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "SAP", OperatorId = "0001" };
    }
}
