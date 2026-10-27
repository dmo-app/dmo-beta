using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// As opções do filtro «Informação» (Diogo, 27/10/2026: «Importante, Job
/// on(inclui muda, alteraçoes, experiencias) Armazem, Controlo»): Toda,
/// Importante, Job On, Armazém e Controlo, cada uma só a quem vê os tipos dela.
/// </summary>
public sealed partial class PlaneamentoSumarioPageTests
{
    [Fact]
    public async Task Filtro_TemImportante_JobOn_Armazem_EControlo()
    {
        using var client = await LoginAsync("5951", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        var html = await GetAsync(client);

        Assert.Contains("<option value=\"\" selected=\"selected\">Toda</option>", html);
        Assert.Contains("<option value=\"importante\">Importante</option>", html);
        Assert.Contains("<option value=\"jobon\">Job On</option>", html);
        Assert.Contains("<option value=\"armazem\">Armazém</option>", html);
        Assert.Contains("<option value=\"controlo\">Controlo</option>", html);
        Assert.True(html.IndexOf("value=\"importante\"", StringComparison.Ordinal) < html.IndexOf("value=\"jobon\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("value=\"jobon\"", StringComparison.Ordinal) < html.IndexOf("value=\"armazem\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("value=\"armazem\"", StringComparison.Ordinal) < html.IndexOf("value=\"controlo\"", StringComparison.Ordinal));
        // Os tipos que o Job On e o Controlo juntam deixam de ser opções à parte.
        foreach (var chave in new[] { "criados", "comecam", "alteracoes", "por-ver", "por-aprovar", "nao-aprovados" })
        {
            Assert.DoesNotContain($"<option value=\"{chave}\"", html);
        }
    }

    [Fact]
    public async Task Filtro_JobOn_JuntaCriados_Mudancas_EExperiencias_SemOsImportantes()
    {
        await JobOnAsync("S9F001", Hoje, DateTime.UtcNow);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = await db.Machines.FirstAsync(m => m.Code == "B1");
            db.JobOns.Add(new JobOn { Reference = "S9F002", ProductionNumber = "202600", Machine = machine, InicioEm = Hoje, CriadoEmUtc = DateTime.UtcNow, Experiencia = true });
            await db.SaveChangesAsync();
        }

        using var client = await LoginAsync("5952", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        var html = await GetAsync(client, "?tipo=jobon");

        Assert.Contains("<option value=\"jobon\" selected=\"selected\">Job On</option>", html);
        Assert.Contains("<div class=\"sumario-grupo\" data-sumario-tipo=\"criados\">", html);
        Assert.Contains("<div class=\"sumario-grupo\" data-sumario-tipo=\"comecam\">", html);
        Assert.Contains("S9F001 7", html);
        Assert.Contains("S9F002 202600", html);
        Assert.DoesNotContain("data-sumario-tipo=\"importante\"", html);
        // O calendário e os links guardam a escolha.
        Assert.Contains("tipo=jobon", html);
    }

    [Fact]
    public async Task Filtro_Controlo_MostraSoOsDoControloNoTopo()
    {
        var jobOnId = await JobOnAsync("S9F003", Hoje, DateTime.UtcNow);
        using var client = await LoginAsync("5953", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        var html = await GetAsync(client, "?tipo=controlo");

        Assert.Contains("<option value=\"controlo\" selected=\"selected\">Controlo</option>", html);
        Assert.DoesNotContain("<option value=\"importante\" selected=\"selected\">", html);
        var grupo = GrupoImportante(html);
        Assert.Matches($"data-sumario-tipo=\"por-ver\">[\\s\\S]*?href=\"/Controlo/Resumo\\?jobon_id={jobOnId}\">S9F003 7</a>", grupo);
        Assert.DoesNotContain("data-sumario-tipo=\"criados\"", html);
        Assert.DoesNotContain("data-sumario-tipo=\"comecam\"", html);
    }

    [Fact]
    public async Task Filtro_SoMostraAsOpcoesDosModulosDaConta()
    {
        using var client = await LoginAsync("5954", DmoCapabilities.JobOnView);

        var html = await GetAsync(client);

        // Quem só tem o Job On ver recebe os Job On e o Armazém, sem o Controlo.
        Assert.Contains("<option value=\"jobon\">Job On</option>", html);
        Assert.Contains("<option value=\"armazem\">Armazém</option>", html);
        Assert.DoesNotContain("<option value=\"controlo\"", html);
        Assert.DoesNotContain("<option value=\"importante\"", html);
    }
}
