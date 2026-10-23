using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// «Ter forma de remover o notice de troca em caso de engano» (Diogo,
/// 23/10/2026): em «O que mudou» da folha, quem tem o Job On criar abre uma
/// alteração e remove-a por engano. Nada se perde: a linha fica gravada,
/// marcada com quem e quando, e aparece riscada em «O que mudou»; sai da
/// marca laranja do dia e do Sumário. Os valores do Job On não mudam. Quem só
/// vê o Job On não vê a janela e um POST forjado não muda nada.
/// </summary>
public class JobOnRemoverAlteracaoTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;

    public JobOnRemoverAlteracaoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    public void Dispose() => ResetDomainState();

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.JobOnChanges.RemoveRange(db.JobOnChanges);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = "RemoverPass1!";
        _factory.AuthProvider.AddAccount(email, "provider-" + operatorId, password, email);
        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Planeador " + operatorId, ProviderUserId = "provider-" + operatorId };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "tpl-remover-" + operatorId,
                Name = "Remover " + operatorId,
                Capabilities = capabilities
            };
            db.Templates.Add(template);
            db.Users.Add(user);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var token = await TokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId), ("password", password), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (client, user.Id);
    }

    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var html = await (await client.GetAsync(path)).Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private static async Task<string> PageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static string Folha(int jobOnId) => $"/JobOn/Folha?jobon_id={jobOnId}&vista=alteracoes";

    private static string Dia()
    {
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        return $"/JobOn?ano={hoje.Year}&mes={hoje.Month}&dia={hoje.Day}";
    }

    /// <summary>Uma produção que começou há dois dias, com duas alterações de hoje: a BQ (lote 12 → 13) e o fim.</summary>
    private async Task<(int JobOnId, int Bq, int Fim)> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b1 = new Machine { Code = "R1" };
        db.Machines.Add(b1);
        db.Tools.Add(new Tool { ToolId = "CM-R", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "MF-R", Type = ToolType.Mf, Reference = "5447", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "BQ-R", Type = ToolType.Bq, Reference = "T194", Lot = "13" });
        var jobOn = new JobOn
        {
            Reference = "5447T194", ProductionNumber = "202677", Machine = b1,
            InicioEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-2)
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-R" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-R" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-R" });
        var agora = DateTime.UtcNow;
        var bq = new JobOnChange
        {
            JobOn = jobOn, Field = JobOnChangeFields.Bq, OldValueFrozen = "T194 · lote 12", NewValueFrozen = "T194 · lote 13",
            ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora.AddMinutes(-2)
        };
        var fim = new JobOnChange
        {
            JobOn = jobOn, Field = JobOnChangeFields.End, OldValueFrozen = null, NewValueFrozen = "31/12/2026",
            ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora.AddMinutes(-1)
        };
        db.JobOnChanges.AddRange(bq, fim);
        await db.SaveChangesAsync();
        return (jobOn.Id, bq.Id, fim.Id);
    }

    private async Task<HttpResponseMessage> RemoverAsync(HttpClient client, int jobOnId, int changeId, string token) =>
        await client.PostAsync("/JobOn/RemoverAlteracao", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("change_id", changeId.ToString()), ("__RequestVerificationToken", token)));

    private async Task<JobOnChange> ChangeAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DmoDbContext>().JobOnChanges.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    [Fact]
    public async Task Remover_SaiDaMarcaEDoSumario_FicaRiscadaComQuemEQuando_EOsValoresNaoMudam()
    {
        var (jobOnId, bqId, fimId) = await SeedAsync();
        var (client, userId) = await LoginAsync("7201", DmoCapabilities.JobOnCreate);
        using var _ = client;

        // Antes: as duas na folha (abríveis), na marca do dia e no Sumário.
        var antes = await PageAsync(client, Folha(jobOnId));
        Assert.Contains("O que mudou desde o início (2 alterações)", antes);
        Assert.Contains($"data-jof-alteracao=\"{bqId}\"", antes);
        Assert.Contains($"data-jof-alteracao=\"{fimId}\"", antes);
        Assert.Contains("<dialog id=\"jobon-remover-alteracao\"", antes);
        Assert.Contains(">Remover por engano</button>", antes);
        // Sem o aviso amarelo na lista do dia (Diogo, 23/10/2026): as alterações vivem na folha e no Sumário.
        Assert.DoesNotContain("BQ · Sai 12 entra 13", await PageAsync(client, Dia()));
        Assert.Contains("Mudou a BQ: T194 · lote 12 → T194 · lote 13", await PageAsync(client, "/Planeamento/Sumario?tipo=alteracoes"));

        var resposta = await RemoverAsync(client, jobOnId, bqId, HtmlFormHelpers.ExtractRequestVerificationToken(antes)!);
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={jobOnId}&vista=alteracoes&removida=1", resposta.Headers.Location!.OriginalString);

        // Nada se perde: a linha fica, com quem e quando.
        var removida = await ChangeAsync(bqId);
        Assert.NotNull(removida.RemovedAtUtc);
        Assert.Equal("Planeador 7201", removida.RemovedByNameFrozen);
        Assert.Equal(userId, removida.RemovedByUserId);
        Assert.Equal("T194 · lote 13", removida.NewValueFrozen);
        Assert.Null((await ChangeAsync(fimId)).RemovedAtUtc);

        // «O que mudou»: a removida riscada, com «Removida por X a dd/MM/yyyy HH:mm»; a outra ativa.
        var depois = await PageAsync(client, resposta.Headers.Location!.OriginalString);
        ExportarParaOBrowser.Gravar("jobon-remover-alteracao.html", depois);
        Assert.Contains("O que mudou desde o início (1 alteração · 1 removida)", depois);
        Assert.Contains($"data-jof-alteracao-removida=\"{bqId}\"", depois);
        Assert.Contains("<strong><s>Mudou a BQ: T194 · lote 12 → T194 · lote 13</s></strong>", depois);
        Assert.Contains($"Removida por Planeador 7201 a {DmoBusinessTime.ToLocal(removida.RemovedAtUtc!.Value):dd/MM/yyyy HH:mm}", depois);
        Assert.DoesNotContain($"data-jof-alteracao=\"{bqId}\"", depois);
        Assert.Contains($"data-jof-alteracao=\"{fimId}\"", depois);
        Assert.Contains("Alteração removida. Os valores do Job On não mudaram.", depois);
        // A removida não leva ao módulo (Rever o Controlo era o da BQ).
        Assert.DoesNotContain("Rever o Controlo", depois);

        // A marca laranja do dia e o Sumário: só a que ficou.
        var dia = await PageAsync(client, Dia());
        Assert.DoesNotContain("beta-light-changes", dia);
        Assert.DoesNotContain("Sai 12 entra 13", dia);
        var sumario = await PageAsync(client, "/Planeamento/Sumario?tipo=alteracoes");
        Assert.DoesNotContain("Mudou a BQ: T194 · lote 12 → T194 · lote 13", sumario);
        Assert.Contains("Mudou o fim: — → 31/12/2026", sumario);

        // Os valores do Job On não mudam: a BQ continua a de agora.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("BQ-R", await db.BqContexts.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.ToolId).SingleAsync());
        Assert.Equal(2, await db.JobOnChanges.AsNoTracking().CountAsync(c => c.JobOnId == jobOnId));
    }

    [Fact]
    public async Task RemoverAUltimaAtiva_AMarcaSai_AFolhaContinuaAMostrarAsRemovidas_EUmSegundoEnvioNaoMuda()
    {
        var (jobOnId, bqId, fimId) = await SeedAsync();
        var (client, _) = await LoginAsync("7202", DmoCapabilities.JobOnCreate);
        using var __ = client;
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await PageAsync(client, Folha(jobOnId)))!;

        Assert.Equal(HttpStatusCode.Redirect, (await RemoverAsync(client, jobOnId, bqId, token)).StatusCode);
        // Com uma ativa ainda há marca e janela.
        Assert.DoesNotContain("beta-light-changes", await PageAsync(client, Dia()));
        Assert.Contains("<dialog id=\"jobon-remover-alteracao\"", await PageAsync(client, Folha(jobOnId)));

        Assert.Equal(HttpStatusCode.Redirect, (await RemoverAsync(client, jobOnId, fimId, token)).StatusCode);
        // Sem nenhuma ativa: a marca sai, o Sumário fica sem elas, a folha mostra as duas riscadas e já não tem a janela.
        Assert.DoesNotContain("beta-light-changes", await PageAsync(client, Dia()));
        var sumario = await PageAsync(client, "/Planeamento/Sumario?tipo=alteracoes");
        Assert.DoesNotContain("T194 · lote 12 → T194 · lote 13", sumario);
        Assert.DoesNotContain("31/12/2026", sumario);
        var folha = await PageAsync(client, Folha(jobOnId));
        Assert.Contains("O que mudou desde o início (0 alterações · 2 removidas)", folha);
        Assert.DoesNotContain("<dialog id=\"jobon-remover-alteracao\"", folha);

        // Um segundo envio (duplo clique) não muda quem nem quando.
        var primeiro = await ChangeAsync(bqId);
        var repetido = await RemoverAsync(client, jobOnId, bqId, token);
        Assert.Equal($"/JobOn/Folha?jobon_id={jobOnId}&vista=alteracoes", repetido.Headers.Location!.OriginalString);
        Assert.Equal(primeiro.RemovedAtUtc, (await ChangeAsync(bqId)).RemovedAtUtc);
    }

    [Fact]
    public async Task UmaAlteracaoDeOutroJobOn_NaoSeRemove()
    {
        var (jobOnId, bqId, _) = await SeedAsync();
        var (client, _) = await LoginAsync("7203", DmoCapabilities.JobOnCreate);
        using var __ = client;
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await PageAsync(client, Folha(jobOnId)))!;

        var resposta = await RemoverAsync(client, jobOnId + 1000, bqId, token);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Null((await ChangeAsync(bqId)).RemovedAtUtc);
    }

    [Fact]
    public async Task QuemSoVeOJobOn_NaoVeAJanela_EUmPostForjadoNaoMudaNada()
    {
        var (jobOnId, bqId, _) = await SeedAsync();
        var (client, _) = await LoginAsync("7204", DmoCapabilities.JobOnView);
        using var __ = client;

        var folha = await PageAsync(client, Folha(jobOnId));
        Assert.DoesNotContain("jobon-remover-alteracao", folha);
        Assert.DoesNotContain("Remover por engano", folha);
        Assert.DoesNotContain("data-jof-alteracao", folha);

        var resposta = await RemoverAsync(client, jobOnId, bqId, HtmlFormHelpers.ExtractRequestVerificationToken(folha) ?? string.Empty);

        Assert.NotEqual(HttpStatusCode.OK, resposta.StatusCode);
        var change = await ChangeAsync(bqId);
        Assert.Null(change.RemovedAtUtc);
        Assert.Null(change.RemovedByNameFrozen);
    }
}
