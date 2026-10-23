using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A FOLHA NOVA DO JOB ON (/JobOn/Folha, Diogo 22/10/2026): numa página
/// própria com o painel do Planeamento e o regresso; abre bloqueada e só o
/// «Editar» (quem tem o Planeamento) a desbloqueia; quem só tem «Job On ver»
/// (jobon.view) vê sem Editar; as Opções são Controlo e Reparação, sem «Nova
/// produção»; a referência total é MF + BQ; Guardar volta à folha sem apagar
/// os campos que não mudaram.
/// </summary>
public partial class JobOnPageTests
{
    private const string FolhaPath = "/JobOn/Folha";

    private async Task<int> SeedJobOnCompletoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.JobOnFolhaCalLinhas.RemoveRange(db.JobOnFolhaCalLinhas);
        db.JobOnFolhas.RemoveRange(db.JobOnFolhas);
        db.Tools.Add(new Tool { ToolId = "F-CM", Type = ToolType.Cm, Reference = "5809", Lot = "01" });
        db.Tools.Add(new Tool { ToolId = "F-MF", Type = ToolType.Mf, Reference = "5810", Lot = "01" });
        db.Tools.Add(new Tool { ToolId = "F-BQ", Type = ToolType.Bq, Reference = "T188", Lot = "21" });
        var jobOn = new JobOn
        {
            Reference = "5810T188",
            ProductionNumber = "202602",
            Machine = new Machine { Code = "B2" },
            InicioEm = new DateOnly(2026, 10, 21),
            FimEm = new DateOnly(2026, 10, 28)
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "F-CM" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "F-MF" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "F-BQ" });
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    private void SeedUserSoVer(string operatorId, string providerUserId, string password)
    {
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Só ver", ProviderUserId = providerUserId };
        db.Users.Add(user);
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-test-jobon-ver");
        if (template is null)
        {
            template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-test-jobon-ver", Name = "Job On ver", Capabilities = new[] { DmoCapabilities.JobOnView } };
            db.Templates.Add(template);
        }
        db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }

    private static string Zona(string html, string marca)
    {
        var inicio = html.IndexOf(marca, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"A página não tem {marca}.");
        return html[inicio..];
    }

    [Fact]
    public async Task FolhaNova_OpcoesBoquilhas_SoComBoquilhas_AbreOsMovimentosDaBQDesteJobOn()
    {
        const string op = "4409";
        var email = $"{op}@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-folha-bq", "Folha1234!", email);
        var id = await SeedJobOnCompletoAsync();
        int bqId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = new User { Id = Guid.NewGuid(), OperatorId = op, Name = "Chefe BQ", ProviderUserId = "provider-folha-bq" };
            db.Users.Add(user);
            var template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-test-jobon-bq", Name = "Job On + BQ", Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.Boquilhas } };
            db.Templates.Add(template);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
            bqId = db.BqContexts.AsNoTracking().Single(c => c.JobOnId == id).Id;
        }
        using var client = await LoginClientAsync(op, "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        var opcoes = Zona(html, "data-jof-opcoes");
        opcoes = opcoes[..opcoes.IndexOf("</details>", StringComparison.Ordinal)];
        Assert.Contains($"href=\"/Boquilhas?bq_id={bqId}\" data-jof-bqs>Boquilhas</a>", opcoes, StringComparison.Ordinal);
        Assert.True(opcoes.IndexOf(">Reparação</a>", StringComparison.Ordinal) < opcoes.IndexOf(">Boquilhas</a>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao()
    {
        SeedUser("4401", "Chefe folha", "provider-folha-1", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4401", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        var folha = Zona(html, "data-jof-folha=");
        Assert.StartsWith("data-jof-folha=\"bloqueada\"", folha, StringComparison.Ordinal);
        // Bloqueada: nenhum campo da folha se escreve.
        Assert.DoesNotContain("name=\"Folha.CmNotas\"", html, StringComparison.Ordinal);
        // O cabeçalho do painel lateral é só «Planeamento», um link para /JobOn (Diogo, 23/10/2026).
        var painel = Zona(html, "<aside class=\"rail\"");
        painel = painel[..painel.IndexOf("class=\"rail-list\"", StringComparison.Ordinal)];
        Assert.Contains("<div class=\"rail-head\"><a class=\"rail-voltar\" href=\"/JobOn\">Planeamento</a><button", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("O que vai entrar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("As produções em curso e as que entram esta semana", html, StringComparison.Ordinal);
        Assert.DoesNotContain("‹ Planeamento", html, StringComparison.Ordinal);
        // Sem os títulos dos grupos (Diogo, 23/10/2026): Moldes, Gargalo, Sopro, Calibres.
        Assert.DoesNotContain("class=\"group-head\"", html, StringComparison.Ordinal);
        foreach (var titulo in new[] { "Moldes", "Gargalo", "Sopro", "Calibres" })
        {
            Assert.DoesNotContain($">{titulo}</h2>", html, StringComparison.Ordinal);
        }
        // A referência total é MF + BQ e as ferramentas leem-se «CM 5809 · Lote 01».
        Assert.Contains("<h1 class=\"sr-only\">5810T188</h1>", html, StringComparison.Ordinal);
        // O lote sem o zero à esquerda: «Lote 1», nunca «Lote 01».
        var cm = Zona(html, "data-jof-tool=\"CM\"");
        cm = cm[..cm.IndexOf("</button>", StringComparison.Ordinal)];
        Assert.Contains("data-jof-tool-ref>5809</b>", cm, StringComparison.Ordinal);
        Assert.Contains("data-jof-tool-lote>1</b>", cm, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote 01", html, StringComparison.Ordinal);
        // A cor da linha vem da mesma fonte do Planeamento (mch-B2).
        Assert.Contains("class=\"mch dmo-linha-cor mch-B2\" data-jof-linha>B2</span>", html, StringComparison.Ordinal);
        Assert.Contains($"data-jof-ficha=\"/Ferramentas/Ficha?tool_id=F-CM\"", html, StringComparison.Ordinal);
        // As Opções: Reparação (Planeamento); sem Controlo sem módulo do Controlo; nunca «Nova produção».
        var opcoes = Zona(html, "data-jof-opcoes");
        opcoes = opcoes[..opcoes.IndexOf("</details>", StringComparison.Ordinal)];
        // Editar e Duplicar são botões fora das Opções (o Diogo, 23/10), por baixo da referência.
        Assert.DoesNotContain("data-jof-editar", opcoes, StringComparison.Ordinal);
        Assert.DoesNotContain("Duplicar", opcoes, StringComparison.Ordinal);
        var acoes = Zona(html, "class=\"head-acoes\"");
        Assert.Contains("data-jof-imprimir>Imprimir</button>", acoes, StringComparison.Ordinal);
        // TP sem «Bacia PS» nem «Referência» na informação técnica; Calibres em lista vertical.
        var tp = Zona(html, "data-jof-familia=\"TP\"");
        tp = tp[..tp.IndexOf("</article>", StringComparison.Ordinal)];
        Assert.DoesNotContain("Bacia PS", tp, StringComparison.Ordinal);
        Assert.DoesNotContain("Referência", tp, StringComparison.Ordinal);
        Assert.DoesNotContain("dims calh", html, StringComparison.Ordinal);
        // A ordem da segunda linha: Imprimir · Duplicar · Editar · Opções (o Diogo, 23/10).
        var ordem = new[] { "data-jof-imprimir", "data-jof-duplicar", "data-jof-editar", "data-jof-opcoes" }.Select(m => acoes.IndexOf(m, StringComparison.Ordinal)).ToArray();
        Assert.All(ordem, i => Assert.True(i >= 0));
        Assert.Equal(ordem.OrderBy(i => i), ordem);
        // Em cima ficam só a referência e as produções: as Opções já não estão na pick-row.
        var pick = Zona(html, "class=\"pick-row\"");
        pick = pick[..pick.IndexOf("class=\"head-acoes\"", StringComparison.Ordinal)];
        Assert.DoesNotContain("data-jof-opcoes", pick, StringComparison.Ordinal);
        // No Sopro, o TP (trabalha com o CM) vem antes do CS (trabalha com o MF).
        Assert.True(html.IndexOf("data-jof-familia=\"TP\"", StringComparison.Ordinal) < html.IndexOf("data-jof-familia=\"CS\"", StringComparison.Ordinal));
        Assert.Contains($"href=\"/JobOn/Folha?jobon_id={id}&amp;editar=1\" data-jof-editar>Editar</a>", acoes, StringComparison.Ordinal);
        // O Duplicar abre a folha nova em Editar com os dados copiados (Diogo, 23/10/2026).
        Assert.Contains($"href=\"/JobOn/Folha?duplicar={id}\" data-jof-duplicar>Duplicar</a>", acoes, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data-jof-editar"));
        Assert.Contains($"href=\"/JobOn/Reparacao?jobon_id={id}\">Reparação</a>", opcoes, StringComparison.Ordinal);
        Assert.DoesNotContain(">Controlo</a>", opcoes, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-bqs", html, StringComparison.Ordinal); // sem a chave das Boquilhas
        Assert.DoesNotContain("Nova produção", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolhaNova_Editar_DesbloqueiaEGuardarVoltaAFolha_SemApagarOQueNaoMudou()
    {
        SeedUser("4402", "Chefe folha 2", "provider-folha-2", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4402", "Folha1234!");

        var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        Assert.Contains("data-jof-folha=\"editar\"", editar, StringComparison.Ordinal);
        Assert.Contains("id=\"lightForm\"", editar, StringComparison.Ordinal);
        Assert.Contains("data-jobon-rascunho=\"editar:" + id + "\"", editar, StringComparison.Ordinal);
        Assert.Contains("data-jof-artigo-ficheiro", editar, StringComparison.Ordinal);

        var (acao, campos) = EnvioDoFormulario.Mudar(editar, "jobon-cm-notas", "Rodar 1/4 de volta.");
        var guardar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, guardar.StatusCode);
        Assert.StartsWith($"/JobOn/Folha?jobon_id={id}&gravado=1", guardar.Headers.Location!.ToString(), StringComparison.Ordinal);

        // Segunda edição noutro campo: a nota do CM fica (o envio leva a folha toda).
        var editar2 = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        var (acao2, campos2) = EnvioDoFormulario.Mudar(editar2, "jobon-mf-notas", "Tratar gravações.");
        var guardar2 = await client.PostAsync(acao2, EnvioDoFormulario.Corpo(campos2));
        Assert.Equal(HttpStatusCode.Redirect, guardar2.StatusCode);

        var lida = await GetPageHtmlAsync(client, guardar2.Headers.Location!.ToString());
        Assert.Contains("data-jof-folha=\"bloqueada\"", lida, StringComparison.Ordinal);
        Assert.Contains("Job On guardado.", lida, StringComparison.Ordinal);
        Assert.Contains("Rodar 1/4 de volta.", lida, StringComparison.Ordinal);
        Assert.Contains("Tratar gravações.", lida, StringComparison.Ordinal);

        // CS · Furos: abertos e fechados (um dígito cada) no mesmo campo de texto («Abertos 4 / Fechados 2»).
        var editar3 = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        Assert.Contains("inputmode=\"numeric\" pattern=\"[0-9]\" maxlength=\"1\"", editar3, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"CS furos abertos (0 a 9)\"", editar3, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"CS furos fechados (0 a 9)\"", editar3, StringComparison.Ordinal);
        Assert.DoesNotContain("Bacia PS", editar3, StringComparison.Ordinal);
        var (acao3, campos3) = EnvioDoFormulario.Mudar(editar3, "jobon-cs-furos", "Abertos 4 / Fechados 2");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao3, EnvioDoFormulario.Corpo(campos3))).StatusCode);
        var editar4 = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        Assert.Contains("value=\"4\" aria-label=\"CS furos abertos (0 a 9)\"", editar4, StringComparison.Ordinal);
        Assert.Contains("value=\"2\" aria-label=\"CS furos fechados (0 a 9)\"", editar4, StringComparison.Ordinal);
        Assert.DoesNotContain("era «", editar4, StringComparison.Ordinal);
        var lida2 = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        Assert.Contains("Abertos 4 / Fechados 2", lida2, StringComparison.Ordinal);
        Assert.Contains("Tratar gravações.", lida, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("F-CM", db.CmContexts.AsNoTracking().Single(c => c.JobOnId == id && c.ReplacedAtUtc == null).ToolId);
    }

    [Fact]
    public async Task FolhaNova_JobOnVer_VeBloqueadaSemEditar_EOPlaneamentoLevaAFolha()
    {
        SeedUserSoVer("4403", "provider-folha-3", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4403", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        Assert.Contains("data-jof-folha=\"bloqueada\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"sr-only\">5810T188</h1>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-editar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-duplicar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-imprimir", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/JobOn/Reparacao", html, StringComparison.Ordinal);
        // Quem só vê não tem Opções (Diogo, 23/10/2026: «não vê controlo, boquilhas e reparação»).
        Assert.DoesNotContain("data-jof-opcoes", html, StringComparison.Ordinal);

        // Nem pelo endereço se desbloqueia.
        var forcado = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        Assert.Contains("data-jof-folha=\"bloqueada\"", forcado, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Folha.CmNotas\"", forcado, StringComparison.Ordinal);

        // O Planeamento (jobon.create) leva-o à folha da mesma produção.
        var planeamento = await client.GetAsync($"/JobOn?jobon_id={id}");
        Assert.Equal(HttpStatusCode.Redirect, planeamento.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={id}", planeamento.Headers.Location!.ToString());

        // E o Guardar do Planeamento não lhe abre a porta: o envio completo de
        // quem edita (com a nota mudada), forjado com o token de quem só vê.
        SeedUser("4413", "Chefe folha 13", "provider-folha-13", "Folha1234!");
        using var chefe = await LoginClientAsync("4413", "Folha1234!");
        var editar = await GetPageHtmlAsync(chefe, $"{FolhaPath}?jobon_id={id}&editar=1");
        var (acao, campos) = EnvioDoFormulario.Mudar(editar, "jobon-cm-notas", "Forjado por quem só vê.");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var forjados = campos.Select(c => c.Nome == "__RequestVerificationToken" ? (c.Nome, token) : c).ToList();
        var antes = await LerJobOnAsync(id);
        var post = await client.PostAsync(acao, EnvioDoFormulario.Corpo(forjados));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.DoesNotContain("gravado", post.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(antes, await LerJobOnAsync(id));
        Assert.DoesNotContain("Forjado por quem só vê.", antes, StringComparison.Ordinal);
    }

    // A referência, as datas, a linha e as notas da folha, para comparar antes e depois.
    private async Task<string> LerJobOnAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var j = await db.JobOns.AsNoTracking().Include(x => x.Machine).SingleAsync(x => x.Id == id);
        var folha = await db.JobOnFolhas.AsNoTracking().SingleOrDefaultAsync(x => x.JobOnId == id);
        return $"{j.Reference}|{j.ProductionNumber}|{j.InicioEm}|{j.FimEm}|{j.Machine?.Code}|{folha?.CmNotas}|{folha?.MfNotas}";
    }

    [Fact]
    public async Task FolhaNova_OpcoesControlo_ComControlo_AparecemControloAntesDaReparacao()
    {
        const string op = "4414";
        var email = $"{op}@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-folha-ctl", "Folha1234!", email);
        var id = await SeedJobOnCompletoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = new User { Id = Guid.NewGuid(), OperatorId = op, Name = "Chefe Controlo", ProviderUserId = "provider-folha-ctl" };
            db.Users.Add(user);
            var template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-test-jobon-ctl", Name = "Job On + Controlo", Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate } };
            db.Templates.Add(template);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }
        using var client = await LoginClientAsync(op, "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        var opcoes = Zona(html, "data-jof-opcoes");
        opcoes = opcoes[..opcoes.IndexOf("</details>", StringComparison.Ordinal)];
        var controlo = opcoes.IndexOf($"href=\"/Controlo/Resumo?jobon_id={id}\">Controlo</a>", StringComparison.Ordinal);
        var reparacao = opcoes.IndexOf($"href=\"/JobOn/Reparacao?jobon_id={id}\">Reparação</a>", StringComparison.Ordinal);
        Assert.True(controlo >= 0, "As Opções não têm o Controlo.");
        Assert.True(reparacao > controlo, "O Controlo vem antes da Reparação.");
    }

    [Fact]
    public async Task FolhaNova_SemReferenciaMfMaisBq_SoAvisa()
    {
        SeedUser("4404", "Chefe folha 4", "provider-folha-4", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var j = db.JobOns.Single(x => x.Id == id);
            j.Reference = "5810T999";
            db.SaveChanges();
        }
        using var client = await LoginClientAsync("4404", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        var aviso = Zona(html, "data-jof-aviso-referencia");
        aviso = aviso[..aviso.IndexOf("</div>", StringComparison.Ordinal)];
        Assert.Contains("A referência da produção é 5810T999, mas MF", aviso, StringComparison.Ordinal);
        Assert.Contains("BQ dá 5810T188.", aviso, StringComparison.Ordinal);
        Assert.Contains("data-jof-folha=\"bloqueada\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolhaNova_ProducoesAnteriores_NoDropdown_AbremAFolhaDeCada()
    {
        SeedUser("4405", "Chefe folha 5", "provider-folha-5", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        int anterior;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var velha = new JobOn { Reference = "5810T188", ProductionNumber = "202540", Machine = new Machine { Code = "C1" } };
            db.JobOns.Add(velha);
            db.SaveChanges();
            anterior = velha.Id;
        }
        using var client = await LoginClientAsync("4405", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        var dropdown = Zona(html, "id=\"jofProducoes\"");
        dropdown = dropdown[..dropdown.IndexOf("</select>", StringComparison.Ordinal)];
        Assert.Contains("data-dmo-ir", dropdown, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"/JobOn/Folha?jobon_id={id}\" selected=\"selected\">202602 · B2</option>", dropdown, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"/JobOn/Folha?jobon_id={anterior}\">202540 · C1</option>", dropdown, StringComparison.Ordinal);

        // A mais recente primeiro (como no mockup).
        Assert.True(dropdown.IndexOf("202602", StringComparison.Ordinal) < dropdown.IndexOf("202540", StringComparison.Ordinal));

        // A procura pela referência abre a última produção dela.
        var procura = await client.GetAsync($"{FolhaPath}?referencia=5810T188");
        Assert.Equal(HttpStatusCode.Redirect, procura.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={id}", procura.Headers.Location!.ToString());
    }
}
