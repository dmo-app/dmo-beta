using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Pages.JobOn;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A folha da fábrica no formulário do Job On (a E6 do DMO-FULL, com as
/// decisões do proprietário): as famílias aos pares (CM+TP, MF+CS, BQ+AN,
/// PU+ARR, PI e CAL), a QTD Stock e a QTD Máq. escritas em cada família CM,
/// MF, BQ, PU, CS e TP e a QTD Máq. em cada linha CAL (Diogo, 21/10/2026). O
/// formulário que a página desenha grava a folha, o Editar volta a mostrá-la,
/// e um texto longo demais é recusado e a folha volta com o que se escreveu.
/// </summary>
public class JobOnFolhaTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/JobOn";

    private readonly CustomWebApplicationFactory _factory;

    public JobOnFolhaTests(CustomWebApplicationFactory factory)
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
        db.JobOnFolhaCalLinhas.RemoveRange(db.JobOnFolhaCalLinhas);
        db.JobOnFolhas.RemoveRange(db.JobOnFolhas);
        db.JobOnChanges.RemoveRange(db.JobOnChanges);
        db.JobOnLotNotes.RemoveRange(db.JobOnLotNotes);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private async Task<HttpClient> EntrarAsync(string operatorId)
    {
        var password = "FolhaPass1!";
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-folha-" + operatorId, password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-test-folha");
            if (template is null)
            {
                template = new Template
                {
                    Id = Guid.NewGuid(),
                    TemplateKey = "tpl-test-folha",
                    Name = "Planeamento de teste — folha do Job On",
                    Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
                };
                db.Templates.Add(template);
            }

            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Planeamento", ProviderUserId = "provider-folha-" + operatorId };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await client.GetAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await login.Content.ReadAsStringAsync())!;
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId), ("password", password), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>A produção 202601 em B1 com o MF 5810, sem nada escrito na folha.</summary>
    private async Task<(int JobOnId, int MachineId)> SeedJobOnAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        db.Tools.Add(new Tool { ToolId = "MF-FOLHA", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = "202601",
            Machine = b1,
            InicioEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-2),
            FimEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(5)
        };
        db.JobOns.Add(jobOn);
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-FOLHA" });
        await db.SaveChangesAsync();
        db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id });
        await db.SaveChangesAsync();
        return (jobOn.Id, b1.Id);
    }

    private static async Task<string> HtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<T> LerAsync<T>(Func<DmoDbContext, Task<T>> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return await ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    /// <summary>A zona da folha da fábrica (#lightFolha); falha quando não existe.</summary>
    private static string ZonaDaFolha(string html)
    {
        var inicio = html.IndexOf("id=\"lightFolha\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "A página não tem a folha da fábrica (#lightFolha).");
        var fim = html.IndexOf("id=\"lightToolWarnings\"", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "A folha da fábrica não acaba antes dos avisos das ferramentas.");
        return html[inicio..fim];
    }

    /// <summary>O value de um input pelo id (null sem value); falha quando o input não existe.</summary>
    private static string? ValorDoCampo(string html, string id)
    {
        var input = Regex.Match(html, $"<input\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>");
        Assert.True(input.Success, $"A página não tem o campo #{id}.");
        var valor = Regex.Match(input.Value, "\\bvalue=\"(?<v>[^\"]*)\"");
        return valor.Success ? WebUtility.HtmlDecode(valor.Groups["v"].Value) : null;
    }

    /// <summary>O texto de uma textarea pelo id; falha quando não existe.</summary>
    private static string TextoDaArea(string html, string id)
    {
        var area = Regex.Match(html, $"<textarea\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>(?<t>.*?)</textarea>", RegexOptions.Singleline);
        Assert.True(area.Success, $"A página não tem a área #{id}.");
        return WebUtility.HtmlDecode(area.Groups["t"].Value).TrimStart('\r', '\n');
    }

    private static bool Invalido(string html, string id) =>
        Regex.IsMatch(html, $"<(?:input|textarea)\\b(?=[^>]*\\bid=\"{Regex.Escape(id)}\")(?=[^>]*\\baria-invalid=\"true\")[^>]*>");

    [Fact]
    public async Task Editar_MostraAsFamiliasAosPares_ComAQtdStockEAQtdMaqEmCadaFamiliaENaCal()
    {
        var (jobOnId, _) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4701");

        var html = await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        var folha = ZonaDaFolha(html);

        // Os pares pela ordem: CM+TP, MF+CS, BQ+AN, PU+ARR, PI e CAL.
        var ordem = new[] { "CM", "TP", "MF", "CS", "BQ", "AN", "PU", "ARR", "PI", "CAL" };
        for (var i = 1; i < ordem.Length; i++)
        {
            Assert.True(
                Posicao(folha, $"data-jobon-familia=\"{ordem[i - 1]}\"") < Posicao(folha, $"data-jobon-familia=\"{ordem[i]}\""),
                $"{ordem[i - 1]} vem antes de {ordem[i]}");
        }

        // QTD Stock e QTD Máq. escritas em cada família que as tem.
        foreach (var familia in new[] { "cm", "mf", "bq", "pu", "cs", "tp" })
        {
            Assert.Contains($"<label for=\"jobon-{familia}-qtd-stock\">QTD Stock</label>", folha);
            Assert.Contains($"<label for=\"jobon-{familia}-qtd-maq\">QTD Máq.</label>", folha);
        }

        // A CAL abre com as oito medidas da folha da fábrica, cada uma com a QTD Máq.
        Assert.Equal("Bucha Marcada", ValorDoCampo(folha, "jobon-cal-0-designacao"));
        Assert.Null(ValorDoCampo(folha, "jobon-cal-0-qtd-maq"));
        Assert.Equal("Pinças", ValorDoCampo(folha, "jobon-cal-7-designacao"));
        Assert.Null(ValorDoCampo(folha, "jobon-cal-8-designacao")); // a linha vazia para acrescentar

        // O cabeçalho e as Notas gerais também são da folha; o FO não.
        Assert.Contains("id=\"jobon-cab-seccoes\"", html);
        Assert.Contains("id=\"jobon-notas-gerais\"", html);
        Assert.Contains("data-jobon-familia=\"CAL\"", folha);
        Assert.DoesNotContain("data-jobon-familia=\"FO\"", folha);

        // Os campos menos usados ficam no «Mais detalhes» da família, e
        // nenhum campo se diz opcional.
        Assert.True(Posicao(folha, "<summary>Mais detalhes</summary>") < Posicao(folha, "id=\"jobon-cm-pata\""));
        Assert.DoesNotContain("opcional", folha, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guardar_GravaAFolhaDoFormulario_EOEditarVoltaAMostrarOsValores()
    {
        var (jobOnId, _) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4702");
        var editar = await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");

        var (acao, campos) = EnvioDoFormulario.Preencher(editar,
            ("jobon-cm-qtd-stock", "12"),
            ("jobon-cm-qtd-maq", "4"),
            ("jobon-tp-qtd-stock", "2"),
            ("jobon-cs-qtd-maq", "1"),
            ("jobon-cab-seccoes", "11"),
            ("jobon-cm-pata", "36.5"),
            ("jobon-cal-0-valor", "8151"),
            ("jobon-cal-0-qtd-maq", "3"),
            ("jobon-notas-gerais", "Emparelhar MF+FF\r\nValidar costuras"));
        Assert.Equal($"{PagePath}?handler=Update", acao);
        var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        var gravada = await LerAsync(db => db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == jobOnId));
        Assert.Equal("12", gravada.CmQtdStock);
        Assert.Equal("4", gravada.CmQtdMaq);
        Assert.Equal("2", gravada.TpQtdStock);
        Assert.Equal("1", gravada.CsQtdMaq);
        Assert.Equal("11", gravada.CabSeccoes);
        Assert.Equal("36.5", gravada.CmPata);
        Assert.Equal("Emparelhar MF+FF\nValidar costuras", gravada.NotasGerais);
        Assert.Null(gravada.BqQtdStock);
        var cal = await LerAsync(db => db.JobOnFolhaCalLinhas.AsNoTracking().Where(l => l.JobOnId == jobOnId).OrderBy(l => l.Ordem).ToListAsync());
        var linha = Assert.Single(cal); // as outras sete medidas por omissão ficaram por escrever
        Assert.Equal(("Bucha Marcada", "8151", "3"), (linha.Designacao, linha.Valor, linha.QtdMaq));

        // Uma linha em «O que mudou» com o que mudou na folha.
        var mudou = await LerAsync(db => db.JobOnChanges.AsNoTracking().Where(c => c.JobOnId == jobOnId).ToListAsync());
        var folhaMudou = Assert.Single(mudou);
        Assert.Equal(JobOnChangeFields.Folha, folhaMudou.Field);
        // Pela ordem da folha; o que não cabe nos 120 caracteres conta-se no fim.
        Assert.Equal(
            "Cabeçalho · Secções, CM · QTD Stock, CM · QTD Máq., CM · Diâm. pata (mm), TP · QTD Stock, CS · QTD Máq. e mais 2",
            folhaMudou.NewValueFrozen);

        // O Editar volta a mostrar o que ficou gravado.
        var reaberta = await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        Assert.Equal("12", ValorDoCampo(reaberta, "jobon-cm-qtd-stock"));
        Assert.Equal("4", ValorDoCampo(reaberta, "jobon-cm-qtd-maq"));
        Assert.Equal("2", ValorDoCampo(reaberta, "jobon-tp-qtd-stock"));
        Assert.Equal("11", ValorDoCampo(reaberta, "jobon-cab-seccoes"));
        Assert.Equal("Bucha Marcada", ValorDoCampo(reaberta, "jobon-cal-0-designacao"));
        Assert.Equal("3", ValorDoCampo(reaberta, "jobon-cal-0-qtd-maq"));
        Assert.Equal("Emparelhar MF+FF\nValidar costuras", TextoDaArea(reaberta, "jobon-notas-gerais").Replace("\r\n", "\n"));

        // Guardar outra vez sem mudar nada não regista outra alteração.
        var (acao2, campos2) = EnvioDoFormulario.Mudar(reaberta, "jobon-cm-qtd-stock", "12");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao2, EnvioDoFormulario.Corpo(campos2))).StatusCode);
        Assert.Equal(1, await LerAsync(db => db.JobOnChanges.CountAsync(c => c.JobOnId == jobOnId)));
    }

    [Fact]
    public async Task Criar_GravaAFolhaNaMesmaEscritaDoJobOn()
    {
        var (_, machineId) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4703");
        var novo = await HtmlAsync(client,
            $"{PagePath}?novo=1&reference=1075C142&production_number=202699&machine_id={machineId}&mf_tool_id=MF-FOLHA");

        var (acao, campos) = EnvioDoFormulario.Preencher(novo,
            ("jobon-pu-qtd-maq", "6"),
            ("jobon-bq-qtd-stock", "9"),
            ("jobon-cal-2-qtd-maq", "1"));
        Assert.Equal($"{PagePath}?handler=Create", acao);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);

        var jobOnId = await LerAsync(db => db.JobOns.Where(j => j.ProductionNumber == "202699").Select(j => j.Id).SingleAsync());
        var gravada = await LerAsync(db => db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == jobOnId));
        Assert.Equal(("6", "9"), (gravada.PuQtdMaq, gravada.BqQtdStock));
        var linha = Assert.Single(await LerAsync(db => db.JobOnFolhaCalLinhas.AsNoTracking().Where(l => l.JobOnId == jobOnId).ToListAsync()));
        Assert.Equal(("Tampão", "1", 1), (linha.Designacao, linha.QtdMaq, linha.Ordem));
        // A criação não é uma alteração.
        Assert.Equal(0, await LerAsync(db => db.JobOnChanges.CountAsync(c => c.JobOnId == jobOnId)));
    }

    [Fact]
    public async Task TextoLongoDemais_NoEditar_ERecusado_EAFolhaVoltaComOQueSeEscreveu()
    {
        var (jobOnId, _) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4704");
        var editar = await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        var fimAntes = await LerAsync(db => db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.FimEm).SingleAsync());
        var notaLonga = new string('x', JobOnFolhaCampos.MaximoNota + 1);

        var (acao, campos) = EnvioDoFormulario.Preencher(editar,
            ("jobon-cm-notas", notaLonga),
            ("jobon-cm-qtd-stock", "7"),
            ("jobon-cal-1-qtd-maq", "5"),
            ("lightEnd", "2030-01-31"));
        var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        // Volta à página (não ao endereço): a recusa e tudo o que se escreveu.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var html = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("«CM · Notas» tem mais de 1000 caracteres. Encurte o texto e volte a guardar. Nenhuma alteração foi guardada.", AvisoDeRecusa(html));
        Assert.Equal("7", ValorDoCampo(html, "jobon-cm-qtd-stock"));
        Assert.Equal("5", ValorDoCampo(html, "jobon-cal-1-qtd-maq"));
        Assert.Equal("2030-01-31", ValorDoCampo(html, "lightEnd"));
        Assert.Equal(notaLonga, TextoDaArea(html, "jobon-cm-notas"));
        Assert.True(Invalido(html, "jobon-cm-notas"));
        Assert.False(Invalido(html, "jobon-cm-qtd-stock"));
        Assert.Contains("name=\"jobon_edit_id\"", html);

        // Nada foi gravado.
        Assert.False(await LerAsync(db => db.JobOnFolhas.AnyAsync(f => f.JobOnId == jobOnId)));
        Assert.False(await LerAsync(db => db.JobOnFolhaCalLinhas.AnyAsync(l => l.JobOnId == jobOnId)));
        Assert.Equal(0, await LerAsync(db => db.JobOnChanges.CountAsync(c => c.JobOnId == jobOnId)));
        Assert.Equal(fimAntes, await LerAsync(db => db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.FimEm).SingleAsync()));

        // Encurtado no limite, o mesmo formulário grava.
        var (acao2, campos2) = EnvioDoFormulario.Mudar(html, "jobon-cm-notas", notaLonga[..JobOnFolhaCampos.MaximoNota]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao2, EnvioDoFormulario.Corpo(campos2))).StatusCode);
        var gravada = await LerAsync(db => db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == jobOnId));
        Assert.Equal(("7", JobOnFolhaCampos.MaximoNota), (gravada.CmQtdStock, gravada.CmNotas!.Length));
    }

    [Fact]
    public async Task TextoLongoDemais_NoCriar_NaoCriaOJobOn_EAFolhaVoltaComOQueSeEscreveu()
    {
        var (_, machineId) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4705");
        var novo = await HtmlAsync(client,
            $"{PagePath}?novo=1&reference=1075C142&production_number=202698&machine_id={machineId}&mf_tool_id=MF-FOLHA");
        var valorLongo = new string('9', JobOnFolhaCampos.CalMaximo + 1);

        var (acao, campos) = EnvioDoFormulario.Preencher(novo,
            ("jobon-cal-3-valor", valorLongo),
            ("jobon-mf-qtd-maq", "2"));
        var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var html = await resposta.Content.ReadAsStringAsync();
        var aviso = AvisoDeRecusa(html);
        Assert.Contains("«CAL · linha 4 · Valor» tem mais de 60 caracteres.", aviso);
        Assert.Contains("Nenhum Job On foi criado.", aviso);
        // A linha 4 é a «Inversão MF», na mesma posição em que se escreveu.
        Assert.Equal("Inversão MF", ValorDoCampo(html, "jobon-cal-3-designacao"));
        Assert.Equal(valorLongo, ValorDoCampo(html, "jobon-cal-3-valor"));
        Assert.True(Invalido(html, "jobon-cal-3-valor"));
        Assert.False(Invalido(html, "jobon-cal-3-designacao"));
        Assert.Equal("2", ValorDoCampo(html, "jobon-mf-qtd-maq"));
        Assert.Equal("202698", ValorDoCampo(html, "lightProduction"));
        Assert.False(await LerAsync(db => db.JobOns.AnyAsync(j => j.ProductionNumber == "202698")));
    }

    /// <summary>Grava a folha pelo formulário do Editar que a página desenha.</summary>
    private static async Task GravarPeloEditarAsync(HttpClient client, int jobOnId, params (string Id, string Valor)[] valores)
    {
        var editar = await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        var (acao, campos) = EnvioDoFormulario.Preencher(editar, valores);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);
    }

    /// <summary>A folha na ficha em leitura (#fichaFolha); falha quando não existe.</summary>
    private static string FolhaNaFicha(string html) => Controlo.PesoZonas.Zona(html, "fichaFolha");

    /// <summary>A família da folha na ficha, até ao seu fecho; null quando a família não aparece.</summary>
    private static string? FamiliaNaFicha(string zona, string familia)
    {
        var abertura = Regex.Match(zona, $"<section\\b[^>]*\\bdata-jobon-ficha-familia=\"{Regex.Escape(familia)}\"");
        if (!abertura.Success)
        {
            return null;
        }

        var nivel = 0;
        foreach (Match m in Regex.Matches(zona[abertura.Index..], "<section\\b|</section>"))
        {
            nivel += m.Value == "</section>" ? -1 : 1;
            if (nivel == 0)
            {
                return zona.Substring(abertura.Index, m.Index + m.Length);
            }
        }

        throw new InvalidOperationException($"A família {familia} da ficha ficou por fechar.");
    }

    /// <summary>Os campos de uma família na ficha, como o operador os lê: (rótulo, valor), pela ordem.</summary>
    private static IReadOnlyList<(string Rotulo, string Valor)> CamposNaFicha(string zona, string familia)
    {
        var secao = FamiliaNaFicha(zona, familia);
        Assert.True(secao is not null, $"A ficha não mostra a família {familia}.");
        return Regex.Matches(secao!, "<dt\\b[^>]*>(?<r>.*?)</dt>\\s*<dd>(?<v>.*?)</dd>", RegexOptions.Singleline)
            .Select(m => (WebUtility.HtmlDecode(m.Groups["r"].Value), WebUtility.HtmlDecode(m.Groups["v"].Value).Replace("\r\n", "\n")))
            .ToList();
    }

    [Fact]
    public async Task Ficha_MostraSoAsFamiliasEOsCamposEscritos_ComAQtdStockEAQtdMaq()
    {
        var (jobOnId, _) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4706");
        await GravarPeloEditarAsync(client, jobOnId,
            ("jobon-cab-seccoes", "11"),
            ("jobon-cm-qtd-stock", "12"),
            ("jobon-cm-qtd-maq", "4"),
            ("jobon-cm-notas", "Polir a pata\r\nVer folga"),
            ("jobon-cm-pata-tol", "0.05"),
            ("jobon-tp-qtd-maq", "2"),
            ("jobon-notas-gerais", "Emparelhar MF+FF"));

        var zona = FolhaNaFicha(await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}"));

        // Só os campos escritos, pela ordem da folha (as notas no fim da
        // família), com a QTD Stock e a QTD Máq. à vista; sem «—» nos vazios.
        Assert.Equal(
            [("QTD Stock", "12"), ("QTD Máq.", "4"), ("Tol. diâm. pata (mm)", "0.05"), ("Notas", "Polir a pata\nVer folga")],
            CamposNaFicha(zona, "CM"));
        Assert.Equal([("QTD Máq.", "2")], CamposNaFicha(zona, "TP"));
        Assert.Equal([("Secções", "11")], CamposNaFicha(zona, "Cabeçalho"));
        Assert.Equal([("Notas gerais", "Emparelhar MF+FF")], CamposNaFicha(zona, "Notas gerais"));
        Assert.DoesNotContain("—", zona);

        // As famílias sem nada escrito não aparecem, nem a CAL sem linhas gravadas.
        foreach (var vazia in new[] { "MF", "CS", "BQ", "AN", "PU", "ARR", "PI", "CAL" })
        {
            Assert.Null(FamiliaNaFicha(zona, vazia));
        }

        Assert.DoesNotContain(FichaDaFolhaVista.SemNadaEscrito, zona);
        // Em leitura: nada se escreve na ficha.
        Assert.DoesNotContain("<input", zona);
        Assert.DoesNotContain("<textarea", zona);
    }

    [Fact]
    public async Task Ficha_ACalMostraAsLinhasGravadas_ComAQtdMaq()
    {
        var (jobOnId, _) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4707");
        await GravarPeloEditarAsync(client, jobOnId,
            ("jobon-cal-0-valor", "8151"),
            ("jobon-cal-0-qtd-maq", "3"),
            ("jobon-cal-1-valor", "2,5"),
            ("jobon-cal-2-designacao", "Tampão novo"),
            ("jobon-cal-2-qtd-maq", "1"));

        var zona = FolhaNaFicha(await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}"));

        var cal = FamiliaNaFicha(zona, "CAL");
        Assert.True(cal is not null, "A ficha não mostra a CAL.");
        Assert.Contains(">QTD Máq.</th>", cal);
        var linhas = Regex.Matches(cal!, "<tr data-jobon-ficha-cal>(?<l>.*?)</tr>", RegexOptions.Singleline)
            .Select(l => Regex.Matches(l.Groups["l"].Value, "<td>(?<c>.*?)</td>").Select(c => WebUtility.HtmlDecode(c.Groups["c"].Value)).ToList())
            .Select(c => (c[0], c[1], c[2]))
            .ToList();
        // As três linhas gravadas, pela ordem; as cinco medidas por omissão por escrever não.
        Assert.Equal([("Bucha Marcada", "8151", "3"), ("CS P Interna", "2,5", ""), ("Tampão novo", "", "1")], linhas);
        // O PI, o par da CAL, não tem nada escrito e não aparece.
        Assert.Null(FamiliaNaFicha(zona, "PI"));
    }

    [Fact]
    public async Task Ficha_SemNadaEscritoNaFolha_DizSoIssoComOEditar()
    {
        var (jobOnId, _) = await SeedJobOnAsync();
        using var client = await EntrarAsync("4708");

        var zona = FolhaNaFicha(await HtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}"));

        Assert.Contains(FichaDaFolhaVista.SemNadaEscrito, zona);
        Assert.Contains($"href=\"/JobOn?jobon_id={jobOnId}&amp;editar=1#lightSheet\">Editar</a>", zona);
        Assert.DoesNotContain("data-jobon-ficha-familia", zona);
    }

    /// <summary>O texto do aviso de recusa da folha (data-dmo-toast="erro").</summary>
    private static string AvisoDeRecusa(string html)
    {
        var aviso = System.Text.RegularExpressions.Regex.Match(html, "<div data-dmo-toast=\"erro\"[^>]*>(.*?)</div>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(aviso.Success, "A página não tem o aviso de recusa.");
        return WebUtility.HtmlDecode(aviso.Groups[1].Value);
    }
}
