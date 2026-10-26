using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// CRIAR E DUPLICAR NA FOLHA NOVA (Diogo, 23/10/2026: «esta versão do Job On
/// já não deveria existir»): o «Criar Job On» abre /JobOn/Folha vazia em
/// Editar e o «Duplicar» abre-a com os dados da origem, sem datas nem nº (o
/// início é o dia escolhido no calendário). Guardam pelo mesmo Create do
/// Planeamento, com as mesmas recusas tipadas (CM, BQ e MF sempre); uma
/// recusa volta à folha com o que se escreveu e não grava nada. Quem só tem
/// «Job On ver» não cria nem duplica. Escolher uma produção no Planeamento
/// abre a folha; a ficha antiga já não existe. O cabeçalho do painel lateral
/// é só «Planeamento» e a folha não tem os títulos dos grupos.
/// </summary>
public partial class JobOnPageTests
{
    private async Task<int> MachineIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.Machines.AsNoTracking().Where(m => m.Code == code).Select(m => m.Id).SingleAsync();
    }

    private static IReadOnlyList<(string Nome, string Valor)> ComValores(
        IReadOnlyList<(string Nome, string Valor)> campos, params (string Nome, string Valor)[] valores) =>
        campos.Select(c => valores.FirstOrDefault(v => v.Nome == c.Nome) is { Nome: not null } v ? (c.Nome, v.Valor) : c).ToList();

    [Fact]
    public async Task FolhaNova_Criar_AbreVaziaEmEditar_EGuardaPeloCreateEVoltaAFolha()
    {
        SeedUser("4421", "Chefe criar", "provider-folha-criar-1", "Folha1234!");
        await SeedJobOnCompletoAsync();
        var b2 = await MachineIdAsync("B2");
        using var client = await LoginClientAsync("4421", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?novo=1&etapa=criar&machine_id={b2}&inicio_em=2026-11-02");
        Assert.Contains("data-jof-folha=\"editar\" data-jof-criar=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/JobOn?handler=Create\"", html, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"voltar\" value=\"folha\" />", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"jobon_edit_id\"", html, StringComparison.Ordinal);
        Assert.Contains("data-jobon-rascunho=\"novo\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"lightReference\" name=\"reference\" type=\"text\" value=\"\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"lightProduction\" name=\"production_number\" type=\"text\" value=\"\"", html, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{b2}\" selected=\"selected\">B2</option>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"inicio_em\" type=\"date\" value=\"2026-11-02\"", html, StringComparison.Ordinal);
        Assert.Contains("Falta CM, MF, BQ: o Job On precisa de CM, MF e BQ.", html, StringComparison.Ordinal);
        Assert.Contains(">Novo Job On</span>", html, StringComparison.Ordinal);
        Assert.Contains("<a class=\"btn\" href=\"/JobOn\">Cancelar</a>", html, StringComparison.Ordinal);

        var (acao, campos) = EnvioDoFormulario.Preencher(html,
            ("lightReference", "5810t188"), ("lightProduction", "202650"), ("jobon-cm-reparador", "Nova CM polida."));
        Assert.Equal("/JobOn?handler=Create", acao);
        var envio = ComValores(campos, ("cm_tool_id", "F-CM"), ("mf_tool_id", "F-MF"), ("bq_tool_id", "F-BQ"));
        var guardar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(envio));
        Assert.Equal(HttpStatusCode.Redirect, guardar.StatusCode);
        var destino = guardar.Headers.Location!.ToString();
        Assert.Matches(@"^/JobOn/Folha\?jobon_id=\d+&gravado=1$", destino);

        var lida = await GetPageHtmlAsync(client, destino);
        Assert.Contains("data-jof-folha=\"bloqueada\"", lida, StringComparison.Ordinal);
        Assert.Contains("Job On guardado.", lida, StringComparison.Ordinal);
        Assert.Contains("Nova CM polida.", lida, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var novo = await db.JobOns.AsNoTracking().Include(j => j.Machine).SingleAsync(j => j.ProductionNumber == "202650");
        Assert.Equal("5810T188", novo.Reference);
        Assert.Equal("B2", novo.Machine!.Code);
        Assert.Equal(new DateOnly(2026, 11, 2), novo.InicioEm);
        Assert.Equal("F-CM", db.CmContexts.AsNoTracking().Single(c => c.JobOnId == novo.Id).ToolId);
        Assert.Equal("F-BQ", db.BqContexts.AsNoTracking().Single(c => c.JobOnId == novo.Id).ToolId);
    }

    [Theory]
    [InlineData("4431", "", "F-MF", "F-BQ", "MissingCmTool", "Falta a CM (contra molde)")]
    [InlineData("4432", "F-CM", "F-MF", "", "MissingBqTool", "Falta a BQ (boquilha)")]
    [InlineData("4433", "F-CM", "", "F-BQ", "MissingMfTool", "O MF (molde final) é obrigatório")]
    public async Task FolhaNova_Criar_SemCmBqOuMf_ERecusado_AFolhaVoltaComOQueSeEscreveu_ENadaEGravado(
        string operador, string cm, string mf, string bq, string recusa, string mensagem)
    {
        SeedUser(operador, "Chefe recusa", $"provider-folha-recusa-{recusa}", "Folha1234!");
        await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync(operador, "Folha1234!");
        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?novo=1&etapa=criar&machine_id={await MachineIdAsync("B2")}");
        var (acao, campos) = EnvioDoFormulario.Preencher(html,
            ("lightReference", "5810T188"), ("lightProduction", "202651"), ("jobon-cm-reparador", "Escrito antes da recusa."));
        var envio = ComValores(campos, ("cm_tool_id", cm), ("mf_tool_id", mf), ("bq_tool_id", bq));

        var post = await client.PostAsync(acao, EnvioDoFormulario.Corpo(envio));
        // A recusa leva o mesmo envio de volta à folha (307): nada se perde.
        Assert.Equal(HttpStatusCode.TemporaryRedirect, post.StatusCode);
        var volta = post.Headers.Location!.ToString();
        Assert.Equal($"/JobOn/Folha?handler=Recusa&create_refusal={recusa}", volta);
        var folha = await client.PostAsync(volta, EnvioDoFormulario.Corpo(envio));
        Assert.Equal(HttpStatusCode.OK, folha.StatusCode);
        var recusada = WebUtility.HtmlDecode(await folha.Content.ReadAsStringAsync());
        Assert.Contains("<div data-dmo-toast=\"erro\" class=\"jobon-aviso\" role=\"alert\" data-jof-recusa>" + mensagem, recusada, StringComparison.Ordinal);
        Assert.Contains("Nenhum Job On foi criado.", recusada, StringComparison.Ordinal);
        Assert.Contains("data-jof-criar=\"true\"", recusada, StringComparison.Ordinal);
        Assert.Contains("name=\"reference\" type=\"text\" value=\"5810T188\"", recusada, StringComparison.Ordinal);
        Assert.Contains("name=\"production_number\" type=\"text\" value=\"202651\"", recusada, StringComparison.Ordinal);
        Assert.Contains("Escrito antes da recusa.", recusada, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.False(await db.JobOns.AnyAsync(j => j.ProductionNumber == "202651"));
    }

    [Fact]
    public async Task FolhaNova_Criar_NumeroRepetido_ERecusadoTipado_ESoAvisaAsDatasTrocadas()
    {
        SeedUser("4441", "Chefe repetido", "provider-folha-repetido", "Folha1234!");
        await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4441", "Folha1234!");
        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?novo=1&etapa=criar&machine_id={await MachineIdAsync("B2")}");
        var ferramentas = new[] { ("cm_tool_id", "F-CM"), ("mf_tool_id", "F-MF"), ("bq_tool_id", "F-BQ") };

        // O mesmo nº na mesma referência: a recusa tipada de sempre.
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("lightReference", "5810T188"), ("lightProduction", "202602"));
        var repetido = await client.PostAsync(acao, EnvioDoFormulario.Corpo(ComValores(campos, ferramentas)));
        Assert.Equal(HttpStatusCode.TemporaryRedirect, repetido.StatusCode);
        Assert.EndsWith("create_refusal=DuplicateProductionNumber", repetido.Headers.Location!.ToString(), StringComparison.Ordinal);

        // O fim antes do início não é impossível: grava e só avisa.
        var (acao2, campos2) = EnvioDoFormulario.Preencher(html,
            ("lightReference", "5810T188"), ("lightProduction", "202652"), ("lightStart", "2026-11-10"), ("lightEnd", "2026-11-09"));
        var gravado = await client.PostAsync(acao2, EnvioDoFormulario.Corpo(ComValores(campos2, ferramentas)));
        Assert.Equal(HttpStatusCode.Redirect, gravado.StatusCode);
        var destino = gravado.Headers.Location!.ToString();
        Assert.StartsWith("/JobOn/Folha?jobon_id=", destino, StringComparison.Ordinal);
        Assert.Contains("fim_antes_inicio=1", destino, StringComparison.Ordinal);
        var lida = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, destino));
        Assert.Contains("Job On guardado com o fim (09/11/2026) antes do início (10/11/2026).", lida, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolhaNova_Duplicar_CopiaOsDados_SemDatas_ComAProducaoSeguinte_ComODiaDoCalendario_EGravaUmaProducaoNova()
    {
        SeedUser("4422", "Chefe duplicar", "provider-folha-duplicar-1", "Folha1234!");
        var origem = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4422", "Folha1234!");
        // A folha da origem com uma nota, para se ver que vem copiada.
        var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={origem}&editar=1");
        var (acaoNota, camposNota) = EnvioDoFormulario.Mudar(editar, "jobon-mf-reparador", "Nota da origem.");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acaoNota, EnvioDoFormulario.Corpo(camposNota))).StatusCode);

        // Escolhida no Planeamento num dia, o Duplicar da folha leva esse dia.
        var lida = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={origem}&dia=2026-11-05");
        Assert.Contains($"href=\"/JobOn/Folha?duplicar={origem}&amp;inicio_em=2026-11-05\" data-jof-duplicar>Duplicar</a>", lida, StringComparison.Ordinal);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?duplicar={origem}&inicio_em=2026-11-05"));
        Assert.Contains("data-jof-folha=\"editar\" data-jof-criar=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/JobOn?handler=Create\"", html, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"duplicar_de\" value=\"{origem}\" />", html, StringComparison.Ordinal);
        Assert.Contains($"data-jobon-rascunho=\"duplicar:{origem}\"", html, StringComparison.Ordinal);
        Assert.Contains(">Novo Job On · duplicado de 202602</span>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"reference\" type=\"text\" value=\"5810T188\"", html, StringComparison.Ordinal);
        // A produção sugerida é a seguinte à última da referência (Diogo, 26/10/2026); escreve-se por cima.
        Assert.Contains("name=\"production_number\" type=\"text\" value=\"202603\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"inicio_em\" type=\"date\" value=\"2026-11-05\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"fim_em\" type=\"date\" aria-label=\"Fim\"", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"F-CM\" selected=\"selected\">", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"F-MF\" selected=\"selected\">", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"F-BQ\" selected=\"selected\">", html, StringComparison.Ordinal);
        Assert.Contains("Nota da origem.", html, StringComparison.Ordinal);
        Assert.Contains($"<a class=\"btn\" href=\"/JobOn/Folha?jobon_id={origem}\">Cancelar</a>", html, StringComparison.Ordinal);

        // Sem dia escolhido, o Duplicar abre sem início.
        var semDia = await GetPageHtmlAsync(client, $"{FolhaPath}?duplicar={origem}");
        Assert.Contains("name=\"inicio_em\" type=\"date\" aria-label=\"Início\"", semDia, StringComparison.Ordinal);

        var (acao, campos) = EnvioDoFormulario.Mudar(html, "lightProduction", "202660");
        var guardar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, guardar.StatusCode);
        Assert.Matches(@"^/JobOn/Folha\?jobon_id=\d+&gravado=1", guardar.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var nova = await db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == "202660");
        Assert.NotEqual(origem, nova.Id);
        Assert.Equal("5810T188", nova.Reference);
        Assert.Equal(new DateOnly(2026, 11, 5), nova.InicioEm);
        Assert.Null(nova.FimEm);
        Assert.Equal("Nota da origem.", (await db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == nova.Id)).MfReparador);
        // A origem fica como estava.
        var antiga = await db.JobOns.AsNoTracking().SingleAsync(j => j.Id == origem);
        Assert.Equal("202602", antiga.ProductionNumber);
        Assert.Equal(new DateOnly(2026, 10, 21), antiga.InicioEm);
    }

    [Fact]
    public async Task FolhaNova_AbreComOTemplateSemEditar_SoReferenciaEProducao_EDepoisCria()
    {
        SeedUser("4425", "Chefe escolher", "provider-folha-escolher", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        var b2 = await MachineIdAsync("B2");
        using var client = await LoginClientAsync("4425", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?novo=1&machine_id={b2}&inicio_em=2026-11-02");
        Assert.DoesNotContain("data-jof-folha=\"editar\"", html, StringComparison.Ordinal);
        Assert.Contains("<form id=\"jofNovo\" method=\"get\" action=\"/JobOn/Folha\"", html, StringComparison.Ordinal);
        Assert.Contains("form=\"jofNovo\"", html, StringComparison.Ordinal);

        var existente = await client.GetAsync($"{FolhaPath}?novo=1&etapa=criar&machine_id={b2}&inicio_em=2026-11-02&referencia=5810t188");
        Assert.Equal(HttpStatusCode.Redirect, existente.StatusCode);
        Assert.StartsWith($"/JobOn/Folha?jobon_id={id}", existente.Headers.Location!.ToString(), StringComparison.Ordinal);

        var nova = await GetPageHtmlAsync(client, $"{FolhaPath}?novo=1&etapa=criar&machine_id={b2}&inicio_em=2026-11-02&referencia=9999t1&producao=202690");
        Assert.Contains("data-jof-folha=\"editar\" data-jof-criar=\"true\"", nova, StringComparison.Ordinal);
        Assert.Contains("id=\"lightReference\" name=\"reference\" type=\"text\" value=\"9999T1\"", nova, StringComparison.Ordinal);
        Assert.Contains("id=\"lightProduction\" name=\"production_number\" type=\"text\" value=\"202690\"", nova, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolhaNova_JobOnVer_NaoCriaNemDuplica_SoVe()
    {
        SeedUserSoVer("4423", "provider-folha-ver-criar", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4423", "Folha1234!");

        var criar = await client.GetAsync($"{FolhaPath}?novo=1");
        Assert.Equal(HttpStatusCode.Redirect, criar.StatusCode);
        Assert.Equal("/JobOn/Folha", criar.Headers.Location!.ToString());
        var linhaVazia = await client.GetAsync($"{FolhaPath}?novo=1&machine_id={await MachineIdAsync("B2")}&inicio_em=2026-11-02");
        Assert.Equal(HttpStatusCode.Redirect, linhaVazia.StatusCode);
        var semCriar = await GetPageHtmlAsync(client, linhaVazia.Headers.Location!.ToString());
        Assert.DoesNotContain("jofNovo", semCriar, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-criar", semCriar, StringComparison.Ordinal);
        var duplicar = await client.GetAsync($"{FolhaPath}?duplicar={id}");
        Assert.Equal(HttpStatusCode.Redirect, duplicar.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={id}", duplicar.Headers.Location!.ToString());

        // Nem forjando o envio do Criar com o token de quem só vê.
        SeedUser("4424", "Chefe ver criar", "provider-folha-ver-criar-chefe", "Folha1234!");
        using var chefe = await LoginClientAsync("4424", "Folha1234!");
        var html = await GetPageHtmlAsync(chefe, $"{FolhaPath}?novo=1&etapa=criar&machine_id={await MachineIdAsync("B2")}");
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("lightReference", "5810T188"), ("lightProduction", "202670"));
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"))!;
        var forjados = ComValores(campos, ("cm_tool_id", "F-CM"), ("mf_tool_id", "F-MF"), ("bq_tool_id", "F-BQ"), ("__RequestVerificationToken", token));
        var post = await client.PostAsync(acao, EnvioDoFormulario.Corpo(forjados));
        Assert.NotEqual(HttpStatusCode.OK, post.StatusCode);
        Assert.DoesNotContain("gravado", post.Headers.Location?.ToString() ?? "", StringComparison.Ordinal);
        var recusa = await client.PostAsync($"{FolhaPath}?handler=Recusa&create_refusal=MissingBqTool", EnvioDoFormulario.Corpo(forjados));
        Assert.Equal(HttpStatusCode.Redirect, recusa.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.False(await db.JobOns.AnyAsync(j => j.ProductionNumber == "202670"));
    }

    [Fact]
    public async Task Planeamento_EscolherUmaProducao_AbreAFolhaNova_SemAFichaAntiga()
    {
        SeedUser("4425", "Chefe planeamento", "provider-folha-planeamento", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4425", "Folha1234!");

        // A lista do dia e a escolha levam à folha nova, com o dia.
        var dia = await GetPageHtmlAsync(client, "/JobOn?ano=2026&mes=10&dia=22");
        Assert.Contains($"data-dmo-open=\"/JobOn/Folha?jobon_id={id}&amp;dia=2026-10-22\"", dia, StringComparison.Ordinal);
        var escolher = await client.GetAsync($"/JobOn?ano=2026&mes=10&dia=22&jobon_id={id}");
        Assert.Equal(HttpStatusCode.Redirect, escolher.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={id}&dia=2026-10-22", escolher.Headers.Location!.ToString());

        // A ficha antiga («Ficha da produção», «Preparar produção») não aparece no Planeamento.
        Assert.DoesNotContain("Ficha da produção", dia, StringComparison.Ordinal);
        Assert.DoesNotContain("Ferramentas usadas", dia, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"lightFicha\"", dia, StringComparison.Ordinal);
        Assert.DoesNotContain("Preparar produção", dia, StringComparison.Ordinal);

        // Uma produção que não existe fica no Planeamento com o aviso, sem inventar folha.
        var naoExiste = await GetPageHtmlAsync(client, "/JobOn?jobon_id=999999");
        Assert.Contains("A produção indicada não existe.", naoExiste, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolhaNova_TP_ODiametroSemPS_ECampoIgual()
    {
        // Diogo, 23/10/2026: «remover o PS»; só o rótulo muda, o campo é o mesmo.
        SeedUser("4426", "Chefe TP", "provider-folha-tp", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4426", "Folha1234!");

        foreach (var url in new[] { $"{FolhaPath}?jobon_id={id}", $"{FolhaPath}?jobon_id={id}&editar=1" })
        {
            var html = await GetPageHtmlAsync(client, url);
            var tp = Zona(html, "data-jof-familia=\"TP\"");
            tp = tp[..tp.IndexOf("</article>", StringComparison.Ordinal)];
            Assert.Contains("<dt>Diâmetro</dt>", tp, StringComparison.Ordinal);
            Assert.DoesNotContain("Diâmetro PS", tp, StringComparison.Ordinal);
        }
        var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        Assert.Contains("name=\"Folha.TpDiametroPs\"", editar, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"TP Diâmetro\"", editar, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { "202601" }, "202602")]
    [InlineData(new[] { "202603", "202605", "202602" }, "202606")]
    [InlineData(new[] { "0099", "X12" }, "0100")]
    [InlineData(new[] { "X12", "" }, null)]
    public void Duplicar_AProducaoSugerida_EASeguinteAMaior(string[] producoes, string? esperada) =>
        Assert.Equal(esperada, DMO.Alpha.Web.Pages.JobOn.FolhaModel.ProximaProducao(producoes));
}
