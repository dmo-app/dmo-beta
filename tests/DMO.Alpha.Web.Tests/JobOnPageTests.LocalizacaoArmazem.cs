using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A LOCALIZAÇÃO NO CARTÃO (Diogo, 28/10/2026: «pedi para meter em bold», «a localização no armazém
/// abri ao registo entrar sair»): a negrito sempre, e quem gere o Armazém abre a Entrada/Saída da
/// ferramenta ao carregar nela, também quando ainda não tem registo.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_LocalizacaoANegrito_AbreAEntradaSaidaDoArmazem()
    {
        var email = "4613@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-folha-localizacao", "Folha1234!", email);
        int jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = new User { Id = Guid.NewGuid(), OperatorId = "4613", Name = "Armazém", ProviderUserId = "provider-folha-localizacao" };
            db.Users.Add(user);
            var template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-test-jobon-armazem", Name = "Job On e Armazém", Capabilities = new[] { DmoCapabilities.JobOnView, DmoCapabilities.ArmazemGestao } };
            db.Templates.Add(template);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            db.Tools.Add(new Tool { ToolId = "L28-MF", Type = ToolType.Mf, Reference = "2032", Lot = "2" });
            var jobOn = new JobOn
            {
                Reference = "2032C028",
                ProductionNumber = "202605",
                Machine = db.Machines.FirstOrDefault(m => m.Code == "B1") ?? new Machine { Code = "B1" },
                InicioEm = new DateOnly(2026, 10, 20),
            };
            db.JobOns.Add(jobOn);
            db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "L28-MF" });
            await db.SaveChangesAsync();
            jobOnId = jobOn.Id;
        }

        using var client = await LoginClientAsync("4613", "Folha1234!");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));

        Assert.Contains("<a class=\"jof-onde-pos\" href=\"/Armazem/Movimentos?ferramenta=L28-MF\" title=\"Sem registo: dar entrada ou saída no Armazém\"><b>Sem registo</b></a>", html, StringComparison.Ordinal);
    }
}
