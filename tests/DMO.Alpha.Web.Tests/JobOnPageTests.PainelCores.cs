using System.Text.RegularExpressions;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnPageTests
{
    // O painel lateral da Folha com as cores do Planeamento (Diogo, 23/10/2026):
    // cada cartão leva a classe da sua linha (dmo-linha-cor mch-X, a mesma da
    // lista do dia) e o selo dentro dele também.
    [Fact]
    public async Task FolhaNova_PainelLateral_CadaCartaoLevaACorDaSuaLinha()
    {
        SeedUser("4461", "Chefe painel", "provider-folha-painel", "Folha1234!");
        int primeiro;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
            var maquinas = new[] { "C2", "B1", "C3" }
                .Select(c => db.Machines.FirstOrDefault(m => m.Code == c) ?? new Machine { Code = c }).ToList();
            var jobs = maquinas.Select((m, i) => new JobOn
            {
                Reference = $"61{i}8Y232", ProductionNumber = $"20269{i}", Machine = m, InicioEm = hoje, FimEm = hoje.AddDays(3)
            }).ToList();
            db.JobOns.AddRange(jobs);
            await db.SaveChangesAsync();
            primeiro = jobs[0].Id;
        }
        using var client = await LoginClientAsync("4461", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"/JobOn/Folha?jobon_id={primeiro}");
        var painel = html[html.IndexOf("class=\"rail-list\"", StringComparison.Ordinal)..];
        painel = painel[..painel.IndexOf("</aside>", StringComparison.Ordinal)];
        var cartoes = Regex.Matches(painel, "<a class=\"rail-item dmo-linha-cor mch-(?<cartao>[A-Z0-9]+)(?<sel> selected)?\"[^>]*><b>(?<ref>[^<]*)<span class=\"mch dmo-linha-cor mch-(?<selo>[A-Z0-9]+)\">(?<texto>[^<]*)</span>");
        var nossos = cartoes.Where(m => m.Groups["ref"].Value.StartsWith("61", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, nossos.Count);
        foreach (var m in nossos)
        {
            Assert.Equal(m.Groups["cartao"].Value, m.Groups["selo"].Value);
            Assert.Equal(m.Groups["cartao"].Value, m.Groups["texto"].Value);
        }
        Assert.Equal(new[] { "B1", "C2", "C3" }, nossos.Select(m => m.Groups["cartao"].Value).OrderBy(c => c, StringComparer.Ordinal));
        Assert.Single(nossos, m => m.Groups["sel"].Success && m.Groups["cartao"].Value == "C2");
        // Nenhum cartão do painel fica sem a classe da linha.
        Assert.Equal(Regex.Matches(painel, "<a class=\"rail-item").Count, cartoes.Count);
    }
}
