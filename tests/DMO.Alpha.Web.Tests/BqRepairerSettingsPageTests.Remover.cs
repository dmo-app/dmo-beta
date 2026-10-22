using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class BqRepairerSettingsPageTests
{
    [Fact]
    public async Task DeleteRepairer_PelaJanela_SaiDaLista_ASaidaGuardaONome_EARotaFicaVazia()
    {
        // «Não existe forma de remover ou editar reparadores» (Diogo,
        // 21/10/2026): cada linha tem «Opções» › «Remover reparador», que abre uma janela; a saída
        // já feita guarda o nome e a máquina que o usava fica sem reparador.
        // Na base real, a ligação da saída ao reparador não deixa apagá-lo
        // sem a largar primeiro.
        using var client = await LoginBoquilhasAsync("4731");
        var repairerId = await SeedRepairerAsync("Vidroart");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var trace = new BqRepairTrace { ToolId = "BQ-REMOVER" };
            db.BqRepairTraces.Add(trace);
            await db.SaveChangesAsync();
            db.BqMovements.Add(new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 2, BqRepairerId = repairerId });
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = BqRepairerRouting.SettingKeyFor("B1"), SettingValue = repairerId.ToString() });
            await db.SaveChangesAsync();
        }

        var html = await GetPageHtmlAsync(client);
        // O «Remover reparador» está na ficha do reparador (um clique na linha).
        Assert.Contains($"data-dmo-ficha=\"bqDefFicha-{repairerId}\"", TabelaDosReparadores(html));
        Assert.Contains($"data-dmo-dialog=\"bqDefRemover-{repairerId}\" aria-label=\"Remover o reparador Vidroart\">Remover reparador</button>", html);
        var janela = html[html.IndexOf($"<dialog id=\"bqDefRemover-{repairerId}\"", StringComparison.Ordinal)..];
        janela = janela[..janela.IndexOf("</dialog>", StringComparison.Ordinal)];
        Assert.Contains("Quer mesmo remover «Vidroart»?", WebUtility.HtmlDecode(janela));
        Assert.Contains("?handler=DeleteRepairer", janela);

        using var response = await PostAsync(client, "DeleteRepairer", ("repairer_id", repairerId.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("save=ok-removido", response.Headers.Location?.OriginalString);

        var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, "save=ok-removido"));
        Assert.Contains("Reparador removido.", depois);
        Assert.Contains("Sem reparadores criados.", depois);

        using var verificar = _factory.Services.CreateScope();
        var dbDepois = verificar.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Empty(await dbDepois.BqRepairers.AsNoTracking().ToListAsync());
        var saida = await dbDepois.BqMovements.AsNoTracking().SingleAsync();
        Assert.Null(saida.BqRepairerId);
        Assert.Equal("Vidroart", saida.BqRepairerNameFrozen);
        Assert.Equal(string.Empty, (await dbDepois.ModuleSettings.AsNoTracking()
            .SingleAsync(s => s.SettingKey == BqRepairerRouting.SettingKeyFor("B1"))).SettingValue);

        // Um reparador que já não existe: a recusa tipada, sem erro.
        using var outraVez = await PostAsync(client, "DeleteRepairer", ("repairer_id", repairerId.ToString()));
        Assert.Contains("save=reparador-inexistente", outraVez.Headers.Location?.OriginalString);
    }
}
