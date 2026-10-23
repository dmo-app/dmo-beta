using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Planeamento · Sumário (Diogo, 19/10/2026): ao lado do Job On, o que há
/// para fazer e o que mudou, pelos módulos da conta. Abre no dia de hoje; o
/// calendário escolhe o dia; filtros por tipo de informação e por dias. A
/// informação do Controlo é de quem tem um dos módulos do Controlo.
/// </summary>
public sealed partial class PlaneamentoSumarioPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PagePath = "/Planeamento/Sumario";

    private readonly CustomWebApplicationFactory _factory;

    public PlaneamentoSumarioPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DmoBusinessTime.Now);

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Sumario{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-sumario-{operatorId}", Name = $"Sumário {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<int> JobOnAsync(string reference, DateOnly? inicio, DateTime? criadoEmUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = await db.Machines.FirstOrDefaultAsync(m => m.Code == "B1");
        if (machine is null)
        {
            machine = new Machine { Code = "B1" };
            db.Machines.Add(machine);
        }

        var jobOn = new JobOn { Reference = reference, ProductionNumber = "7", Machine = machine, InicioEm = inicio, CriadoEmUtc = criadoEmUtc };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    private static async Task<string> GetAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"{PagePath}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Planeamento_SeesTheProductionsStartingToday_AndTheTabSitsNextToJobOn()
    {
        var hoje = await JobOnAsync("S1A001", Hoje);
        await JobOnAsync("S1A002", Hoje.AddDays(1));
        using var client = await LoginAsync("5101", DmoCapabilities.JobOnCreate);

        var html = await GetAsync(client);

        Assert.Contains(">Sumário</a>", html);
        Assert.True(html.IndexOf(">Job On</a>", StringComparison.Ordinal) < html.IndexOf(">Sumário</a>", StringComparison.Ordinal));
        Assert.True(html.IndexOf(">Sumário</a>", StringComparison.Ordinal) < html.IndexOf(">Histórico</a>", StringComparison.Ordinal));
        Assert.Contains("<h3>Mudanças</h3>", html);
        Assert.Contains($"<a class=\"sumario-titulo\" href=\"/JobOn?jobon_id={hoje}\">S1A001 7</a>", html);
        // A de amanhã só pode estar no painel lateral («O que vai entrar»).
        Assert.DoesNotContain("S1A002", SemPainelLateral(html));
        Assert.Contains("id=\"dmoCalendario\"", html);
    }

    [Fact]
    public async Task Linhas_TemADataAFrente_AMaquinaComACor_ESemContagens()
    {
        // Diogo, 19/10/2026: «as datas estão no fim» (passam para o início da
        // linha), «remover os (1)» e a página «muito branca»: cada linha leva
        // a cor da máquina, como no Job On.
        var ontem = await JobOnAsync("S9A001", Hoje.AddDays(-1));
        using var client = await LoginAsync("5109", DmoCapabilities.JobOnCreate);

        var html = await GetAsync(client, $"?ano={Hoje.Year}&mes={Hoje.Month}&dia={Hoje.Day}&dias=7&tipo=comecam");

        Assert.Matches(
            $"<li class=\"sumario-item mch-B1\">\\s*<time class=\"sumario-quando\">{Semana(Hoje.AddDays(-1)).Split('-')[0]} {Hoje.AddDays(-1):dd/MM}</time>\\s*<strong class=\"sumario-maquina\">B1</strong>\\s*<a class=\"sumario-titulo\" href=\"/JobOn\\?jobon_id={ontem}\">S9A001 7</a>",
            html);
        Assert.DoesNotMatch("<h3>[^<]*\\(\\d+\\)</h3>", html);
        Assert.Contains("<option value=\"comecam\" selected=\"selected\">Mudanças</option>", html);
        // O dia da semana por extenso junto às datas do título (Diogo, 19/10/2026).
        Assert.Contains($">{Semana(Hoje.AddDays(-6))}, {Hoje.AddDays(-6):dd/MM/yyyy} a {Semana(Hoje)}, {Hoje:dd/MM/yyyy}</h2>", html);
        // A primeira linha não se cola à barra do título: o espaço vem do gap
        // da grelha do grupo, que a regra global do h3 não apaga.
        var css = await client.GetStringAsync("/css/dmo-sumario.css");
        Assert.Contains(".sumario-grupo{display:grid;gap:8px;", css);
    }

    private static string Semana(DateOnly data)
    {
        var nome = data.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("pt-PT"));
        return char.ToUpperInvariant(nome[0]) + nome[1..];
    }

    [Fact]
    public async Task ControloInformation_IsForEitherControloModule()
    {
        var jobOnId = await JobOnAsync("S2A001", Hoje);

        using (var planeamento = await LoginAsync("5200", DmoCapabilities.JobOnCreate))
        {
            // Nem as linhas nem a opção do filtro.
            var html = await GetAsync(planeamento);
            Assert.DoesNotContain("Job Ons por ver no Controlo", html);
            Assert.DoesNotContain("Controlos por aprovar", html);
            Assert.DoesNotContain("Controlos não aprovados", html);
        }

        // Com o Controlo Criar abre o Resumo; só com o Controlo Aprovar, a Revisão.
        using var criar = await LoginAsync("5201", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);
        Assert.Contains($"href=\"/Controlo/Resumo?jobon_id={jobOnId}\">S2A001", await GetAsync(criar, "?tipo=por-ver"));
        using var aprovar = await LoginAsync("5202", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove);
        var porVer = await GetAsync(aprovar, "?tipo=por-ver");
        // No grupo «Importante» do topo, com o tipo na etiqueta (Diogo, 23/10/2026).
        Assert.Contains("<span class=\"sumario-etiqueta\">Job Ons por ver no Controlo</span>", porVer);
        Assert.Contains($"href=\"/Controlo/Approve?jobon_id={jobOnId}\">S2A001", porVer);

        // Depois de alguém abrir o Resumo, já não está por ver.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            db.ControloViewings.Add(new ControloViewing { ControloId = controlo.Id, OperatorId = "5202", ViewedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain("S2A001", SemPainelLateral(await GetAsync(aprovar, "?tipo=por-ver")));
    }

    [Fact]
    public async Task AMesmaAlteracao_NoMesmoMinuto_FicaNumaSoLinha_EOutraContinua()
    {
        // «Se a informação já existe não duplicar no sumário» (Diogo, 23/10/2026).
        var jobOnId = await JobOnAsync("S3D001", Hoje);
        var agora = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            foreach (var quando in new[] { agora, agora.AddSeconds(-1) })
            {
                db.JobOnChanges.Add(new JobOnChange
                {
                    JobOnId = jobOnId, Field = JobOnChangeFields.Machine, OldValueFrozen = "S3DM1", NewValueFrozen = "S3DM2",
                    ChangedByNameFrozen = "Diogo", ChangedAtUtc = quando
                });
            }
            // Outra alteração (outro valor) não se junta.
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOnId, Field = JobOnChangeFields.Machine, OldValueFrozen = "S3DM2", NewValueFrozen = "S3DM3",
                ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora
            });
            await db.SaveChangesAsync();
        }

        using var client = await LoginAsync("5303", DmoCapabilities.JobOnCreate);
        var html = await GetAsync(client, "?tipo=alteracoes");

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Mudou a linha: S3DM1 → S3DM2"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Mudou a linha: S3DM2 → S3DM3"));
    }

    [Fact]
    public async Task Changes_AreListed_AlsoBeforeTheStart_AndTheDaysFilterReachesBack()
    {
        var jobOnId = await JobOnAsync("S3A001", Hoje.AddDays(-10));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOnId, Field = JobOnChangeFields.Machine, OldValueFrozen = "S3M1", NewValueFrozen = "S3M2",
                ChangedByNameFrozen = "Chefe", ChangedAtUtc = DateTime.UtcNow.AddDays(-3)
            });
            // Antes do início também conta (Diogo, 19/10/2026: «se existir alterações mostrar também»).
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOnId, Field = JobOnChangeFields.Calote, OldValueFrozen = "S3C1", NewValueFrozen = "S3C2",
                ChangedAtUtc = DateTime.UtcNow.AddDays(-12)
            });
            await db.SaveChangesAsync();
        }

        using var client = await LoginAsync("5301", DmoCapabilities.JobOnCreate);

        Assert.DoesNotContain("S3M1", await GetAsync(client, "?tipo=alteracoes"));
        var semana = await GetAsync(client, "?tipo=alteracoes&dias=7");
        Assert.Contains("<h3>Alterações a Job Ons</h3>", semana);
        Assert.Contains("Mudou a linha: S3M1 → S3M2", semana);
        Assert.Contains("Chefe", semana);
        Assert.Contains("S3C1 → S3C2", await GetAsync(client, "?tipo=alteracoes&dias=30"));
        // O filtro por tipo deixa só esse tipo.
        Assert.DoesNotContain("data-sumario-tipo=\"comecam\"", semana);
    }

    [Fact]
    public async Task AJobOn_IsAnnouncedOnTheDayItWasCreated_NotOnlyOnItsStart()
    {
        // Criado hoje para começar daqui a cinco dias.
        var jobOnId = await JobOnAsync("S8A001", Hoje.AddDays(5), DateTime.UtcNow);
        using var client = await LoginAsync("5801", DmoCapabilities.JobOnCreate);

        var hoje = await GetAsync(client, "?tipo=criados");
        Assert.Contains("<h3>Job Ons criados</h3>", hoje);
        Assert.Contains($"<a class=\"sumario-titulo\" href=\"/JobOn?jobon_id={jobOnId}\">S8A001 7</a>", hoje);
        Assert.Contains($"Começa a {Hoje.AddDays(5):dd/MM/yyyy}", hoje);
        Assert.DoesNotContain("S8A001", await GetAsync(client, "?tipo=comecam"));
    }

    [Fact]
    public async Task TheCalendarOpensTheChosenDay()
    {
        var dia = Hoje.AddDays(-2);
        await JobOnAsync("S4A001", dia);
        using var client = await LoginAsync("5401", DmoCapabilities.JobOnCreate);

        // Sem fim, a produção está em curso e aparece no painel lateral (Diogo,
        // 22/10/2026); fora dele, só no dia escolhido.
        Assert.DoesNotContain("S4A001", SemPainelLateral(await GetAsync(client)));
        var html = await GetAsync(client, $"?ano={dia.Year}&mes={dia.Month}&dia={dia.Day}");
        Assert.Contains("S4A001", html);
        Assert.Contains(dia.ToString("dd/MM/yyyy"), html);
        // O duplo clique no dia escolhido (dia=0) tira a seleção.
        Assert.Contains("Escolha um dia no calendário", await GetAsync(client, $"?ano={Hoje.Year}&mes={Hoje.Month}&dia=0"));
    }

    [Fact]
    public async Task DeletedBqMovements_AreOnlyForBoquilhas()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.BqMovementDeletions.Add(new BqMovementDeletion
            {
                BqMovementId = 99, BqRepairTraceId = 9999, Type = BqMovementType.Saida, Quantity = 3,
                OccurredAtUtc = DateTime.UtcNow, MachineCodeFrozen = "C2", DeletedByNameFrozen = "Operador Sumário 5502", DeletedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var planeamento = await LoginAsync("5501", DmoCapabilities.JobOnCreate))
        {
            Assert.DoesNotContain("Movimentos de boquilhas apagados", await GetAsync(planeamento));
        }

        using var boquilhas = await LoginAsync("5502", DmoCapabilities.JobOnCreate, DmoCapabilities.Boquilhas);
        var html = await GetAsync(boquilhas, "?tipo=bq-apagados");
        Assert.Contains("<span class=\"sumario-etiqueta\">Movimentos de boquilhas apagados</span>", html);
        Assert.Contains("<strong class=\"sumario-maquina\">C2</strong>", html);
        Assert.Contains("Saída de 3", html);
        Assert.Contains("Operador Sumário 5502", html);
    }

    // Diogo, 23/10/2026: os dados técnicos fazem falta ao Peso e aos
    // Pegamentos, por isso vê-os quem tem o Controlo Criar, não o módulo
    // Ferramentas. Com Ferramentas a linha abre a ficha técnica; sem ele, o
    // Resumo do controlo da produção.
    [Fact]
    public async Task ToolsWithMissingTechnicalData_AreForControloCreate_NotForFerramentas()
    {
        var jobOnId = await JobOnAsync("S7A001", Hoje);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new DMO.Alpha.Core.Tools.Tool { ToolId = "CM-S7A001", Type = DMO.Alpha.Core.Tools.ToolType.Cm, Reference = "S7A001", Lot = "2" });
            db.ToolTechnicalContexts.Add(new DMO.Alpha.Core.Tools.ToolTechnicalContext
            {
                ToolId = "CM-S7A001", Process = DMO.Alpha.Core.Tools.CmProcess.Ps, NominalWeightGrams = 300m, VolumePuncaoCm3 = 12m
            });
            db.CmContexts.Add(new CmContext { JobOnId = jobOnId, ToolId = "CM-S7A001" });
            await db.SaveChangesAsync();
        }

        using (var planeamento = await LoginAsync("5701", DmoCapabilities.JobOnCreate))
        {
            Assert.DoesNotContain("CM-S7A001", await GetAsync(planeamento));
        }

        using (var ferramentas = await LoginAsync("5702", DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas))
        {
            var semControlo = await GetAsync(ferramentas, "?tipo=dados-em-falta");
            Assert.DoesNotContain("Ferramentas com dados técnicos em falta", semControlo);
            Assert.DoesNotContain("S7A001 · Lote 2", semControlo);
        }

        using (var controlo = await LoginAsync("5703", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate))
        {
            var html = await GetAsync(controlo, "?tipo=dados-em-falta");
            Assert.Contains("<span class=\"sumario-etiqueta\">Ferramentas com dados técnicos em falta</span>", html);
            Assert.Contains($"href=\"/Controlo/Resumo?jobon_id={jobOnId}\">CM S7A001 · Lote 2</a>", html);
            Assert.Contains("Falta: gargalo, tampão · S7A001 7", html);
        }

        using var ambos = await LoginAsync("5704", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.Ferramentas);
        Assert.Contains("href=\"/Ferramentas/Ficha?tool_id=CM-S7A001&tecnica=1\">CM S7A001 · Lote 2</a>", await GetAsync(ambos, "?tipo=dados-em-falta"));
    }

    // «ao sair da produção entra no armazém dá o aviso, e podemos usar o
    // sumário para dar o aviso a quem tem módulo ferramentas» (Diogo,
    // 22/10/2026): o lembrete aberto aparece no Sumário, com o link para a ficha.
    [Fact]
    public async Task UtilizacaoPorAtualizar_AvisaNoSumario_SoQuemTemFerramentas()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new DMO.Alpha.Core.Tools.Tool { ToolId = "CM-USO1", Type = DMO.Alpha.Core.Tools.ToolType.Cm, Reference = "5447T173", Lot = "11" });
            db.FerramentaLembretesUtilizacao.Add(new DMO.Alpha.Core.Tools.FerramentaLembreteUtilizacao { ToolId = "CM-USO1", CriadoEmUtc = new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        }

        using (var planeamento = await LoginAsync("5711", DmoCapabilities.JobOnCreate))
        {
            Assert.DoesNotContain("data-dmo-uso-por-atualizar", await GetAsync(planeamento));
        }

        using var ferramentas = await LoginAsync("5712", DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas);
        var html = await GetAsync(ferramentas);
        // Já não é um aviso à parte: é uma linha do grupo «Importante» (Diogo, 23/10/2026).
        Assert.DoesNotContain("<p class=\"dmo-aviso warn\" data-dmo-uso-por-atualizar", html);
        var linha = System.Text.RegularExpressions.Regex.Match(html, "<li [^>]*data-dmo-uso-por-atualizar=\"CM-USO1\">[\\s\\S]*?</li>").Value;
        Assert.Contains(" 21/10 ", linha);
        Assert.Contains("<span class=\"sumario-etiqueta\">% de uso atualizada</span>", linha);
        Assert.Contains("href=\"/Ferramentas/Ficha?tool_id=CM-USO1\">CM 5447T173 · Lote 11</a>", linha);
        Assert.Contains("Utilização por atualizar (entrou no Armazém)", linha);
    }

    [Fact]
    public async Task WithoutPlaneamento_ThePageIsRefused()
    {
        using var client = await LoginAsync("5601", DmoCapabilities.Boquilhas);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(PagePath)).StatusCode);
    }
}
