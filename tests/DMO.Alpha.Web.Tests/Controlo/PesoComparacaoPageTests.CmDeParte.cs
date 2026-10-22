using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

// O Diogo, 19/10/2026: «Manter / Colocar de parte» cada CM da comparação
// guardada; as postas de parte vão para aprovação, uma decisão por CM, por
// quem tem o Controlo Aprovar (D10), na própria comparação («entra logo na
// comparação, não é uma página», o Diogo, 19/10/2026). E o «não comparável» quando a punção ou a densidade
// mudou desde o Peso inicial, sem recusar (D12).
public partial class PesoComparacaoPageTests
{
    private async Task<(int JobOnId, int PesoInicialId, int ComparacaoId)> SeedComparacaoGuardadaAsync()
    {
        var (_, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        using var scope = _factory.Services.CreateScope();
        var outcome = await new DMO.Alpha.Infrastructure.Controlo.CreatePesoComparacaoHandler(
                scope.ServiceProvider.GetRequiredService<DmoDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPesoComparisonQuery>())
            .HandleAsync(
            new CreatePesoComparacao(pesoInicialId, "T3", 20m, [new("12", 199.64m), new("21", 99.82m)]));
        Assert.Equal(PesoComparacaoResultado.Sucesso, outcome.Result);
        return (jobOnId, pesoInicialId, outcome.Comparacao!.ComparacaoId);
    }

    /// <summary>Quem aprova: um operador com o Controlo Criar (abre a comparação) e o Controlo Aprovar.</summary>
    private void SeedAprovador(string operatorId, string password)
    {
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = new Template
        {
            TemplateKey = $"tpl-aprovar-{operatorId}",
            Name = $"Aprovar {operatorId}",
            Capabilities = [DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]
        };
        db.Templates.Add(template);
        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Chefe Aprovador", ProviderUserId = $"provider-{operatorId}" };
        db.Users.Add(user);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }

    private List<PesoComparacaoCmDecisao> Decisoes()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DmoDbContext>()
            .PesoComparacaoCmDecisoes.AsNoTracking().ToList();
    }

    private void LimparDecisoes()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PesoComparacaoCmDecisoes.RemoveRange(db.PesoComparacaoCmDecisoes);
        db.SaveChanges();
    }

    private static string Zona(string html, string id)
    {
        var inicio = html.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"A zona {id} não está na página.");
        return html[inicio..];
    }

    [Fact]
    public async Task ComparacaoGuardada_ColocarDeParte_GravaPorCm_FicaPorAprovar_EVoltaAComparacao()
    {
        LimparDecisoes();
        var (jobOnId, pesoInicialId, comparacaoId) = await SeedComparacaoGuardadaAsync();
        SeedUser("4391", "Peso Operador", "provider-cmp-91", "PesoCmp91!");
        using var client = await LoginClientAsync("4391", "PesoCmp91!");
        var url = $"{PagePath}?comparacao=1&jobon_id={jobOnId}&peso_inicial={pesoInicialId}&comparacao_id={comparacaoId}";

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, url));
        var zona = Zona(html, $"pesoCmDeParte-{comparacaoId}");
        Assert.Contains("Manter ou colocar de parte", zona);
        Assert.Contains("<option value=\"de-parte\">Colocar de parte</option>", zona);

        var measurementId = await PrimeiraLeituraAsync(comparacaoId);
        var resposta = await client.PostAsync("/Controlo/PesoCmEscolha", HtmlFormHelpers.BuildFormPost(
            ("measurement_id", measurementId.ToString()),
            ("escolha", "de-parte"),
            ("voltar", url),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        var destino = resposta.Headers.Location!.ToString();
        Assert.StartsWith("/Controlo/Peso?", destino);
        Assert.Contains($"comparacao_id={comparacaoId}", destino);
        Assert.EndsWith("cm_de_parte=ok-de-parte", destino);
        var decisao = Assert.Single(Decisoes());
        Assert.Equal((measurementId, true, (bool?)null, "Peso Operador"), (decisao.MeasurementId, decisao.DeParte, decisao.Aprovado, decisao.EscolhidoPor));

        var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, destino));
        zona = Zona(depois, $"pesoCmDeParte-{comparacaoId}");
        Assert.Contains("CM posta de parte: fica por aprovar.", zona);
        Assert.Contains("Por aprovar", zona);
        // Sem o Controlo Aprovar, sem a escolha da aprovação.
        Assert.DoesNotContain("name=\"decisao\"", zona);
    }

    [Fact]
    public async Task AprovarNaComparacao_SoQuemTemControloAprovar_UmaCmDeCadaVez_EVoltaAComparacao()
    {
        LimparDecisoes();
        var (jobOnId, pesoInicialId, comparacaoId) = await SeedComparacaoGuardadaAsync();
        var measurementId = await PrimeiraLeituraAsync(comparacaoId);
        using (var scope = _factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Controlo.PesoCmDeParteHandler>();
            foreach (var linha in await handler.DaComparacaoAsync(comparacaoId, default))
                await handler.EscolherAsync(linha.MeasurementId, true, "Peso Operador", default);
        }
        var url = $"{PagePath}?comparacao=1&jobon_id={jobOnId}&peso_inicial={pesoInicialId}&comparacao_id={comparacaoId}";
        var escolhida = Decisoes().Single(d => d.MeasurementId == measurementId).EscolhidoEmUtc;
        (string, string)[] Aprovar(string token) =>
        [
            ("measurement_id", measurementId.ToString()),
            ("decisao", "aprovar"),
            ("escolhida_em", escolhida.Ticks.ToString()),
            ("voltar", url),
            ("__RequestVerificationToken", token)
        ];

        // Sem o Controlo Aprovar: a comparação mostra «Por aprovar», sem a
        // escolha, e um envio à mão não grava nada.
        SeedUser("4392", "Peso Operador", "provider-cmp-92", "PesoCmp92!");
        using (var semAprovar = await LoginClientAsync("4392", "PesoCmp92!"))
        {
            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(semAprovar, url));
            var zona = Zona(html, $"pesoCmDeParte-{comparacaoId}");
            Assert.Contains("Por aprovar", zona);
            Assert.DoesNotContain("name=\"decisao\"", zona);
            var recusada = await semAprovar.PostAsync("/Controlo/CmDeParte",
                HtmlFormHelpers.BuildFormPost(Aprovar(HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));
            Assert.Equal(HttpStatusCode.Redirect, recusada.StatusCode);
            Assert.DoesNotContain("cm_de_parte=ok-aprovado", recusada.Headers.Location!.ToString());
            Assert.All(Decisoes(), d => Assert.Null(d.Aprovado));
        }

        SeedAprovador("4393", "Aprovar4393!");
        using var client = await LoginClientAsync("4393", "Aprovar4393!");
        var pagina = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, url));
        var tabela = Zona(pagina, $"pesoCmDeParte-{comparacaoId}");
        Assert.Contains($"id=\"cmDeParteDecisao-{measurementId}\"", tabela);
        Assert.Contains("<option value=\"aprovar\">Aprovar</option>", tabela);
        // A aprovação já não tem página própria no menu do Controlo.
        Assert.DoesNotContain(">CM de parte</a>", pagina);

        var resposta = await client.PostAsync("/Controlo/CmDeParte",
            HtmlFormHelpers.BuildFormPost(Aprovar(HtmlFormHelpers.ExtractRequestVerificationToken(pagina)!)));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        var destino = resposta.Headers.Location!.ToString();
        Assert.StartsWith("/Controlo/Peso?", destino);
        Assert.Contains($"comparacao_id={comparacaoId}", destino);
        Assert.EndsWith("cm_de_parte=ok-aprovado", destino);
        var decisoes = Decisoes();
        var aprovada = decisoes.Single(d => d.MeasurementId == measurementId);
        Assert.Equal((true, "Chefe Aprovador"), (aprovada.Aprovado, aprovada.AprovadoPor));
        Assert.Null(decisoes.Single(d => d.MeasurementId != measurementId).Aprovado);

        var depois = Zona(WebUtility.HtmlDecode(await GetPageHtmlAsync(client, destino)), $"pesoCmDeParte-{comparacaoId}");
        Assert.Contains("Aprovado: a CM fica de parte.", depois);
        Assert.Contains("<option value=\"aprovar\" selected=\"selected\">Aprovar</option>", depois);
        Assert.Contains("Chefe Aprovador", depois);
    }

    [Fact]
    public async Task CompararPesos_ComADensidadeMudadaNasDefinicoes_AvisaNaoComparavel_ComLink_SemRecusar()
    {
        var (_, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            (await db.GlassDensities.SingleAsync()).DensityGramsPerCm3 = 2.4027m;
            await db.SaveChangesAsync();
        }

        SeedUser("4394", "Peso Operador", "provider-cmp-94", "PesoCmp94!");
        using var client = await LoginClientAsync("4394", "PesoCmp94!");
        var pagina = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&jobon_id={jobOnId}");
        var response = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(pagina)!)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resultado = Zona(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "pesoComparacaoResultado");
        Assert.Contains("Peso do vidro · controlo inicial / novas leituras", resultado);
        Assert.Contains("Não comparável.", resultado);
        Assert.Contains("A densidade do vidro é 2.4027 g/cm³ e no Peso inicial era 2.5000 g/cm³.", resultado);
        Assert.DoesNotContain("A punção da CM", resultado);
        Assert.Contains("href=\"/Definicoes?secao=controlo\">Ver a densidade nas Definições do Controlo</a>", resultado);
    }

    private async Task<int> PrimeiraLeituraAsync(int comparacaoId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PesoComparacaoMeasurements
            .Where(m => m.ComparacaoId == comparacaoId)
            .OrderBy(m => m.RowPosition)
            .Select(m => m.Id)
            .FirstAsync();
    }
}
