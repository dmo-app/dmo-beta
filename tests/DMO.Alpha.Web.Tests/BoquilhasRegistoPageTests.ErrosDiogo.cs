using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Os erros do Diogo nas Boquilhas (Diogo, 27/10/2026): o cartão de cada
/// linha mostra a BQ em máquina com o lote, e o aviso «O Job On mudou» fica
/// numa linha no cabeçalho do registo, para a página não saltar ao trocar de
/// máquina.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task PainelDasLinhas_MostraABqEmMaquinaComOLote_EAProducaoNumaLinhaDiscreta()
    {
        await SeedToolAsync("BQ-ERROS-LINHA", "T977", "041");
        await SeedProductionAsync("BQ-ERROS-LINHA", "9977T977", "PN-977", "EL1");
        SeedUser("4271", "Painel BQ Lote", "provider-painel-bq-lote", "PainelBqLote1!");
        using var client = await LoginClientAsync("4271", "PainelBqLote1!");

        var painel = PainelDasLinhas(await GetPageHtmlAsync(client, "/Boquilhas"));
        var inicio = painel.IndexOf("<strong>EL1</strong>", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "A EL1 não aparece no painel.");
        var linha = painel[inicio..painel.IndexOf("</button>", inicio, StringComparison.Ordinal)];
        Assert.Contains("<span>BQ T977 · Lote 41</span>", linha);
        Assert.Contains("<span>9977T977 · PN-977</span>", linha);
        Assert.True(linha.IndexOf("BQ T977", StringComparison.Ordinal) < linha.IndexOf("9977T977", StringComparison.Ordinal),
            "A BQ e o lote vêm antes da produção.");
    }

    [Fact]
    public async Task JobOnMudou_FicaNumaLinhaNoCabecalhoDoRegisto_SemBlocoPorBaixoDosCartoes()
    {
        await SeedToolAsync("BQ-ERROS-AVISO", "T978", "17");
        var bq = await SeedProductionAsync("BQ-ERROS-AVISO", "9978T978", "PN-978", "EL2");
        var trace = await SeedTraceAsync("BQ-ERROS-AVISO", bq.Id);
        await SeedMovementAsync(trace.Id, "saida", 5);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = bq.JobOnId!.Value,
                Field = JobOnChangeFields.End,
                OldValueFrozen = "2026-10-30",
                NewValueFrozen = "2026-11-03",
                ChangedByNameFrozen = "Diogo",
                ChangedAtUtc = DateTime.UtcNow.AddMinutes(5)
            });
            await db.SaveChangesAsync();
        }
        SeedUser("4272", "Registo Aviso", "provider-registo-aviso", "RegistoAviso1!");
        using var client = await LoginClientAsync("4272", "RegistoAviso1!");

        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Boquilhas?bq_id={bq.Id}"));
        var cabecalho = html.IndexOf("<div class=\"card-head\">", html.IndexOf("class=\"card register-open\"", StringComparison.Ordinal), StringComparison.Ordinal);
        var aviso = html.IndexOf("id=\"bqJobOnMudou\"", StringComparison.Ordinal);
        var cartoes = html.IndexOf("id=\"bqRegisterFacts\"", StringComparison.Ordinal);
        Assert.True(cabecalho >= 0 && aviso > cabecalho && cartoes > aviso, "O aviso não está no cabeçalho do registo.");
        Assert.DoesNotContain("class=\"jobon-change-notice\"", html);
        Assert.Contains("<summary title=\"O Job On mudou depois deste registo.\"><span class=\"dmo-caret\" aria-hidden=\"true\"></span>Job On mudou</summary>", html);
        Assert.Contains("Mudou o fim", html);
        Assert.Contains($"href=\"/JobOn?jobon_id={bq.JobOnId}&vista=alteracoes#fichaMudancas\">Ver o que mudou</a>", html);
    }
}
