using System.Net;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O REPARADOR NA LINHA DO ARMAZÉM (Diogo, 28/10/2026: «Reparador deve aparecer depois do
/// estado reparado»): na folha de ver, «Reparado   5252 · MOLDIN»; a linha «Reparador» à
/// parte sai (sem registo: «Sem registo · MOLDIN»). No Editar o campo continua.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_ReparadorDepoisDoEstadoEDaPosicao_SemLinhaAParte()
    {
        SeedUser("4612", "Chefe reparador", "provider-folha-reparador", "Folha1234!");
        int jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "R570-CM", Type = ToolType.Cm, Reference = "8326", Lot = "7" });
            db.Tools.Add(new Tool { ToolId = "R570-MF", Type = ToolType.Mf, Reference = "8326", Lot = "6" });
            var jobOn = new JobOn
            {
                Reference = "8326T570",
                ProductionNumber = "202602",
                Machine = db.Machines.FirstOrDefault(m => m.Code == "C1") ?? new Machine { Code = "C1" },
                InicioEm = new DateOnly(2026, 8, 5),
            };
            db.JobOns.Add(jobOn);
            db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "R570-CM" });
            db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "R570-MF" });
            var folha = new JobOnFolha { JobOn = jobOn };
            folha.DefinirValor("cm_reparador", "MOLDIN");
            folha.DefinirValor("mf_reparador", "MOLDIN");
            db.JobOnFolhas.Add(folha);
            var posicao = new ArmazemPosicao { Codigo = "5252", CriadaEmUtc = DateTime.UtcNow };
            db.ArmazemPosicoes.Add(posicao);
            var entrada = new ArmazemMovimento
            {
                Tipo = ArmazemMovimentoTipo.Entrada, ToolId = "R570-CM",
                OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid()
            };
            db.ArmazemMovimentos.Add(entrada);
            await db.SaveChangesAsync();
            db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = "R570-CM", PosicaoId = posicao.Id, MovimentoId = entrada.Id, DesdeUtc = DateTime.UtcNow });
            db.FerramentaEstados.Add(new FerramentaEstado { ToolId = "R570-CM", Estado = FerramentaEstadoTipo.Reparado, RegistadoEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            jobOnId = jobOn.Id;
        }

        using var client = await LoginClientAsync("4612", "Folha1234!");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        var cm = html.IndexOf("data-jof-familia=\"CM\"", StringComparison.Ordinal);
        var mf = html.IndexOf("data-jof-familia=\"MF\"", StringComparison.Ordinal);
        var cartaoCm = html[cm..mf];

        // CM: «Reparado   5252 · MOLDIN», e a linha «Reparador» à parte já não aparece.
        Assert.Contains("<b>Reparado</b><span class=\"jof-onde-espaco\"> </span><b>5252</b><span class=\"jof-onde-reparador\" title=\"Reparador\"> · <b>MOLDIN</b></span></p>", cartaoCm, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-fora=\"CM\"", cartaoCm, StringComparison.Ordinal);
        // MF sem registo no Armazém: «Sem registo · MOLDIN»; ficam o FF e o Lote FF.
        var cartaoMf = html[mf..];
        Assert.Contains("<p class=\"jof-onde\" data-dmo-onde-esta title=\"Sem registo\"><b>Sem registo</b><span class=\"jof-onde-reparador\" title=\"Reparador\"> · <b>MOLDIN</b></span></p>", cartaoMf, StringComparison.Ordinal);
        Assert.Contains("<small>Lote FF</small>", cartaoMf, StringComparison.Ordinal);
        Assert.DoesNotContain("<small>Reparador</small>", cartaoMf, StringComparison.Ordinal);

        // No Editar o campo do reparador da CM continua.
        var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}&editar=1");
        Assert.Contains("data-jof-fora=\"CM\"", editar, StringComparison.Ordinal);
    }
}
