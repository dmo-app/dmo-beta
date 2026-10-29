using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnPageTests
{
    // O painel das linhas da Folha mostra só o que entra nos próximos 5 dias (Diogo, 29/10/2026):
    // nem a produção que já corre desde antes de hoje, nem a que entra daqui a 6 dias.
    [Fact]
    public async Task Folha_PainelDasLinhas_SoOQueEntraNosProximosCincoDias()
    {
        SeedUser("4462", "Chefe cinco dias", "provider-folha-cinco", "Folha1234!");
        int aberto;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
            Machine M(string c) => db.Machines.FirstOrDefault(m => m.Code == c) ?? new Machine { Code = c };
            var jobs = new[]
            {
                new JobOn { Reference = "5D01T100", ProductionNumber = "202681", Machine = M("C1"), InicioEm = hoje.AddDays(-3), FimEm = hoje.AddDays(4) },
                new JobOn { Reference = "5D02T100", ProductionNumber = "202682", Machine = M("C2"), InicioEm = hoje.AddDays(2), FimEm = hoje.AddDays(9) },
                new JobOn { Reference = "5D03T100", ProductionNumber = "202683", Machine = M("C3"), InicioEm = hoje.AddDays(5), FimEm = hoje.AddDays(9) },
                new JobOn { Reference = "5D04T100", ProductionNumber = "202684", Machine = M("B1"), InicioEm = hoje.AddDays(6), FimEm = hoje.AddDays(9) },
            };
            db.JobOns.AddRange(jobs);
            await db.SaveChangesAsync();
            aberto = jobs[0].Id;
        }
        using var client = await LoginClientAsync("4462", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"/JobOn/Folha?jobon_id={aberto}");
        var painel = html[html.IndexOf("class=\"rail-list\"", StringComparison.Ordinal)..];
        painel = painel[..painel.IndexOf("</aside>", StringComparison.Ordinal)];
        Assert.DoesNotContain("5D01T100", painel);
        Assert.Contains("5D02T100", painel);
        Assert.Contains("5D03T100", painel);
        Assert.DoesNotContain("5D04T100", painel);
    }
}
