using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova a FICHA DA PRODUÇÃO (o job-sheet do dmo-html) no slice vertical
/// nativo: as AÇÕES a partir da ficha (Editar, Duplicar (Job On ou Lote),
/// Abrir Resumo/Peso/Pegamentos), o campo Calote (mm) EDITÁVEL (a migração
/// aditiva da B1 — alimenta o apoio TP/calote do Peso), os FATOS TÉCNICOS
/// das ferramentas na escolha (o showToolFacts portado — o endpoint
/// read-only do backend e a ficha server-render) e os avisos INFORMATIVOS
/// que nunca bloqueiam (a compatibilidade registada e o uso paralelo).
/// SEM «Colocar/substituir na máquina» manual e SEM «Retirar do
/// planeamento» — as datas do Job On mandam (a decisão 14/10).
/// </summary>
public class JobOnFichaTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/JobOn";

    private readonly CustomWebApplicationFactory _factory;

    public JobOnFichaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    public void Dispose()
    {
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.JobOnLotNotes.RemoveRange(db.JobOnLotNotes);
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.ToolMachineLinks.RemoveRange(db.ToolMachineLinks);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void SeedUser(string operatorId, string name, string providerUserId, string password)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-test-planejador");
        if (template is null)
        {
            template = new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "tpl-test-planejador",
                Name = "Planejador de teste — Job On",
                Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
            };
            db.Templates.Add(template);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = name,
            ProviderUserId = providerUserId
        };
        db.Users.Add(user);
        db.SaveChanges();

        db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TemplateId = template.Id
        });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginClientAsync(string identifier, string password)
    {
        var client = CreateClient();
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// O worked example da ficha: a CM 5447 (lote 3, NNPB, com os técnicos
    /// e a TP) registada para B1 e C3, a BQ T194 (marisa 10, lote 800), o
    /// MF 5810 (gargalo 12,5) — e a produção 202601 em B1 com a calote 9,5
    /// e o controlo associado; uma OUTRA produção ativa (202602) também
    /// usa a CM (o uso paralelo).
    /// </summary>
    private async Task<(int JobOnId, int ControloId, int B1Id, int C3Id)> SeedFichaAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var b1 = new Machine { Code = "B1" };
        var c3 = new Machine { Code = "C3" };
        db.Machines.Add(b1);
        db.Machines.Add(c3);

        db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12", BqAccountedQuantity = 800 });
        db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });

        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb,
            VolumePuncaoCm3 = 30m,
            DiametroGargaloMm = 4.5m,
            TpDiametroMm = 50m,
            TpRaioMm = 24.11m,
            TpSagittaMm = 10m
        });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "BQ-TOOL-9",
            VolumeMarisaCm3 = 10m,
            DiametroGargaloMm = 1.8m
        });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "MF-TOOL-9",
            DiametroGargaloMm = 12.5m
        });
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T3", MachineId = b1.Id });
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T3", MachineId = c3.Id });
        await db.SaveChangesAsync();

        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = "202601",
            Machine = b1,
            InicioEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3),
            CaloteMm = 9.5m
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-TOOL-9" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" });
        await db.SaveChangesAsync();

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();

        // A OUTRA produção ATIVA (começou, não terminou) com a mesma CM —
        // o uso paralelo que avisa sem bloquear.
        var outra = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = "202602",
            Machine = c3,
            InicioEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-1)
        };
        db.JobOns.Add(outra);
        db.CmContexts.Add(new CmContext { JobOn = outra, ToolId = "T3" });
        await db.SaveChangesAsync();

        return (jobOn.Id, controlo.Id, b1.Id, c3.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_OverlapWarningsReflectKnownDates_AndNeverBlockSaving(bool realConflicts)
    {
        var (jobOnId, controloId, machineId, _) = await SeedFichaAsync();
        SeedUser("4591", "Planejador", "provider-jobon-warnings", "JobWarning1!");
        using var client = await LoginClientAsync("4591", "JobWarning1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOns.Add(new JobOn { ProductionNumber = "SEM-DATAS", Reference = "1075C142", MachineId = machineId });
            if (realConflicts)
            {
                db.JobOns.Add(new JobOn { ProductionNumber = "ATE-AVISO", Reference = "1075C142", MachineId = machineId, InicioEm = new(2026, 4, 10) });
                db.JobOns.Add(new JobOn { ProductionNumber = "COM-FIM", Reference = "1075C142", MachineId = machineId, InicioEm = new(2026, 4, 11), FimEm = new(2026, 4, 15) });
            }
            await db.SaveChangesAsync();
        }
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        using var saved = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()), ("machine_id", machineId.ToString()),
            ("cm_tool_id", "T3"), ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-9"),
            ("inicio_em", "2026-04-12"), ("fim_em", "2026-04-20"), ("calote_mm", "9.5"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var location = saved.Headers.Location!.ToString();
        if (realConflicts)
        {
            Assert.Contains("overlap=1", location);
            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
            Assert.Contains("ainda está em aberto", html);
            Assert.DoesNotContain("Ajusta o início da nova para depois de 15/04/2026", html);
        }
        else
        {
            Assert.StartsWith($"{PagePath}?jobon_id={jobOnId}", location, StringComparison.Ordinal);
            Assert.DoesNotContain("overlap=1", location);
        }
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var job = await finalDb.JobOns.SingleAsync(j => j.Id == jobOnId);
        Assert.Equal(new DateOnly(2026, 4, 12), job.InicioEm);
        Assert.Equal(new DateOnly(2026, 4, 20), job.FimEm);
        Assert.Equal(9.5m, job.CaloteMm);
        Assert.Equal(controloId, await finalDb.Controlos.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync());
    }

    [Fact]
    public async Task Opcoes_EditarPrimeiro_SemDuplicarLote_EFecharVoltaAFicha()
    {
        // O Diogo, 17/10/2026: as Opções começam por Editar e Duplicar Job
        // On; o «Duplicar lote» sai (um lote novo associa-se no Editar e fica
        // como alteração). O Fechar do Editar volta à ficha e o da ficha sai.
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        SeedUser("4592", "Planejador", "provider-jobon-lote", "JobLote1!");
        using var client = await LoginClientAsync("4592", "JobLote1!");

        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&ano=2026&mes=10&dia=5"));
        var editar = ficha.IndexOf("editar=1#lightSheet\">Editar</a>", StringComparison.Ordinal);
        var duplicar = ficha.IndexOf("duplicar=1\">Duplicar Job On</a>", StringComparison.Ordinal);
        var controlo = ficha.IndexOf($"href=\"/Controlo/Resumo?jobon_id={jobOnId}\">Controlo</a>", StringComparison.Ordinal);
        Assert.True(editar >= 0 && editar < duplicar && duplicar < controlo);
        Assert.DoesNotContain("Duplicar lote", ficha);
        Assert.DoesNotContain("duplicar=lote", ficha);
        Assert.Contains("<a class=\"dmo-button\" href=\"/JobOn?ano=2026&mes=10&dia=5\" data-dmo-parcial>Fechar</a>", ficha);

        // O formulário antigo da mudança de lote já não abre.
        var antigo = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&duplicar=lote"));
        Assert.DoesNotContain("Mudança de lote", antigo);

        var edicao = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1"));
        Assert.Contains($"<a class=\"dmo-button\" href=\"/JobOn?jobon_id={jobOnId}#lightFicha\">Fechar</a>", edicao);
    }

    [Fact]
    public async Task CriarCmNoEditar_VoltaAEdicaoDoMesmoJobOnERegistaATroca()
    {
        // O Diogo, 17/10/2026: no Editar de um Job On aberto, «Criar CM» para
        // trocar a CM durante o fabrico abria um Job On novo e pedia o
        // número da produção. Agora volta à edição do mesmo Job On, com as
        // outras ferramentas, e o Guardar regista a troca em «O que mudou».
        var (jobOnId, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4598", "Planejador", "provider-jobon-criar-cm", "JobCriarCm1!");
        using var client = await LoginClientAsync("4598", "JobCriarCm1!");

        var edicao = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1"));
        var link = System.Text.RegularExpressions.Regex.Match(edicao, "href=\"([^\"]+)\" data-tool-create>Criar CM</a>");
        Assert.True(link.Success);
        var criar = link.Groups[1].Value;
        Assert.StartsWith($"/Ferramentas?criar=1&type=CM&machine_ids={b1Id}&return=", criar, StringComparison.Ordinal);
        var regresso = Uri.UnescapeDataString(criar[(criar.IndexOf("return=", StringComparison.Ordinal) + "return=".Length)..]);
        Assert.StartsWith($"/JobOn?jobon_id={jobOnId}&editar=1&retoma=1&machine_id={b1Id}", regresso, StringComparison.Ordinal);
        Assert.Contains("mf_tool_id=MF-TOOL-9", regresso);
        Assert.Contains("bq_tool_id=BQ-TOOL-9", regresso);
        Assert.Contains("calote_mm=9.5", regresso);
        // A versão da folha aberta volta com ela (edições ao mesmo tempo, B4).
        Assert.Contains("&versao=0&", regresso);
        Assert.EndsWith("&cm_tool_id={tool_id}#lightSheet", regresso, StringComparison.Ordinal);
        Assert.DoesNotContain("novo=1", regresso);

        // A CM criada nas Ferramentas (T4) volta no lugar da T3.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
            await db.SaveChangesAsync();
        }

        var voltou = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, regresso.Replace("{tool_id}", "T4")));
        Assert.Contains("Preparar a edição de 202601", voltou);
        Assert.Contains("<option value=\"T4\" selected=\"selected\">", voltou);
        Assert.Contains("<option value=\"MF-TOOL-9\" selected=\"selected\">", voltou);
        Assert.DoesNotContain("<option value=\"T3\" selected=\"selected\">", voltou);
        Assert.Contains("value=\"9.5\"", voltou);

        var token = await GetVerificationTokenAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        using var response = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()),
            ("machine_id", b1Id.ToString()),
            ("cm_tool_id", "T4"),
            ("mf_tool_id", "MF-TOOL-9"),
            ("bq_tool_id", "BQ-TOOL-9"),
            ("inicio_em", DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3).ToString("yyyy-MM-dd")),
            ("calote_mm", "9.5"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("T4", (await checkDb.CmContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId)).ToolId);
        var troca = await checkDb.JobOnChanges.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
        Assert.Equal(JobOnChangeFields.Cm, troca.Field);
        Assert.Equal("5447 · lote 3", troca.OldValueFrozen);
        Assert.Equal("5447 · lote 4", troca.NewValueFrozen);
        Assert.Equal(1, await checkDb.JobOns.CountAsync(j => j.Reference == "1075C142" && j.ProductionNumber == "202601"));
    }

    [Fact]
    public async Task CriarCmNaCriacao_VoltaComOQueEstavaEscrito()
    {
        var (_, _, _, c3Id) = await SeedFichaAsync();
        SeedUser("4599", "Planejador", "provider-jobon-criar-novo", "JobCriarNovo1!");
        using var client = await LoginClientAsync("4599", "JobCriarNovo1!");

        var regresso = $"/JobOn?novo=1&reference=6018Y232&production_number=202616&retoma=1&machine_id={c3Id}&mf_tool_id=MF-TOOL-9&cm_tool_id=T3#lightSheet";
        var voltou = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, regresso));
        Assert.Contains("Preparar produção", voltou);
        Assert.Contains("value=\"6018Y232\"", voltou);
        Assert.Contains("value=\"202616\"", voltou);
        Assert.Contains("<option value=\"T3\" selected=\"selected\">", voltou);
        Assert.Contains("<option value=\"MF-TOOL-9\" selected=\"selected\">", voltou);

        var link = System.Text.RegularExpressions.Regex.Match(voltou, "href=\"([^\"]+)\" data-tool-create>Criar BQ</a>");
        Assert.True(link.Success);
        var criar = link.Groups[1].Value;
        var novoRegresso = Uri.UnescapeDataString(criar[(criar.IndexOf("return=", StringComparison.Ordinal) + "return=".Length)..]);
        Assert.StartsWith("/JobOn?novo=1&reference=6018Y232&production_number=202616&retoma=1", novoRegresso, StringComparison.Ordinal);
        Assert.Contains("cm_tool_id=T3", novoRegresso);
        Assert.EndsWith("&bq_tool_id={tool_id}#lightSheet", novoRegresso, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CriarBq_LevaAoRegistoDasFerramentasOTipoBqEAMaquinaDaProducao()
    {
        // O Diogo, 18/10/2026: «não vamos inventar mais páginas para
        // ferramentas, 1 página e funciona com todas … estamos no jobon
        // 6018Y232 C2, mudar boquilha já vem com C2 BQ filtrado». O «Criar BQ»
        // da folha leva ao registo das Ferramentas o tipo BQ e a máquina da
        // produção.
        var (_, _, _, c3Id) = await SeedFichaAsync();
        SeedUser("4595", "Planejador", "provider-jobon-criar-bq", "JobCriarBq1!");
        using var client = await LoginClientAsync("4595", "JobCriarBq1!");

        var folha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?novo=1&reference=6018Y232&production_number=202616&machine_id={c3Id}"));
        var link = System.Text.RegularExpressions.Regex.Match(folha, "href=\"([^\"]+)\" data-tool-create>Criar BQ</a>");
        Assert.True(link.Success);
        var criar = link.Groups[1].Value;
        // As Ferramentas marcam a máquina do machine_ids (o «Criar boquilha»
        // das Boquilhas segue o mesmo caminho até ao fim).
        Assert.StartsWith($"/Ferramentas?criar=1&type=BQ&machine_ids={c3Id}&return=", criar, StringComparison.Ordinal);

        // Sem máquina na folha, o registo abre sem máquina escolhida.
        var semMaquina = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?novo=1&reference=6018Y232&production_number=202616"));
        var outro = System.Text.RegularExpressions.Regex.Match(semMaquina, "href=\"([^\"]+)\" data-tool-create>Criar BQ</a>");
        Assert.True(outro.Success);
        Assert.StartsWith("/Ferramentas?criar=1&type=BQ&return=", outro.Groups[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrepararProducao_SoAoCriar_EAMaquinaVaziaAbreACriacaoComDiaEMaquina()
    {
        // O Diogo, 17/10/2026: «Preparar produção» não fica sempre aberto;
        // abre no «Criar Job On» ou ao carregar numa máquina vazia do dia,
        // já com a máquina e a data.
        var (_, _, _, c3Id) = await SeedFichaAsync();
        SeedUser("4597", "Planejador", "provider-jobon-criar", "JobCriar1!");
        using var client = await LoginClientAsync("4597", "JobCriar1!");
        var dia = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-10); // antes das duas produções: C3 vazia

        var pagina = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={dia.Year}&mes={dia.Month}&dia={dia.Day}"));
        Assert.DoesNotContain("id=\"lightSheet\"", pagina);
        var criarAqui = $"/JobOn?novo=1&machine_id={c3Id}&inicio_em={dia:yyyy-MM-dd}&ano={dia.Year}&mes={dia.Month}&dia={dia.Day}#lightSheet";
        Assert.Contains($"<a class=\"beta-light-machine-empty\" href=\"{criarAqui}\"", pagina);

        var folha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, criarAqui.Split('#')[0]));
        Assert.Contains("id=\"lightSheet\"", folha);
        Assert.Contains($"<option value=\"{c3Id}\" selected=\"selected\">C3</option>", folha);
        Assert.Contains($"name=\"inicio_em\" type=\"date\" value=\"{dia:yyyy-MM-dd}\"", folha);
    }

    [Fact]
    public async Task ProducoesDoDia_DuasNaMesmaMaquina_MarcaASobreposicao()
    {
        // A sobreposição grava com aviso (o proprietário, 16/10/2026: «Manter
        // o aviso») e a linha da máquina fica marcada a vermelho no dia.
        var (_, _, b1Id, _) = await SeedFichaAsync();
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOns.Add(new JobOn { ProductionNumber = "202618", Reference = "1075C142", MachineId = b1Id, InicioEm = hoje.AddDays(-1), FimEm = hoje.AddDays(-1) });
            await db.SaveChangesAsync();
        }
        SeedUser("4593", "Planejador", "provider-jobon-overlap", "JobOverlap1!");
        using var client = await LoginClientAsync("4593", "JobOverlap1!");

        var dia = hoje.AddDays(-1);
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={dia.Year}&mes={dia.Month}&dia={dia.Day}"));
        Assert.Contains("beta-light-machine-row mch-B1 overlap", html);
        Assert.Contains("Sobreposição: 2 produções na B1 neste dia.", html);
        Assert.DoesNotContain("mch-C3 overlap", html);

        var antes = hoje.AddDays(-3);
        var semSobreposicao = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={antes.Year}&mes={antes.Month}&dia={antes.Day}"));
        Assert.DoesNotContain("Sobreposição:", semSobreposicao);
        Assert.Contains("beta-light-machine-row mch-B1\"", semSobreposicao);
    }

    [Fact]
    public async Task ProducoesDoDia_MostramAProximaMudancaEmCadaMaquina()
    {
        // O proprietário, 17/10/2026: a linha mostra «referência produção»
        // sem a palavra «Produção» e, à frente, a próxima mudança: a produção
        // que entra a seguir ou a troca de um componente, a que vier primeiro.
        var (jobOnId, _, b1Id, c3Id) = await SeedFichaAsync();
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        int entraNaC3Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var c1 = new Machine { Code = "C1" };
            db.Machines.Add(c1);
            db.JobOns.Add(new JobOn { ProductionNumber = "202618", Reference = "6018Y232", MachineId = b1Id, InicioEm = hoje.AddDays(9) });
            db.JobOns.Add(new JobOn { ProductionNumber = "202617", Reference = "5447T173", MachineId = b1Id, InicioEm = hoje.AddDays(3) });
            db.JobOns.Add(new JobOn { ProductionNumber = "202619", Reference = "1199C194", Machine = c1, InicioEm = hoje.AddDays(5) });
            var entraNaC3 = new JobOn { ProductionNumber = "202620", Reference = "1075C142", MachineId = c3Id, InicioEm = hoje.AddDays(6) };
            db.JobOns.Add(entraNaC3);
            // A B1 troca a BQ antes da produção nova: aparece a troca. A C3
            // troca depois da produção nova: aparece a produção.
            var outraC3 = db.JobOns.Single(j => j.ProductionNumber == "202602").Id;
            db.JobOnLotNotes.Add(new JobOnLotNote { JobOnId = jobOnId, Dia = hoje.AddDays(2), Slot = "BQ", NewToolId = "BQ-TOOL-9", ToolReferenceFrozen = "T194", PreviousLotFrozen = "12", NewLotFrozen = "13", CreatedAtUtc = DateTime.UtcNow });
            db.JobOnLotNotes.Add(new JobOnLotNote { JobOnId = outraC3, Dia = hoje.AddDays(8), Slot = "CM", NewToolId = "T3", ToolReferenceFrozen = "5447", PreviousLotFrozen = "3", NewLotFrozen = "4", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            entraNaC3Id = entraNaC3.Id;
        }
        SeedUser("4596", "Planejador", "provider-jobon-proxima", "JobProxima1!");
        using var client = await LoginClientAsync("4596", "JobProxima1!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={hoje.Year}&mes={hoje.Month}&dia={hoje.Day}"));
        Assert.Contains("<span>1075C142 202601</span>", html);
        Assert.DoesNotContain("<span>Produção 202601</span>", html);
        var troca = hoje.AddDays(2).ToString("dd/MM/yyyy");
        // A mudança é um link (Diogo, 21/10/2026): a troca abre a ficha da
        // ferramenta que entra; a produção que entra abre o Job On no dia dela.
        Assert.Contains($"<a class=\"beta-light-next\" href=\"/Ferramentas/Ficha?tool_id=BQ-TOOL-9\" title=\"Troca BQ lote 12 para 13 a {troca}\">Mudança {troca} · BQ lote 12 para 13</a>", html);
        var diaC3 = hoje.AddDays(6);
        var entraC3 = diaC3.ToString("dd/MM/yyyy");
        Assert.Contains($"<a class=\"beta-light-next\" href=\"/JobOn?ano={diaC3.Year}&mes={diaC3.Month}&dia={diaC3.Day}&jobon_id={entraNaC3Id}#lightFicha\" title=\"Entra 1075C142 202620 na C3 a {entraC3}\">Mudança {entraC3} · 1075C142 202620</a>", html);
        // Na máquina vazia o link da mudança fica fora do link de criar.
        var entraC1 = hoje.AddDays(5).ToString("dd/MM/yyyy");
        Assert.Matches($"<strong>C1</strong> · —</a>\\s*<a class=\"beta-light-next\" href=\"/JobOn\\?[^\"]*jobon_id=\\d+#lightFicha\" title=\"Entra 1199C194 202619 na C1 a {entraC1}\">Mudança {entraC1} · 1199C194 202619</a>", html);
        // Os links ficam dentro da zona das produções do dia.
        var zonaDia = html.IndexOf("<div id=\"jobonProducoes\" data-dmo-regiao>", StringComparison.Ordinal);
        var folhas = html.IndexOf("<div id=\"jobonFolhas\" data-dmo-regiao>", StringComparison.Ordinal);
        Assert.True(zonaDia > 0 && folhas > zonaDia);
        Assert.True(html.IndexOf("<a class=\"beta-light-next\"", StringComparison.Ordinal) > zonaDia);
        Assert.True(html.LastIndexOf("<a class=\"beta-light-next\"", StringComparison.Ordinal) < folhas);
        // Na B1 a próxima mudança é a troca, não a produção que entra depois.
        // (A 5447T173 pode aparecer no «O que vai entrar», que mostra a semana:
        // de segunda a quinta, hoje + 3 dias ainda é esta semana.)
        Assert.DoesNotContain($"Mudança {hoje.AddDays(3):dd/MM/yyyy} · 5447T173 202617</a>", html);

        // Depois da troca, a B1 já mostra a produção que entra a seguir.
        var depois = hoje.AddDays(2);
        var noDia = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={depois.Year}&mes={depois.Month}&dia={depois.Day}"));
        Assert.Contains($"Mudança {hoje.AddDays(3):dd/MM/yyyy} · 5447T173 202617</a>", noDia);
        Assert.DoesNotContain("· BQ lote 12 para 13</a>", noDia);
    }

    [Fact]
    public async Task ProducoesDoDia_LinhaVermelha_SoComSobreposicaoReal()
    {
        // Diogo, 21/10/2026: «uma producao sai dia 20 outra entra dia 20» não
        // é sobreposição. A linha da máquina só fica vermelha quando duas
        // produções se cruzam de facto.
        await SeedFichaAsync();
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var troca = new Machine { Code = "D7" };
            var cruza = new Machine { Code = "D8" };
            var sozinha = new Machine { Code = "D9" };
            db.Machines.AddRange(troca, cruza, sozinha);
            // Na D7 uma sai hoje e a outra entra hoje: a troca, sem vermelho.
            db.JobOns.Add(new JobOn { ProductionNumber = "202671", Reference = "5447T173", Machine = troca, InicioEm = hoje.AddDays(-5), FimEm = hoje });
            db.JobOns.Add(new JobOn { ProductionNumber = "202672", Reference = "6018Y232", Machine = troca, InicioEm = hoje, FimEm = hoje.AddDays(5) });
            // Na D8 a segunda entrou no dia antes de a primeira sair: vermelho.
            db.JobOns.Add(new JobOn { ProductionNumber = "202681", Reference = "5447T173", Machine = cruza, InicioEm = hoje.AddDays(-5), FimEm = hoje });
            db.JobOns.Add(new JobOn { ProductionNumber = "202682", Reference = "6018Y232", Machine = cruza, InicioEm = hoje.AddDays(-1), FimEm = hoje.AddDays(5) });
            // Na D9 uma produção só: sem vermelho.
            db.JobOns.Add(new JobOn { ProductionNumber = "202691", Reference = "1199C194", Machine = sozinha, InicioEm = hoje.AddDays(-5), FimEm = hoje.AddDays(5) });
            await db.SaveChangesAsync();
        }
        SeedUser("4588", "Planejador", "provider-jobon-vermelho", "JobVermelho1!");
        using var client = await LoginClientAsync("4588", "JobVermelho1!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={hoje.Year}&mes={hoje.Month}&dia={hoje.Day}"));
        Assert.Contains("<div class=\"beta-light-machine-row mch-D7\">", html);
        Assert.Contains("<div class=\"beta-light-machine-row mch-D8 overlap\">", html);
        Assert.Contains("<div class=\"beta-light-machine-row mch-D9\">", html);
        Assert.Contains("Sobreposição: 2 produções na D8 neste dia.", html);
        Assert.DoesNotContain("produções na D7 neste dia", html);
        Assert.DoesNotContain("produções na D9 neste dia", html);
    }

    [Fact]
    public async Task Ficha_FerramentasUsadas_AbremAFichaDaFerramenta_SemAvisoDeDadosEmFalta()
    {
        // O Diogo, 17/10/2026: «nao temos forma de inserir informaçao tecnica
        // nas ferramentas». A referência abre a ficha da ferramenta na
        // informação técnica e volta a esta ficha. Sem aviso de dados em
        // falta: esse fica nos módulos que os usam (Peso, Pegamentos). Sem o
        // módulo Ferramentas não há link.
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ToolTechnicalContexts.Single(t => t.ToolId == "MF-TOOL-9").DiametroGargaloMm = null;
            db.SaveChanges();
        }
        SeedUser("4597", "Planejador", "provider-jobon-tecnica", "JobTecnica1!");
        using var semFerramentas = await LoginClientAsync("4597", "JobTecnica1!");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(semFerramentas, $"{PagePath}?jobon_id={jobOnId}"));
        // A zona mostra o MF sem gargalo e nenhum aviso de dados em falta (os
        // avisos da casa levam role="status" ou role="alert"). O aviso do
        // Armazém (não registada, sem estado; Diogo 21/10) é outro e sai da conta.
        var ferramentas = SemAvisoDoArmazem(FerramentasUsadas(html));
        Assert.Contains("Molde final 5810 · lote 1", ferramentas);
        Assert.DoesNotContain("role=\"status\"", ferramentas);
        Assert.DoesNotContain("role=\"alert\"", ferramentas);
        Assert.DoesNotContain("/Ferramentas/Ficha?tool_id=", html);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = db.Templates.Single(t => t.TemplateKey == "tpl-test-planejador");
            template.Capabilities = template.Capabilities.Append(DmoCapabilities.Ferramentas).ToArray();
            db.SaveChanges();
        }
        using var comFerramentas = await LoginClientAsync("4597", "JobTecnica1!");
        var comLink = WebUtility.HtmlDecode(await GetPageHtmlAsync(comFerramentas, $"{PagePath}?jobon_id={jobOnId}"));
        var fichaMf = $"/Ferramentas/Ficha?tool_id=MF-TOOL-9&tecnica=1&continuar=%2FJobOn%3Fjobon_id%3D{jobOnId}";
        Assert.Contains($"<a class=\"jobon-ferramenta-link\" href=\"{fichaMf}\"", comLink);
        Assert.DoesNotContain(">Completar</a>", comLink);
        Assert.Contains("/Ferramentas/Ficha?tool_id=T3&tecnica=1", comLink);
        var ferramentasComLink = SemAvisoDoArmazem(FerramentasUsadas(comLink));
        Assert.Contains($"href=\"{fichaMf}\"", ferramentasComLink);
        Assert.DoesNotContain("role=\"status\"", ferramentasComLink);
        Assert.DoesNotContain("role=\"alert\"", ferramentasComLink);
    }

    /// <summary>
    /// A zona «Ferramentas usadas» da ficha: a grelha a seguir ao título. Não
    /// tem id nem data-*, por isso encontra-se pelo título que o operador lê.
    /// </summary>
    private static string FerramentasUsadas(string html)
    {
        var titulo = html.IndexOf(">Ferramentas usadas</h3>", StringComparison.Ordinal);
        Assert.True(titulo >= 0, "A ficha não tem a zona «Ferramentas usadas».");
        return ElementoAteAoFecho(html, html.IndexOf("<div", titulo, StringComparison.Ordinal), "div");
    }

    /// <summary>O elemento que abre em <paramref name="inicio"/>, até ao seu fecho (conta os aninhados).</summary>
    private static string ElementoAteAoFecho(string html, int inicio, string tag)
    {
        var nivel = 0;
        foreach (Match m in Regex.Matches(html[inicio..], $"<{tag}\\b|</{tag}>"))
        {
            nivel += m.Value == $"</{tag}>" ? -1 : 1;
            if (nivel == 0)
            {
                return html.Substring(inicio, m.Index + m.Length);
            }
        }

        throw new InvalidOperationException($"Um <{tag}> da página ficou por fechar.");
    }

    [Fact]
    public async Task Ficha_AcoesEFactos_AoUsoDoHtml()
    {
        // A ficha server-rendered com as ações do dmo-html, o campo Calote
        // (o facto da produção), as FERRAMENTAS USADAS com os fatos
        // técnicos e os avisos informativos — nunca um bloqueio.
        var (jobOnId, controloId, _, c3Id) = await SeedFichaAsync();
        SeedUser("4501", "Planejador", "provider-ficha-1", "FichaPass1!");
        using var client = await LoginClientAsync("4501", "FichaPass1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");

        Assert.Contains("Ficha da produção", html);
        Assert.Contains("Ficha da produção", html); Assert.Contains(">1075C142</strong>", html); Assert.Contains(">202601</strong>", html);
        Assert.DoesNotContain("Calote desta produção (mm)", html); // «Preparar produção» só no Editar
        Assert.Contains(">9.5 mm</strong>", html);      // o facto da calote (2 casas pt-PT)

        // AS AÇÕES do job-sheet do dmo-html.
        Assert.Contains(">Editar</a>", html); Assert.DoesNotContain("Abrir Resumo", html); Assert.DoesNotContain("Abrir Peso", html); Assert.DoesNotContain("Abrir Pegamentos", html);
        Assert.Contains(">Duplicar Job On</a>", html);
        Assert.DoesNotContain(">Duplicar lote</a>", html);
        Assert.Contains(">Controlo</a>", html);
        
        
        // O BOTÃO [Controlo] é UM SÓ e abre o RESUMO do controlo desta
        // produção (a DECISÃO DO OWNER, verbatim: «abrir resumo, peso e
        // pegamentos é redundante, só precisa abrir resumo e depois ver peso e
        // pegamentos... já sabemos que vai abrir o resumo do controlo para este
        // jobon, e lá vemos o peso e pegamentos»): o controlo_id NÃO viaja no
        // URL da ficha — quem o procura é o Resumo. A afirmação anterior
        // (/Controlo/Peso?controlo_id=N) era a pré-história e contradizia o
        // DoesNotContain(«Abrir Peso») três linhas acima.
        Assert.Contains($"/Controlo/Resumo?jobon_id={jobOnId}", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"/Controlo/Peso?controlo_id={controloId}", html, StringComparison.Ordinal);

        // AS FERRAMENTAS USADAS (os contextos registados) com os fatos
        // técnicos que os controlos consomem.
        Assert.Contains("Ferramentas usadas</h3>", html);
        Assert.DoesNotContain("Histórico de alterações", html);
        Assert.DoesNotContain("Aplicável agora", html); Assert.DoesNotContain("Contextos registados", html);
        Assert.Contains("5447 · lote 3", html);
        
        Assert.Contains("NNPB", html);
        Assert.Contains("Punção <b", html);
        Assert.Contains("30 cm³", html);
        Assert.Contains("TP: D <b", html);
        Assert.Contains("24.11 mm", html);
        Assert.Contains("Marisa <b", html);
        Assert.Contains("10 cm³", html);
        Assert.Contains("800 peças", html);
        Assert.Contains("Gargalo nominal <b", html);

        // OS AVISOS INFORMATIVOS (nunca bloqueiam) só ao associar (o Diogo,
        // 17/10/2026: ao abrir um Job On pelo calendário não aparecem). A
        // produção 202602 (C3) também usa a CM: a ficha não avisa, a edição
        // sim. A CM 5447 está registada para B1 e C3 e a ficha está em B1
        // (compatível, sem aviso de compatibilidade).
        Assert.DoesNotContain("também está associado", html);
        Assert.DoesNotContain("Esta informação não impede a sua escolha", html);
        var edicao = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        Assert.Contains("também está associado a 202602 (C3)", edicao);
        Assert.Contains("Esta informação não impede a sua escolha", edicao);
        Assert.DoesNotContain("compatibilidade registada", edicao); // a ficha está numa máquina compatível
        Assert.Contains("TP Calote desta produção (mm)", edicao);
        // Os avisos ficam depois do «Mais detalhes» e sem margin:0 em linha:
        // ao aparecerem ou saírem não empurram nada (regra 56, B-jobon-10).
        Assert.True(Posicao(edicao, "id=\"lightToolWarnings\"")
            > Posicao(edicao, "<details class=\"jobon-mais-detalhes\">"));
        Assert.DoesNotContain("role=\"status\" style=\"margin:0\"", edicao);
        Assert.Contains("<span>TP Calote</span>", html);

        // Os ids fora das folhas (o tool_id nunca é facto da folha).
        Assert.DoesNotContain(">T3<", html);
        Assert.DoesNotContain(">BQ-TOOL-9<", html);
    }

    [Theory]
    [InlineData("8", "8")]
    [InlineData("2,5", "2.5")]
    [InlineData("3.25", "3.25")]
    [InlineData("0,125", "0.125")]
    [InlineData(" 2,5 ", "2.5")]
    [InlineData("0", "0")]
    public async Task CaloteEditavel_GravaPelaEdicao(string submitted, string expected)
    {
        // O campo Calote (mm) EDITÁVEL na ficha: a edição grava o valor
        // submetido (a migração aditiva da B1) — a calote do Job On usada
        // automaticamente pelo apoio TP/calote do Peso.
        var (jobOnId, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4502", "Planejador", "provider-ficha-2", "FichaPass2!");
        using var client = await LoginClientAsync("4502", "FichaPass2!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");

        var response = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()),
            ("machine_id", b1Id.ToString()),
            ("cm_tool_id", "T3"),
            ("mf_tool_id", "MF-TOOL-9"),
            ("bq_tool_id", "BQ-TOOL-9"),
            ("inicio_em", DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3).ToString("yyyy-MM-dd")),
            ("calote_mm", submitted),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), jobOn.CaloteMm);
        var reopened = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        Assert.Contains($"value=\"{expected}\"", reopened);
    }

    [Theory]
    [InlineData("2,5", "2.5")]
    [InlineData("3.25", "3.25")]
    [InlineData("0,125", "0.125")]
    [InlineData("0", "0")]
    public async Task CaloteNaCriacao_PreservaODecimalAoGuardarEReabrir(string submitted, string expected)
    {
        var (_, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4502", "Planejador", "provider-ficha-2", "FichaPass2!");
        using var client = await LoginClientAsync("4502", "FichaPass2!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");
        using var response = await client.PostAsync($"{PagePath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("production_number", "DECIMAL-TEST"), ("reference", "1075C142"),
            ("machine_id", b1Id.ToString()), ("cm_tool_id", "T3"),
            ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-9"),
            ("calote_mm", submitted), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // Guardar fica no Job On do Planeamento (Diogo, 22/10/2026), mesmo com o Controlo Criar.
        Assert.StartsWith($"{PagePath}?jobon_id=", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var job = await db.JobOns.AsNoTracking().SingleAsync(x => x.ProductionNumber == "DECIMAL-TEST");
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), job.CaloteMm);
        var reopened = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={job.Id}&editar=1");
        Assert.Contains($"value=\"{expected}\"", reopened);
    }

    /// <summary>
    /// A6 (decisão do proprietário, 16/10/2026: «tentar sempre warning,
    /// desde que não parta nada»): uma calote ambígua, ilegível ou negativa
    /// já não perde a edição. O Job On grava as outras alterações, MANTÉM a
    /// calote já gravada (9.5) e a folha gravada mostra o aviso amarelo com
    /// o valor ignorado e a calote mantida.
    /// </summary>
    [Theory]
    [InlineData("1.234,5")]
    [InlineData("1,234.5")]
    [InlineData("2,5,0")]
    [InlineData("abc")]
    [InlineData("-2")]
    public async Task CaloteIgnorada_NaEdicao_MantemACaloteAnteriorEAvisa(string submitted)
    {
        var (jobOnId, controloId, b1Id, _) = await SeedFichaAsync();
        SeedUser("4502", "Planejador", "provider-ficha-2", "FichaPass2!");
        using var client = await LoginClientAsync("4502", "FichaPass2!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");
        using var response = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()), ("machine_id", b1Id.ToString()),
            ("cm_tool_id", "T3"), ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-9"),
            ("inicio_em", "2026-05-04"), ("calote_mm", submitted),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.DoesNotContain("update_refusal", location, StringComparison.Ordinal);
        Assert.StartsWith($"{PagePath}?jobon_id={jobOnId}", location, StringComparison.Ordinal);
        Assert.Contains("calote_ignorada=", location, StringComparison.Ordinal);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains($"Job On guardado: «{submitted}» não é um número em mm igual ou superior a 0 e foi ignorado; mantém-se a calote anterior (9.5 mm).", html);
        // O resultado da gravação vai para o canto: não empurra a grelha (regra 53).
        Assert.Contains($"<div data-dmo-toast=\"aviso\" role=\"status\">Job On guardado: «{submitted}»", html);
        Assert.DoesNotContain("<div class=\"jobon-aviso warn\" role=\"status\">Job On guardado", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync(x => x.Id == jobOnId);
        Assert.Equal(9.5m, jobOn.CaloteMm);                    // a calote anterior não se perde
        Assert.Equal(new DateOnly(2026, 5, 4), jobOn.InicioEm); // o resto da edição gravou
        Assert.Equal(controloId, await db.Controlos.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync());
    }

    /// <summary>
    /// Diogo, 19/10/2026: «as datas têm de ser livres»; na beta.228 o fim
    /// antes do início era recusado. Agora a edição grava e a folha gravada
    /// avisa no canto porque a produção não aparece no calendário.
    /// </summary>
    [Fact]
    public async Task FimAntesDoInicio_NaEdicao_GravaEAvisa()
    {
        var (jobOnId, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4507", "Planejador", "provider-ficha-7", "FichaPass7!");
        using var client = await LoginClientAsync("4507", "FichaPass7!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");
        using var response = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()), ("machine_id", b1Id.ToString()),
            ("cm_tool_id", "T3"), ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-9"),
            ("inicio_em", "2026-05-04"), ("fim_em", "2026-05-03"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.DoesNotContain("update_refusal", location, StringComparison.Ordinal);
        Assert.StartsWith($"{PagePath}?jobon_id={jobOnId}", location, StringComparison.Ordinal);
        Assert.Contains("fim_antes_inicio=1", location, StringComparison.Ordinal);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains("<div data-dmo-toast=\"aviso\" role=\"status\">Job On guardado com o fim (03/05/2026) antes do início (04/05/2026). " +
            "Enquanto o fim for antes do início, a produção não aparece em nenhum dia do calendário. Para corrigir as datas, use Editar. " +
            $"<a class=\"dmo-aviso-link\" href=\"/JobOn?jobon_id={jobOnId}&editar=1#lightSheet\">Editar as datas</a></div>", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync(x => x.Id == jobOnId);
        Assert.Equal(new DateOnly(2026, 5, 4), jobOn.InicioEm);
        Assert.Equal(new DateOnly(2026, 5, 3), jobOn.FimEm);

        // Com as datas já certas, o mesmo link não avisa.
        jobOn = await db.JobOns.SingleAsync(x => x.Id == jobOnId);
        jobOn.FimEm = new DateOnly(2026, 5, 5);
        await db.SaveChangesAsync();
        var fixedHtml = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.DoesNotContain("antes do início", fixedHtml);
    }

    /// <summary>
    /// O mesmo no Criar (Diogo, 19/10/2026: datas livres, só avisos): um dia
    /// antes do início, o Job On grava e a folha gravada avisa no canto.
    /// </summary>
    [Fact]
    public async Task FimUmDiaAntesDoInicioNaCriacao_GravaEAvisa()
    {
        var (_, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4508", "Planejador", "provider-ficha-8", "FichaPass8!");
        using var client = await LoginClientAsync("4508", "FichaPass8!");

        using var response = await CriarPeloFormularioAsync(client, b1Id, "FIM-ANTES", "2026-05-04", "2026-05-03");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var job = await JobOnGravadoAsync("FIM-ANTES");
        Assert.Equal(new DateOnly(2026, 5, 4), job.InicioEm);
        Assert.Equal(new DateOnly(2026, 5, 3), job.FimEm);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith($"{PagePath}?jobon_id={job.Id}", location, StringComparison.Ordinal);
        Assert.Contains("fim_antes_inicio=1", location, StringComparison.Ordinal);

        var avisos = AvisosNoCanto(await GetPageHtmlAsync(client, location));
        Assert.Contains(avisos, aviso => aviso.StartsWith(
            "Job On guardado com o fim (03/05/2026) antes do início (04/05/2026).", StringComparison.Ordinal));
    }

    /// <summary>
    /// No mesmo dia do início ou um dia depois, as datas estão certas: grava
    /// sem aviso e o Guardar fica no Job On do Planeamento, mesmo com o
    /// Controlo Criar no template (Diogo, 22/10/2026: sem ir ao Controlo).
    /// </summary>
    [Theory]
    [InlineData("2026-05-04")] // no mesmo dia
    [InlineData("2026-05-05")] // um dia depois
    public async Task FimNoDiaDoInicioOuDepoisNaCriacao_GravaEFicaNoJobOnSemAviso(string fim)
    {
        var (_, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4509", "Planejador", "provider-ficha-9", "FichaPass9!");
        using var client = await LoginClientAsync("4509", "FichaPass9!");

        using var response = await CriarPeloFormularioAsync(client, b1Id, "FIM-CERTO", "2026-05-04", fim);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var job = await JobOnGravadoAsync("FIM-CERTO");
        Assert.Equal(new DateOnly(2026, 5, 4), job.InicioEm);
        Assert.Equal(DateOnly.Parse(fim, System.Globalization.CultureInfo.InvariantCulture), job.FimEm);
        Assert.Equal($"{PagePath}?jobon_id={job.Id}", response.Headers.Location!.OriginalString);
    }

    /// <summary>
    /// Cria pelo formulário que a página desenha: a folha nova abre com a
    /// produção, a máquina, as ferramentas e o início, e o operador escreve o
    /// fim e carrega em Guardar.
    /// </summary>
    private async Task<HttpResponseMessage> CriarPeloFormularioAsync(
        HttpClient client, int machineId, string producao, string inicio, string fim)
    {
        var folha = await GetPageHtmlAsync(client,
            $"{PagePath}?novo=1&reference=1075C142&production_number={producao}&machine_id={machineId}" +
            $"&cm_tool_id=T3&mf_tool_id=MF-TOOL-9&bq_tool_id=BQ-TOOL-9&inicio_em={inicio}");
        var (acao, campos) = EnvioDoFormulario.Mudar(folha, "lightEnd", fim);
        Assert.Equal($"{PagePath}?handler=Create", acao);
        return await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
    }

    private async Task<JobOn> JobOnGravadoAsync(string producao)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == producao);
    }

    /// <summary>
    /// O texto dos avisos do canto (data-dmo-toast="aviso") na zona dos
    /// avisos do Job On, como o operador o lê. Falha se a zona não existir.
    /// </summary>
    private static IReadOnlyList<string> AvisosNoCanto(string html) =>
        Regex.Matches(Controlo.PesoZonas.Zona(html, "jobonAvisos"),
                "<div\\b[^>]*\\bdata-dmo-toast=\"aviso\"[^>]*>(?<texto>.*?)</div>", RegexOptions.Singleline)
            .Select(m => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(m.Groups["texto"].Value, "<[^>]+>", " ")), "\\s+", " ").Trim())
            .ToList();

    [Fact]
    public async Task CaloteIgnorada_NaEdicaoSemCaloteAnterior_FicaSemCaloteEAvisa()
    {
        var (jobOnId, _, b1Id, _) = await SeedFichaAsync();
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var stored = await seedDb.JobOns.SingleAsync(x => x.Id == jobOnId);
            stored.CaloteMm = null;
            await seedDb.SaveChangesAsync();
        }

        SeedUser("4506", "Planejador", "provider-ficha-6", "FichaPass6!");
        using var client = await LoginClientAsync("4506", "FichaPass6!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");
        using var response = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()), ("machine_id", b1Id.ToString()),
            ("cm_tool_id", "T3"), ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-9"),
            ("calote_mm", "abc"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith($"{PagePath}?jobon_id={jobOnId}", location, StringComparison.Ordinal);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains("Job On guardado sem calote: «abc»", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Null((await db.JobOns.AsNoTracking().SingleAsync(x => x.Id == jobOnId)).CaloteMm);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-2")]
    public async Task CaloteIgnorada_NaCriacao_GravaOJobOnSemCaloteEAvisa(string submitted)
    {
        var (_, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4503", "Planejador", "provider-ficha-3", "FichaPass3!");
        using var client = await LoginClientAsync("4503", "FichaPass3!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");

        var response = await client.PostAsync($"{PagePath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("production_number", "CALOTE-IGNORADA"), ("reference", "1075C142"),
            ("machine_id", b1Id.ToString()), ("cm_tool_id", "T3"),
            ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-9"),
            ("calote_mm", submitted), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.DoesNotContain("create_refusal", location, StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var job = await db.JobOns.AsNoTracking().SingleAsync(x => x.ProductionNumber == "CALOTE-IGNORADA");
            Assert.Null(job.CaloteMm);
            Assert.StartsWith($"{PagePath}?jobon_id={job.Id}", location, StringComparison.Ordinal);
        }

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains($"Job On guardado sem calote: «{submitted}»", html);
        Assert.Contains("CALOTE-IGNORADA", html); // a ficha da produção gravada
    }

    [Fact]
    public async Task DuplicarEsteJobOn_CopiaOsDadosParaUmaProducaoNova()
    {
        // O html: o duplicar copia o jobon INTEIRO para uma produção nova
        // (o nº e as datas por preencher, as tools e a calote copiadas) —
        // mudar datas/máquina sem voltar a criar.
        var (jobOnId, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4504", "Planejador", "provider-ficha-4", "FichaPass4!");
        using var client = await LoginClientAsync("4504", "FichaPass4!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&duplicar=1");

        // A ficha de origem fica de fora (o modo duplicar abre a folha em
        // criação); a folha desenhada pelo servidor leva os dados copiados.
        Assert.DoesNotContain("Ficha da produção", html);
        Assert.Contains("name=\"reference\" required placeholder=\"Ex.: 0109S022\" value=\"1075C142\"", html);
        Assert.Contains("<option value=\"T3\" selected=\"selected\">", html);
        Assert.Contains("name=\"calote_mm\" type=\"number\" lang=\"en\" min=\"0\" step=\"any\" placeholder=\"Usar referência da Tool\" value=\"9.5\"", html);
        Assert.DoesNotContain("value=\"202601\"", html); // o nº é NOVO (por preencher)
    }

    [Fact]
    public async Task ToolFactsEndpoint_FatosEAvisos_DoBackend()
    {
        // O preview ao vivo é ENDPOINT READ-ONLY DO BACKEND (o padrão da
        // B1): os fatos técnicos + os avisos — o browser nunca calcula
        // regra de domínio.
        var (jobOnId, _, b1Id, c3Id) = await SeedFichaAsync();
        SeedUser("4505", "Planejador", "provider-ficha-5", "FichaPass5!");
        using var client = await LoginClientAsync("4505", "FichaPass5!");

        // As datas da folha (o início há 3 dias, sem fim) cruzam as da 202602.
        var inicio = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3).ToString("yyyy-MM-dd");
        var response = await client.GetAsync(
            $"{PagePath}?handler=ToolFacts&cm_tool_id=T3&mf_tool_id=MF-TOOL-9&bq_tool_id=BQ-TOOL-9&machine_id={b1Id}&jobon_id={jobOnId}&inicio_em={inicio}&fim_em=");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(3, root.GetArrayLength());

        var cm = root.EnumerateArray().First(f => f.GetProperty("toolId").GetString() == "T3");
        Assert.Equal("CM", cm.GetProperty("type").GetString());
        Assert.Equal("NNPB", cm.GetProperty("processo").GetString());
        Assert.Equal(30m, cm.GetProperty("volumePuncaoCm3").GetDecimal());
        Assert.Equal(50m, cm.GetProperty("tpDiametroMm").GetDecimal());

        // A CM está registada para B1 e C3: em B1 não há aviso de
        // compatibilidade; o aviso vem do USO PARALELO (202602 noutra
        // máquina, a C3, com as datas a cruzar).
        Assert.Null(cm.TryGetProperty("avisoCompatibilidade", out var compat) && compat.ValueKind != JsonValueKind.Null
            ? compat.GetString()
            : null);
        var usoParalelo = cm.GetProperty("avisoUsoParalelo").GetString();
        Assert.NotNull(usoParalelo);
        Assert.Contains("também está associado a 202602 (C3)", usoParalelo);
        Assert.Contains("Esta informação não impede a sua escolha", usoParalelo);

        // A própria produção exclui-se (o jobon_id pedido).
        Assert.DoesNotContain("202601", usoParalelo);
    }

    [Fact]
    public async Task ToolFactsEndpoint_AvisosLevamLinkParaOndeSeResolvem()
    {
        // O Diogo (19/10/2026): todos os avisos têm um link para a página
        // onde se resolvem, se possível. A compatibilidade leva à ficha da
        // ferramenta (só a quem tem Ferramentas); o uso paralelo, à outra
        // produção. Noutro separador: a folha pode estar por gravar.
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        int b2Id;
        int outraId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            // Sem o módulo Ferramentas, seja qual for a ordem dos testes.
            var planejador = db.Templates.SingleOrDefault(t => t.TemplateKey == "tpl-test-planejador");
            if (planejador is not null)
            {
                planejador.Capabilities = planejador.Capabilities.Where(c => c != DmoCapabilities.Ferramentas).ToArray();
            }

            var b2 = new Machine { Code = "B2" };
            db.Machines.Add(b2);
            db.SaveChanges();
            b2Id = b2.Id;
            outraId = db.JobOns.Single(j => j.ProductionNumber == "202602").Id;
        }

        SeedUser("4521", "Planejador", "provider-ficha-21", "FichaPass21!");
        var inicio = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3).ToString("yyyy-MM-dd");
        var query = $"{PagePath}?handler=ToolFacts&cm_tool_id=T3&machine_id={b2Id}&jobon_id={jobOnId}&inicio_em={inicio}&fim_em=";

        async Task<JsonElement> AvisosAsync(HttpClient client)
        {
            var response = await client.GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement[0].GetProperty("avisos").Clone();
        }

        using (var semFerramentas = await LoginClientAsync("4521", "FichaPass21!"))
        {
            var avisos = await AvisosAsync(semFerramentas);
            // O terceiro é o do Armazém (Diogo 21/10, FER-E-03), sem link a quem não tem o Armazém.
            Assert.Equal(3, avisos.GetArrayLength());
            Assert.Contains("não está registada no Armazém e não tem estado", avisos[2].GetProperty("texto").GetString());
            Assert.Equal(0, avisos[2].GetProperty("links").GetArrayLength());
            Assert.Contains("compatibilidade registada", avisos[0].GetProperty("texto").GetString());
            Assert.Equal(0, avisos[0].GetProperty("links").GetArrayLength());
            Assert.Contains("202602 (C3)", avisos[1].GetProperty("texto").GetString());
            var paralelo = Assert.Single(avisos[1].GetProperty("links").EnumerateArray());
            Assert.Equal("Abrir 202602 (C3)", paralelo.GetProperty("texto").GetString());
            Assert.Equal($"/JobOn?jobon_id={outraId}", paralelo.GetProperty("href").GetString());
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = db.Templates.Single(t => t.TemplateKey == "tpl-test-planejador");
            template.Capabilities = template.Capabilities.Append(DmoCapabilities.Ferramentas).ToArray();
            db.SaveChanges();
        }

        using var comFerramentas = await LoginClientAsync("4521", "FichaPass21!");
        var compat = Assert.Single((await AvisosAsync(comFerramentas))[0].GetProperty("links").EnumerateArray());
        Assert.Equal("Abrir a ficha da CM", compat.GetProperty("texto").GetString());
        Assert.Equal("/Ferramentas/Ficha?tool_id=T3", compat.GetProperty("href").GetString());

        // A folha desenhada pelo servidor (o Editar) leva os mesmos links.
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(comFerramentas, $"{PagePath}?jobon_id={jobOnId}&editar=1"));
        Assert.Contains($"<a class=\"dmo-aviso-link\" href=\"/JobOn?jobon_id={outraId}\" target=\"_blank\" rel=\"noopener\">Abrir 202602 (C3)</a>", html);
    }

    [Fact]
    public async Task ToolFactsEndpoint_DatasQueNaoSeCruzam_SemAvisoDeUsoParalelo()
    {
        // A mesma CM pode servir outra produção; só não pode trabalhar ao
        // mesmo tempo (o proprietário, 17/10/2026). A 202602 começou ontem:
        // uma produção que terminou anteontem não avisa, e sem datas também
        // não há nada a comparar.
        var (jobOnId, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4515", "Planejador", "provider-ficha-15", "FichaPass15!");
        using var client = await LoginClientAsync("4515", "FichaPass15!");
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);

        async Task<string?> AvisoAsync(string datas)
        {
            var response = await client.GetAsync(
                $"{PagePath}?handler=ToolFacts&cm_tool_id=T3&machine_id={b1Id}&jobon_id={jobOnId}{datas}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var cm = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement[0];
            return cm.TryGetProperty("avisoUsoParalelo", out var aviso) && aviso.ValueKind != JsonValueKind.Null
                ? aviso.GetString()
                : null;
        }

        Assert.Null(await AvisoAsync($"&inicio_em={hoje.AddDays(-5):yyyy-MM-dd}&fim_em={hoje.AddDays(-2):yyyy-MM-dd}"));
        Assert.Null(await AvisoAsync("&inicio_em=&fim_em="));
        // O dia da troca (acaba no dia em que a 202602 começou) não conta (o Diogo, 22/10/2026).
        Assert.Null(await AvisoAsync($"&inicio_em={hoje.AddDays(-5):yyyy-MM-dd}&fim_em={hoje.AddDays(-1):yyyy-MM-dd}"));
        Assert.Contains("202602 (C3)", await AvisoAsync($"&inicio_em={hoje.AddDays(-5):yyyy-MM-dd}&fim_em={hoje:yyyy-MM-dd}"));
    }

    [Fact]
    public async Task CartoesDasFerramentas_ONomeEAbrirFicha_LevamAFichaDaFerramenta()
    {
        // Diogo, 21/10/2026: «nao consigo aceder as ferramentas a partir do
        // jobon, apenas tem trocar remover criar. ao clicar na ferramenta
        // deveria ir para a pagina da mesma».
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        SeedUser("4587", "Planejador", "provider-ficha-abrir", "FichaAbrir1!");
        using var client = await LoginClientAsync("4587", "FichaAbrir1!");

        var editar = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        foreach (var (tipo, toolId, titulo) in new[] { ("CM", "T3", "5447 · Lote 3"), ("BQ", "BQ-TOOL-9", "T194 · Lote 12"), ("MF", "MF-TOOL-9", "5810 · Lote 1") })
        {
            Assert.Contains($"<a class=\"dmo-options-item\" data-tool-open href=\"/Ferramentas/Ficha?tool_id={toolId}\">Abrir ficha {tipo}</a>", editar);
            Assert.Contains($"<a class=\"job-tool-card-link\" data-tool-open href=\"/Ferramentas/Ficha?tool_id={toolId}\" title=\"Abrir a ficha da ferramenta\"><strong class=\"job-tool-card-title\" data-tool-title>{titulo}</strong></a>", editar);
        }

        // Sem ferramenta escolhida não há ficha para abrir.
        var novo = await GetPageHtmlAsync(client, $"{PagePath}?novo=1");
        Assert.Contains("<a class=\"dmo-options-item\" data-tool-open hidden=\"hidden\">Abrir ficha CM</a>", novo);
    }

    [Fact]
    public async Task CartoesDasFerramentas_AssociarOuCriar_EEscolhidaMostraSoAInformacao()
    {
        // O Diogo (17/10/2026): carregar em CM abre «Associar» ou «Criar»;
        // escolhida, o cartão mostra a referência, o lote e os dados
        // técnicos, sem as máquinas (essas só na procura).
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        SeedUser("4516", "Planejador", "provider-ficha-16", "FichaPass16!");
        using var client = await LoginClientAsync("4516", "FichaPass16!");

        var editar = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        Assert.Contains("<section class=\"job-tool-card\" data-tool-card=\"CM\">", editar);
        Assert.Contains("<strong class=\"job-tool-card-title\" data-tool-title>5447 · Lote 3</strong>", editar);
        Assert.Contains("<strong class=\"job-tool-card-title\" data-tool-title>T194 · Lote 12</strong>", editar);
        Assert.Contains(">Trocar CM</button>", editar);
        Assert.Contains(">Criar CM</a>", editar);
        Assert.DoesNotContain("+ Criar", editar);

        var novo = await GetPageHtmlAsync(client, $"{PagePath}?novo=1");
        Assert.Contains(">Associar CM</button>", novo);
        Assert.Contains(">Associar ou criar</span>", novo);
        Assert.Contains("<div class=\"job-tool-chosen\" data-tool-chosen hidden>", novo);
        Assert.Contains("data-tool-clear hidden>Retirar CM</button>", novo);
    }

    [Fact]
    public async Task CartoesDasFerramentas_RetirarSoNoNovoJobOn_ENuncaNoMf()
    {
        // Deep sweep, 18/10/2026, A1-M3: no Editar, «Retirar CM/BQ» gravava
        // com sucesso e não retirava nada (o slot vazio é «não mexer» no
        // handler), e «Retirar MF» era sempre recusado (o MF é obrigatório).
        // O Editar não oferece «Retirar»; o Novo Job On oferece-o na CM e na
        // BQ, nunca no MF (lá, sem MF, a criação também é sempre recusada).
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        SeedUser("4517", "Planejador", "provider-ficha-17", "FichaPass17!");
        using var client = await LoginClientAsync("4517", "FichaPass17!");

        var editar = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        foreach (var tipo in new[] { "CM", "MF", "BQ" })
        {
            Assert.Contains($">Trocar {tipo}</button>", editar);
            Assert.Contains($">Criar {tipo}</a>", editar);
        }
        Assert.DoesNotContain("data-tool-clear", editar);

        var novo = await GetPageHtmlAsync(client,
            $"{PagePath}?novo=1&cm_tool_id=T3&mf_tool_id=MF-TOOL-9&bq_tool_id=BQ-TOOL-9");
        Assert.Contains(">Trocar MF</button>", novo);
        Assert.Matches("data-tool-clear\\s*>Retirar CM</button>", novo);
        Assert.Matches("data-tool-clear\\s*>Retirar BQ</button>", novo);
        Assert.DoesNotContain(">Retirar MF</button>", novo);
        Assert.Equal(2, Regex.Matches(novo, "data-tool-clear").Count);
    }

    [Fact]
    public async Task Ficha_SemColocacaoManualNemRetirarDoPlaneamento()
    {
        // A decisão do Owner 14/10: a colocação em máquina segue AS DATAS
        // do Job On — a ficha NUNCA traz a colocação manual nem o retirar
        // do planeamento.
        var (jobOnId, _, b1Id, _) = await SeedFichaAsync();
        SeedUser("4506", "Planejador", "provider-ficha-6", "FichaPass6!");
        using var client = await LoginClientAsync("4506", "FichaPass6!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");

        // A ficha (#lightFicha) está desenhada, com as ações nas Opções...
        var marca = html.IndexOf("id=\"lightFicha\"", StringComparison.Ordinal);
        Assert.True(marca >= 0, "A ficha da produção não aparece.");
        var ficha = ElementoAteAoFecho(html, html.LastIndexOf("<section", marca, StringComparison.Ordinal), "section");
        Assert.Contains("202601", ficha);
        Assert.Contains(">Editar</a>", ficha);
        // ...e nenhuma grava a partir dela: colocar, substituir ou retirar
        // seria um envio daqui, e a ficha não tem formulário nem botão de envio.
        Assert.DoesNotContain("<form", ficha);
        Assert.DoesNotContain("<button", ficha);
        Assert.DoesNotContain("type=\"submit\"", ficha);
        Assert.DoesNotContain("Colocar na máquina", ficha);
        Assert.DoesNotContain("Substituir na máquina", ficha);
        Assert.DoesNotContain("Retirar do planeamento", ficha);
    }
    [Fact]
    public async Task Editar_RegistaOQueMudou_EAFichaMostraAAcao()
    {
        // O Job On apresenta o problema e leva à solução (o proprietário,
        // 17/10/2026): a edição grava o que mudou, a ficha ganha a página
        // «O que mudou» com a ação de cada linha e a linha do dia leva a marca.
        var (jobOnId, _, machineId, _) = await SeedFichaAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "BQ-TOOL-13", Type = ToolType.Bq, Reference = "T194", Lot = "13" });
            await db.SaveChangesAsync();
        }
        SeedUser("4593", "Planejador", "provider-jobon-mudou", "JobMudou1!");
        using var client = await LoginClientAsync("4593", "JobMudou1!");

        var antes = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&vista=alteracoes"));
        Assert.DoesNotContain("jobon-mudancas", antes);
        // Menos botões (o Diogo, 17/10/2026): sem separadores; à vista só o
        // Editar, e o Controlo passa para as Opções.
        Assert.DoesNotContain("jobon-ficha-tabs", antes);
        Assert.Contains("<a class=\"dmo-options-item\" href=\"/Controlo/Resumo?jobon_id=" + jobOnId + "\">Controlo</a>", antes);

        var inicio = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3);
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        using var saved = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()), ("machine_id", machineId.ToString()),
            ("cm_tool_id", "T3"), ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-13"),
            ("inicio_em", inicio.ToString("yyyy-MM-dd")), ("fim_em", ""), ("calote_mm", "9.5"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}"));
        Assert.Contains("<details class=\"jobon-mudancas\" id=\"fichaMudancas\">", ficha);
        Assert.Contains("O que mudou desde o início (1 alteração)", ficha);

        var resumo = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&vista=alteracoes"));
        Assert.Contains("<details class=\"jobon-mudancas\" id=\"fichaMudancas\" open=\"open\">", resumo);
        Assert.Contains("<strong>Mudou a BQ: T194 · lote 12 → T194 · lote 13</strong>", resumo);
        Assert.Contains($"<a class=\"dmo-button\" href=\"/Controlo/Resumo?jobon_id={jobOnId}\">Rever o Controlo</a>", resumo);
        Assert.DoesNotContain("Mudou a CM", resumo);

        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var dia = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={hoje.Year}&mes={hoje.Month}&dia={hoje.Day}"));
        // A barra da máquina diz qual foi a alteração (o Diogo, 17/10/2026).
        Assert.Contains("<b class=\"beta-light-changes\">Mudança BQ · Sai 12 entra 13</b>", dia);
        Assert.Contains("Mudou a BQ: T194 · lote 12 → T194 · lote 13\"", dia);

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var change = await finalDb.JobOnChanges.SingleAsync(c => c.JobOnId == jobOnId);
        Assert.Equal("BQ", change.Field);
        Assert.False(string.IsNullOrWhiteSpace(change.ChangedByNameFrozen));
    }

    [Fact]
    public async Task MudarACm_VerificarOPeso_AbreOPesoDaProducao()
    {
        // O «Verificar o Peso» de «O que mudou» levava a /Controlo/Peso?jobon_id=N
        // e a folha, sem controlo_id, abria o Novo Peso sem Job On
        // (18/10/2026). Tem de abrir o Peso desta produção.
        var (jobOnId, controloId, machineId, _) = await SeedFichaAsync();
        int pesoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
            var peso = new DMO.Alpha.Core.Controlo.Peso
            {
                CmToolId = "T3", BqToolId = "BQ-TOOL-9", Process = "NNPB", WaterTemperatureC = 24,
                VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.5m,
                ControloId = controloId
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            pesoId = peso.Id;
        }
        SeedUser("4597", "Planejador", "provider-jobon-verificar-peso", "JobPeso1!");
        using var client = await LoginClientAsync("4597", "JobPeso1!");

        var inicio = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3);
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        using var saved = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()), ("machine_id", machineId.ToString()),
            ("cm_tool_id", "T4"), ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-9"),
            ("inicio_em", inicio.ToString("yyyy-MM-dd")), ("fim_em", ""), ("calote_mm", "9.5"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&vista=alteracoes"));
        Assert.Contains("<strong>Mudou a CM: 5447 · lote 3 → 5447 · lote 4</strong>", ficha);
        var link = Regex.Match(ficha, "<a class=\"dmo-button\" href=\"([^\"]+)\">Verificar o Peso</a>");
        Assert.True(link.Success);

        // O operador segue o link (e os redirects) até à folha.
        var url = link.Groups[1].Value;
        HttpResponseMessage response;
        for (var saltos = 0; ; saltos++)
        {
            response = await client.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.Redirect || saltos == 3) break;
            url = response.Headers.Location!.OriginalString;
            response.Dispose();
        }
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var peso = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("<h3 id=\"peso-production-title\">Produção 202601</h3>", peso);
            // Trocar a CM pede um Peso novo (resposta R15 do especialista;
            // 18/10/2026): a folha desta produção propõe o Peso da CM nova,
            // diz porquê, e o Peso da CM anterior fica nos Pesos da produção.
            Assert.Contains("Novo Peso da produção", peso);
            Assert.Contains("A CM mudou nesta produção; o Peso anterior ficou como estava.", peso);
            Assert.Contains($"data-dmo-open=\"/Controlo/Peso?controlo_id={controloId}&peso_id={pesoId}\"", peso);
            Assert.Contains($"<input type=\"hidden\" name=\"controlo_id\" value=\"{controloId}\" />", peso);
        }
    }

    [Fact]
    public async Task OQueMudou_SoAsAlteracoesFeitasDepoisDoInicio()
    {
        // O histórico só conta o que mudou depois de a produção começar (o
        // Diogo, 17/10/2026). A produção começou há 3 dias: a alteração de há
        // 5 dias fica fora da ficha e da marca da linha do dia.
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOnId, Field = JobOnChangeFields.Calote,
                OldValueFrozen = "8 mm", NewValueFrozen = "9.5 mm",
                ChangedByNameFrozen = "Antes", ChangedAtUtc = DateTime.UtcNow.AddDays(-5)
            });
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOnId, Field = JobOnChangeFields.End,
                OldValueFrozen = null, NewValueFrozen = "31/12/2026",
                ChangedByNameFrozen = "Depois", ChangedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SeedUser("4596", "Planejador", "provider-jobon-inicio", "JobInicio1!");
        using var client = await LoginClientAsync("4596", "JobInicio1!");

        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&vista=alteracoes"));
        Assert.Contains("O que mudou desde o início (1 alteração)", ficha);
        Assert.Contains("Mudou o fim: — → 31/12/2026", ficha);
        Assert.DoesNotContain("Mudou a calote", ficha);

        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var dia = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?ano={hoje.Year}&mes={hoje.Month}&dia={hoje.Day}"));
        Assert.Contains("<b class=\"beta-light-changes\">Mudança Fim · Sai — entra 31/12/2026</b>", dia);
        Assert.DoesNotContain("Calote · Sai", dia);

        // Só com alterações de antes do início, a ficha não mostra o resumo.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnChanges.RemoveRange(db.JobOnChanges.Where(c => c.JobOnId == jobOnId && c.Field == JobOnChangeFields.End));
            await db.SaveChangesAsync();
        }
        var semAlteracoes = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");
        Assert.DoesNotContain("jobon-mudancas", semAlteracoes);
        Assert.DoesNotContain("beta-light-changes", await GetPageHtmlAsync(client, $"{PagePath}?ano={hoje.Year}&mes={hoje.Month}&dia={hoje.Day}"));
    }

    [Fact]
    public async Task Editar_DepoisDaAprovacao_OControloAvisaEMantemAAprovacao()
    {
        // O aviso nos módulos (o proprietário, 17/10/2026): o Job On mudou
        // depois da aprovação; o Controlo avisa e continua aprovado.
        var (jobOnId, _, machineId, _) = await SeedFichaAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "BQ-TOOL-14", Type = ToolType.Bq, Reference = "T194", Lot = "14" });
            await db.SaveChangesAsync();
        }
        SeedUser("4594", "Planejador", "provider-jobon-aviso", "JobAviso1!");
        using var client = await LoginClientAsync("4594", "JobAviso1!");

        var resumoPath = $"/Controlo/Resumo?jobon_id={jobOnId}";
        var antes = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, resumoPath));
        Assert.DoesNotContain("jobon-change-notice", antes);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.SingleAsync(c => c.JobOnId == jobOnId);
            controlo.ApprovedByOperatorId = "4594";
            controlo.ApprovedAtUtc = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        var inicio = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3);
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        using var saved = await client.PostAsync($"{PagePath}?handler=Update", HtmlFormHelpers.BuildFormPost(
            ("jobon_edit_id", jobOnId.ToString()), ("machine_id", machineId.ToString()),
            ("cm_tool_id", "T3"), ("mf_tool_id", "MF-TOOL-9"), ("bq_tool_id", "BQ-TOOL-14"),
            ("inicio_em", inicio.ToString("yyyy-MM-dd")), ("fim_em", ""), ("calote_mm", "9.5"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, resumoPath));
        Assert.Contains("<strong>O Job On mudou depois da aprovação. A aprovação mantém-se.</strong>", depois);
        Assert.Contains("Mudou a BQ: T194 · lote 12 → T194 · lote 14", depois);
        Assert.Contains($"href=\"/JobOn?jobon_id={jobOnId}&vista=alteracoes#fichaMudancas\">Ver o que mudou</a>", depois);
        Assert.Contains("<strong>Aprovado</strong>", depois);
    }

    [Fact]
    public void Resumo_SemOModulo_ALinhaFicaSoComoInformacao()
    {
        // O botão da ação só a quem tem o módulo (o Diogo, 17/10/2026).
        var linhas = DMO.Alpha.Web.Presentation.JobOnSummaryEntry.Compose(7, new DateOnly(2026, 1, 1),
            new[]
            {
                new DMO.Alpha.Core.Production.JobOnChangeView(7, "BQ", "Mudou a BQ: a → b", "Diogo", DateTime.UtcNow),
                new DMO.Alpha.Core.Production.JobOnChangeView(7, "Maquina", "Mudou a máquina: B1 → B2", "Diogo", DateTime.UtcNow)
            },
            Array.Empty<DMO.Alpha.Core.Production.LotNoteView>());

        var soBoquilhas = DMO.Alpha.Web.Presentation.JobOnSummaryEntry.ForCapabilities(linhas, c => c == DmoCapabilities.Boquilhas);

        Assert.Null(soBoquilhas.Single(l => l.Text.StartsWith("Mudou a BQ")).ActionHref);
        Assert.Equal("/Boquilhas", soBoquilhas.Single(l => l.Text.StartsWith("Mudou a máquina")).ActionHref);
    }

    [Fact]
    public async Task Ficha_FerramentasUsadas_MostramOUsoDaUltimaLeitura()
    {
        // Diogo, 21/10/2026: «no Job On também mostrar o % uso».
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        SeedUser("4599", "Planejador", "provider-jobon-uso", "JobUso1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.AddRange(
                new DMO.Alpha.Core.Tools.FerramentaUtilizacao { ToolId = "T3", Percentagem = 30m, RegistadaEmUtc = DateTime.UtcNow.AddDays(-5) },
                new DMO.Alpha.Core.Tools.FerramentaUtilizacao { ToolId = "T3", Percentagem = 33.5m, RegistadaEmUtc = DateTime.UtcNow });
            db.SaveChanges();
        }

        try
        {
            using var client = await LoginClientAsync("4599", "JobUso1!");
            var ferramentas = WebUtility.HtmlDecode(FerramentasUsadas(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}")));

            // Só a CM tem leitura: um só «Uso», com a mais recente.
            var usos = System.Text.RegularExpressions.Regex.Matches(ferramentas, "<p[^>]*data-dmo-uso[^>]*>Uso <b[^>]*>([^<]*)</b></p>");
            Assert.Equal("33.5 %", Assert.Single(usos).Groups[1].Value);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.RemoveRange(db.FerramentaUtilizacoes.Where(u => u.ToolId == "T3"));
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Ficha_FerramentasUsadas_OndeEstaNoArmazem_EAvisoSemRegisto()
    {
        // Diogo 21/10 (FER-E-03): a ferramenta não registada no Armazém ou
        // sem estado só avisa, com o link para os movimentos dela.
        var (jobOnId, _, _, _) = await SeedFichaAsync();
        SeedUser("4598", "Planejador", "provider-jobon-armazem", "JobArmazem1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var posicao = new DMO.Alpha.Core.Armazem.ArmazemPosicao { Codigo = "1033", CriadaEmUtc = DateTime.UtcNow };
            db.ArmazemPosicoes.Add(posicao);
            var entrada = new DMO.Alpha.Core.Armazem.ArmazemMovimento
            {
                Tipo = DMO.Alpha.Core.Armazem.ArmazemMovimentoTipo.Entrada, ToolId = "T3",
                OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid()
            };
            db.ArmazemMovimentos.Add(entrada);
            db.SaveChanges();
            db.ArmazemOcupacoes.Add(new DMO.Alpha.Core.Armazem.ArmazemOcupacao { ToolId = "T3", PosicaoId = posicao.Id, MovimentoId = entrada.Id, DesdeUtc = DateTime.UtcNow });
            db.FerramentaEstados.Add(new DMO.Alpha.Core.Armazem.FerramentaEstado { ToolId = "T3", Estado = DMO.Alpha.Core.Armazem.FerramentaEstadoTipo.Reparado, RegistadoEmUtc = DateTime.UtcNow });
            var template = db.Templates.Single(t => t.TemplateKey == "tpl-test-planejador");
            template.Capabilities = template.Capabilities.Append(DmoCapabilities.Ferramentas).Distinct().ToArray();
            db.SaveChanges();
        }

        try
        {
            using var client = await LoginClientAsync("4598", "JobArmazem1!");
            var ferramentas = WebUtility.HtmlDecode(FerramentasUsadas(await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}")));

            Assert.Contains("Armazém · posição 1033 · Reparado", ferramentas);
            Assert.Contains("MF 5810 / lote 1 não está registada no Armazém e não tem estado.", ferramentas);
            Assert.Contains("href=\"/Ferramentas/Ficha?tool_id=MF-TOOL-9\"", ferramentas);
            Assert.DoesNotContain("CM 5447 / lote 3 não", ferramentas);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaEstados.RemoveRange(db.FerramentaEstados.Where(e => e.ToolId == "T3"));
            db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes.Where(o => o.ToolId == "T3"));
            db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos.Where(m => m.ToolId == "T3"));
            db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes.Where(p => p.Codigo == "1033"));
            var template = db.Templates.Single(t => t.TemplateKey == "tpl-test-planejador");
            template.Capabilities = template.Capabilities.Where(c => c != DmoCapabilities.Ferramentas).ToArray();
            db.SaveChanges();
        }
    }

    /// <summary>A zona sem os avisos do Armazém (não registada, sem estado).</summary>
    private static string SemAvisoDoArmazem(string html) =>
        Regex.Replace(html, "<div class=\"jobon-aviso warn\" role=\"status\" data-dmo-aviso-armazem>.*?</div>", string.Empty, RegexOptions.Singleline);
}
