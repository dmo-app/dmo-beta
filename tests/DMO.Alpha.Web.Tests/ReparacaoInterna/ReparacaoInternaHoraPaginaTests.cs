using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Presentation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.ReparacaoInterna;

/// <summary>
/// A hora da reparação na página (Diogo, 23/10/2026): o campo «Hora» ao lado
/// do «Dia», na mesma linha, vem com a hora local de agora e vai com o
/// registo (form=); mudada, fica a escrita; inválida, nada se grava e a
/// página diz porquê. As de antes (sem hora) mostram a hora do registo. A
/// janela de corrigir leva a hora da linha e muda-a.
/// </summary>
public sealed class ReparacaoInternaHoraPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RIHOR";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoInternaHoraPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var maquinas = db.Machines.Where(m => m.Code.StartsWith(Marca)).Select(m => m.Id).ToList();
        var jobOns = db.JobOns.Where(j => maquinas.Contains(j.MachineId)).Select(j => j.Id).ToList();
        var reparacoes = db.ReparacoesInternas.Where(r => maquinas.Contains(r.MachineId)).ToList();
        var ids = reparacoes.Select(r => r.Id).ToList();
        var resultados = ids.Select(id => (int?)id).ToList();
        db.ReparacoesInternasRasto.RemoveRange(db.ReparacoesInternasRasto.Where(r => ids.Contains(r.ReparacaoId)));
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas.Where(o => o.ResultadoTipo == "Reparacao" && resultados.Contains(o.ResultadoId)));
        db.ReparacoesInternas.RemoveRange(reparacoes);
        db.CmContexts.RemoveRange(db.CmContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.MfContexts.RemoveRange(db.MfContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.JobOns.RemoveRange(db.JobOns.Where(j => jobOns.Contains(j.Id)));
        db.Machines.RemoveRange(db.Machines.Where(m => maquinas.Contains(m.Id)));
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId.StartsWith(Marca)));
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Hora{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-rihor-{operatorId}-{Guid.NewGuid():N}", Name = $"RI {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Reparador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            return db.SaveChanges();
        });
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

    private T NaBase<T>(Func<DmoDbContext, T> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    private Guid Utilizador(string operatorId) => NaBase(db => db.Users.AsNoTracking().Single(u => u.OperatorId == operatorId).Id);

    /// <summary>Uma máquina desta classe com uma produção que cobre hoje e ontem, com a CM e o MF.</summary>
    private (string Codigo, int JobOn) Maquina(string sufixo) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var maquina = new Machine { Code = codigo };
        var referencia = $"77{sufixo}T324";
        db.Machines.Add(maquina);
        db.Tools.AddRange(
            new Tool { ToolId = $"{codigo}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "1" },
            new Tool { ToolId = $"{codigo}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "1" });
        var jobOn = new JobOn { Machine = maquina, Reference = referencia, ProductionNumber = $"{sufixo}01", InicioEm = hoje.AddDays(-2), FimEm = hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        db.SaveChanges();
        db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm" });
        db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf" });
        db.SaveChanges();
        return (codigo, jobOn.Id);
    });

    /// <summary>Regista os números pela página (Enter no campo) e devolve os ids, pela ordem.</summary>
    private async Task<int[]> RegistarAsync(HttpClient client, string codigo, int jobOn, params string[] numeros)
    {
        foreach (var numero in numeros)
        {
            var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
            var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", numero));
            using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
            Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        }

        return NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOn).OrderBy(r => r.Id).Select(r => r.Id).ToArray());
    }

    private static string Elemento(string html, string atributo)
    {
        var inicio = html.IndexOf(atributo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"a página não tem {atributo}");
        inicio = html.LastIndexOf('<', inicio);
        var tag = Regex.Match(html[(inicio + 1)..], "^[a-z][a-z0-9]*").Value;
        var nivel = 0;
        foreach (Match m in Regex.Matches(html[inicio..], $"<{tag}\\b|</{tag}>"))
        {
            nivel += m.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (nivel == 0)
            {
                return html.Substring(inicio, m.Index + m.Length);
            }
        }

        throw new InvalidOperationException($"{atributo} ficou por fechar");
    }

    private static string Atributo(string elemento, string nome) =>
        WebUtility.HtmlDecode(Regex.Match(elemento, $"{nome}=\"([^\"]*)\"").Groups[1].Value);

    /// <summary>
    /// O que o ri-corrigir.js faz com o duplo clique na linha (ou «Editar»):
    /// enche a janela da reparação com a reparação e o número da linha, a
    /// linha, a produção, o dia e a página da tabela e um envio novo; a
    /// pessoa escreve o resto e carrega Guardar (riCorrigir) ou Anular
    /// (riAnular, o botão com o formaction). Os campos escritos podem vir
    /// com o prefixo riAnular (o motivo): é a mesma janela.
    /// </summary>
    private static (string Acao, IReadOnlyList<(string Nome, string Valor)> Campos) Janela(
        string html, int reparacaoId, string prefixo, Guid envio, params (string IdDoCampo, string Valor)[] escritos)
    {
        var linhaEm = html.IndexOf($"data-dmo-reparacao=\"{reparacaoId}\"", StringComparison.Ordinal);
        Assert.True(linhaEm >= 0, $"a página não tem a reparação {reparacaoId}");
        var tabela = Elemento(html[html.LastIndexOf("<table", linhaEm, StringComparison.Ordinal)..], "data-ri-maquina");
        var linha = Regex.Match(Elemento(html, $"data-dmo-reparacao=\"{reparacaoId}\""), "<tr[^>]*>").Value;
        var preenchidos = new List<(string, string)>
        {
            ("riCorrigirOperacao", envio.ToString()),
            ("riCorrigirId", Atributo(linha, "data-ri-reparacao")),
            ("riCorrigirMaquina", Atributo(tabela, "data-ri-maquina")),
            ("riCorrigirProducao", Atributo(tabela, "data-ri-producao")),
            ("riCorrigirTipo", Atributo(tabela, "data-ri-tipo")),
            ("riCorrigirDia", Atributo(tabela, "data-ri-dia")),
            ("riCorrigirPagina", Atributo(tabela, "data-ri-pagina")),
            ("riCorrigirNumero", Atributo(linha, "data-ri-numero"))
        };
        foreach (var (id, valor) in escritos)
        {
            var campo = id.Replace("riAnular", "riCorrigir", StringComparison.Ordinal);
            preenchidos.RemoveAll(p => p.Item1 == campo);
            preenchidos.Add((campo, valor));
        }

        return EnvioDoFormulario.Enviar(html, [.. preenchidos], idDoBotao: prefixo == "riAnular" ? "riAnularBotao" : "riGuardarBotao");
    }

    /// <summary>O mesmo envio feito por outra pessoa (o token dela; RI-04: quem tem o módulo pode corrigir).</summary>
    private static IReadOnlyList<(string Nome, string Valor)> ComTokenDe(IReadOnlyList<(string Nome, string Valor)> campos, string htmlDoOutro) =>
        [.. campos.Where(c => c.Nome != "__RequestVerificationToken"), ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(htmlDoOutro)!)];


    private static string Lisboa(DateTime utc) => DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(utc).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task OCampoHora_AoLadoDoDia_VemComAHoraDeAgora_EGravaSeComOsMinutos()
    {
        var (codigo, jobOn) = Maquina("A");
        using var client = await LoginOperatorAsync("6801", DmoCapabilities.ReparacaoInterna);
        var antes = Lisboa(DateTime.UtcNow);

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        // O Dia e a Hora na mesma linha, no Chromium (tests/visual/ri_hora_browser.py).
        ExportarParaOBrowser.Gravar("ri-hora.html", html);

        // Na mesma linha do Dia (o mesmo .ri-linha), mas do formulário do registo.
        var linhaDoDia = Elemento(html, $"id=\"riDiaForm-{codigo}\"");
        var campo = Regex.Match(linhaDoDia, $"<input id=\"riHora-{codigo}\"[^>]*>").Value;
        Assert.Contains("type=\"time\"", campo);
        Assert.Contains($"form=\"riRegistar-{codigo}\"", campo);
        var valor = Atributo(campo, "value");
        Assert.Contains(valor, new[] { antes, Lisboa(DateTime.UtcNow) });

        var ids = await RegistarAsync(client, codigo, jobOn, "457");
        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Id == ids[0]));
        Assert.Equal(TimeOnly.ParseExact(valor, "HH:mm"), r.Hora);
    }

    [Fact]
    public async Task AHoraMudada_FicaGravada_EAparecenoRegisto()
    {
        var (codigo, jobOn) = Maquina("B");
        using var client = await LoginOperatorAsync("6802", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", "12"), ($"riHora-{codigo}", "06:15"));

        using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.JobOnId == jobOn));
        Assert.Equal(new TimeOnly(6, 15), r.Hora);
        var depois = await client.GetStringAsync(gravar.Headers.Location!.OriginalString);
        var linha = Elemento(depois, $"data-dmo-reparacao=\"{r.Id}\"");
        Assert.Contains("<td>06:15</td>", linha);
        Assert.Contains("data-ri-hora=\"06:15\"", linha);
    }

    [Fact]
    public async Task UmaHoraInvalida_Recusa_ComMensagem_ENadaSeGrava()
    {
        var (codigo, jobOn) = Maquina("C");
        using var client = await LoginOperatorAsync("6803", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", "12"), ($"riHora-{codigo}", "25:99"));

        using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.OK, gravar.StatusCode);
        var pagina = await gravar.Content.ReadAsStringAsync();
        Assert.Contains("Hora inválida: use HH:mm (ex.: 14:30). Nada foi gravado.", Elemento(pagina, "id=\"riAvisos\""));
        var campo = Regex.Match(pagina, $"<input id=\"riHora-{codigo}\"[^>]*>").Value;
        Assert.Contains("value=\"25:99\"", campo);
        Assert.Contains("aria-invalid=\"true\"", campo);
        Assert.False(NaBase(db => db.ReparacoesInternas.Any(x => x.JobOnId == jobOn)));
    }

    // Os limites da hora: 00:00 e 23:59 gravam, 24:00 recusa sem gravar.
    [Theory]
    [InlineData("G", "6807", "00:00", true)]
    [InlineData("H", "6808", "23:59", true)]
    [InlineData("J", "6809", "24:00", false)]
    public async Task OsLimitesDaHora_DaMeiaNoiteAo2359_E24Recusa(string sufixo, string operador, string valor, bool grava)
    {
        var (codigo, jobOn) = Maquina(sufixo);
        using var client = await LoginOperatorAsync(operador, DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", "12"), ($"riHora-{codigo}", valor));

        using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        if (grava)
        {
            Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
            Assert.Equal(TimeOnly.ParseExact(valor, "HH:mm"), NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.JobOnId == jobOn)).Hora);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, gravar.StatusCode);
            Assert.Contains("Hora inválida: use HH:mm (ex.: 14:30). Nada foi gravado.", Elemento(await gravar.Content.ReadAsStringAsync(), "id=\"riAvisos\""));
            Assert.False(NaBase(db => db.ReparacoesInternas.Any(x => x.JobOnId == jobOn)));
        }
    }

    [Fact]
    public async Task UmaDeAntes_SemHora_MostraAHoraDoRegisto()
    {
        var (codigo, jobOn) = Maquina("D");
        using var client = await LoginOperatorAsync("6804", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "33");
        var registadoEm = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(5).AddMinutes(4), DateTimeKind.Utc);
        NaBase(db =>
        {
            var r = db.ReparacoesInternas.Single(x => x.Id == ids[0]);
            r.Hora = null;
            r.RegistadoEmUtc = registadoEm;
            return db.SaveChanges();
        });

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        var linha = Elemento(html, $"data-dmo-reparacao=\"{ids[0]}\"");
        Assert.Contains($"<td>{Lisboa(registadoEm)}</td>", linha);
        Assert.Contains("<strong>33</strong>", linha);
    }

    [Fact]
    public async Task Corrigir_MudaAHora_ComRasto()
    {
        var (codigo, jobOn) = Maquina("E");
        using var client = await LoginOperatorAsync("6805", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "457");
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        Assert.Contains("id=\"riCorrigirHora\" name=\"hora\" type=\"time\"", Elemento(html, "id=\"riCorrigirJanela\""));
        var (acao, campos) = Janela(html, ids[0], "riCorrigir", Guid.NewGuid(), ("riCorrigirHora", "07:45"));

        using var corrigir = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, corrigir.StatusCode);
        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Id == ids[0]));
        Assert.Equal(("457", (TimeOnly?)new TimeOnly(7, 45)), (r.Numero, r.Hora));
        var rasto = Assert.Single(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(x => x.ReparacaoId == ids[0]).ToList()));
        Assert.Equal((TimeOnly?)new TimeOnly(7, 45), rasto.HoraDepois);
        var depois = await client.GetStringAsync(corrigir.Headers.Location!.OriginalString);
        Assert.Contains("<td>07:45</td>", Elemento(depois, $"data-dmo-reparacao=\"{ids[0]}\""));
    }

    [Fact]
    public async Task Corrigir_ComUmaHoraInvalida_NadaMuda()
    {
        var (codigo, jobOn) = Maquina("F");
        using var client = await LoginOperatorAsync("6806", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "457");
        var antes = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Id == ids[0]).Hora);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = Janela(html, ids[0], "riCorrigir", Guid.NewGuid(), ("riCorrigirNumero", "458"), ("riCorrigirHora", "7h"));

        using var corrigir = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        var depois = await client.GetStringAsync(corrigir.Headers.Location!.OriginalString);
        Assert.Contains("Hora inválida: use HH:mm (ex.: 14:30). Nada mudou.", Elemento(depois, "id=\"riAvisos\""));
        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Id == ids[0]));
        Assert.Equal(("457", antes), (r.Numero, r.Hora));
    }
}
