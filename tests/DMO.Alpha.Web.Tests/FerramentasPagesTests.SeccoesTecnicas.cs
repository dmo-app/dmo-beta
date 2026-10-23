using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DMO.Alpha.Web.Pages.Ferramentas;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // Diogo, 20/10/2026: no registo, «Tampão» passa a título com Diâmetro,
    // Raio e Calote por baixo; cada secção da informação técnica tem a dica
    // de onde a app usa os dados; a Quantidade do lote vem antes da Ref. MF
    // (raramente usada) e não diz «opcional».
    [Fact]
    public async Task Registo_InformacaoTecnicaEmSeccoesComDica_QuantidadeAntesDaRefMf()
    {
        SeedUser("4993", "Ferramentas Secções", "provider-ferr-seccoes", "FerrSeccoes1!");
        using var client = await LoginClientAsync("4993", "FerrSeccoes1!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, IndexPath + "?criar=1&type=CM"));

        Assert.True(Posicao(html, "id=\"ferr-new-qty\"") < Posicao(html, "id=\"ferr-new-mf\""));
        var quantidade = System.Text.RegularExpressions.Regex.Match(html, "<input id=\"ferr-new-qty\"[^>]*>").Value;
        Assert.StartsWith("<input id=\"ferr-new-qty\" name=\"bq_quantity\"", quantidade, StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder", quantidade, StringComparison.Ordinal);
        Assert.DoesNotContain("Tampão D (mm)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Gargalo (mm)", html, StringComparison.Ordinal);
        var tampao = html[Posicao(html, ">Tampão<")..];
        Assert.True(Posicao(tampao, ">Diâmetro (mm)</label>") < Posicao(tampao, ">Calote (mm)</label>"));
        // O raio deixou de ser pedido: D e calote chegam para o volume.
        Assert.DoesNotContain(">Raio (mm)</label>", tampao);
        Assert.DoesNotContain("name=\"tp_raio\"", tampao);
        foreach (var dica in new[] { AjudaTecnica.Peso, AjudaTecnica.Gargalo, AjudaTecnica.Tampao })
            Assert.Contains($"<span class=\"dmo-help-tip\" role=\"tooltip\">{dica}</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"dmo-field ferr-w-processo\"", html, StringComparison.Ordinal);
    }

    // «nao pode ser optional pq da erro se nao tiver» (Diogo, 20/10/2026):
    // a BQ grava sem quantidade (só se bloqueia o impossível), mas a ficha
    // avisa que a Fabricação das Boquilhas fica por calcular, com o link
    // para o Editar.
    [Fact]
    public async Task Ficha_BqSemQuantidade_AvisaQueAFabricacaoNaoECalculada()
    {
        SeedUser("4994", "Ferramentas Sem Qtd", "provider-ferr-sem-qtd", "FerrSemQtd1!");
        using var client = await LoginClientAsync("4994", "FerrSemQtd1!");
        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1&type=BQ");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "BQ"), ("reference", "Z991"), ("lot", "4"), ("bq_quantity", ""),
            ("quantity_manual", "1"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, response.Headers.Location!.ToString()));
        Assert.Contains("Sem quantidade do lote: nas Boquilhas, a Fabricação desta BQ não é calculada.", ficha, StringComparison.Ordinal);
        Assert.Contains("href=\"#ferrEditar\" id=\"ferr-sem-quantidade-editar\">Acrescentar a quantidade</a>", ficha, StringComparison.Ordinal);
    }

    // «tem de existir uma pagina de crir ferramenras» (Diogo, 20/10/2026): o
    // Criar e o Editar da ficha são o mesmo formulário: os mesmos campos, pela
    // mesma ordem e com os mesmos nomes; o Editar também grava o Nome técnico.
    [Fact]
    public async Task CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico()
    {
        SeedUser("4995", "Ferramentas Um Form", "provider-ferr-um-form", "FerrUmForm1!");
        using var client = await LoginClientAsync("4995", "FerrUmForm1!");
        var toolId = await SeedRealToolAsync();

        var criar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, IndexPath + "?criar=1&type=CM"));
        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}"));
        string[] Rotulos(string html, string prefixo) => System.Text.RegularExpressions.Regex
            .Matches(html, $"<label for=\"{prefixo}-[a-z-]+\">([^<]+)<")
            .Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(
            ["Tipo", "Referência", "Lote", "Quantidade do lote", "% uso", "Ref. MF associada", "Nome técnico (produção)"],
            Rotulos(criar, "ferr-new").Take(7));
        // A ficha acrescenta a Fábrica de origem (o % uso também está no Criar, 23/10/2026).
        var doCriar = Rotulos(criar, "ferr-new").Take(7).ToList();
        Assert.Equal(doCriar.Append("Fábrica de origem"), Rotulos(ficha, "ferr-edit"));
        // A ficha da CM só tem os campos da CM (a Marisa é da BQ).
        Assert.Equal(Rotulos(criar, "ferr-new").Skip(7).Where(r => r != "Marisa (cm³)"), Rotulos(ficha, "ferr-tech"));

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"),
            ("technical_name", "m107544mfnnv"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("facts_refusal", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("M107544MFNNV", (await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId)).TechnicalName);
    }

    // Diogo, 22/10/2026: «Linhas compatíveis» sem «Máquinas» nem «(opcional)»;
    // a Ref. MF, o nome técnico e a Fábrica de origem num bloco «Informação
    // opcional»; nunca «SAP» nos rótulos.
    [Fact]
    public async Task Ficha_Editar_LinhasCompativeis_InformacaoOpcionalComFabricaDeOrigem()
    {
        SeedUser("4996", "Ferramentas Opcional", "provider-ferr-opcional", "FerrOpcional1!");
        using var client = await LoginClientAsync("4996", "FerrOpcional1!");
        var toolId = await SeedRealToolAsync();

        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}"));
        Assert.Contains(">Linhas compatíveis</span>", ficha, StringComparison.Ordinal);
        Assert.Contains("<span>Linhas</span>", ficha, StringComparison.Ordinal);
        Assert.DoesNotContain("Máquinas / linhas", ficha, StringComparison.Ordinal);
        Assert.DoesNotContain("(SAP)", ficha, StringComparison.Ordinal);
        var bloco = ficha[Posicao(ficha, "id=\"ferr-edit-opcional\"")..];
        bloco = bloco[..bloco.IndexOf("</fieldset>", StringComparison.Ordinal)];
        Assert.Contains(">Informação opcional</legend>", bloco, StringComparison.Ordinal);
        Assert.True(Posicao(bloco, "id=\"ferr-edit-mf\"") < Posicao(bloco, "id=\"ferr-edit-technical\""));
        Assert.True(Posicao(bloco, "id=\"ferr-edit-technical\"") < Posicao(bloco, "id=\"ferr-edit-fabrica\""));
        // Ao lado dos campos, depois do traço vertical da Informação técnica.
        Assert.True(Posicao(ficha, "id=\"ferr-edit-machines\"") < Posicao(ficha, "class=\"ferr-tech-sep ferr-editar-sep\""));
        Assert.True(Posicao(ficha, "class=\"ferr-tech-sep ferr-editar-sep\"") < Posicao(ficha, "id=\"ferr-edit-opcional\""));

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"),
            ("fabrica_origem", " mg "), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal("MG", (await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId)).FabricaOrigem);
        }
        var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}"));
        Assert.Matches("<input id=\"ferr-edit-fabrica\"[^>]*value=\"MG\"", depois);
    }

    // O % uso no Editar, ao lado da Quantidade do lote (Diogo, 22/10/2026):
    // escrito à mão, opcional, gravado com o «Guardar» como uma leitura nova
    // (manual) que fecha o «Utilização por atualizar»; vazio ou igual não
    // grava; 100 ou mais grava sem aviso; ilegível avisa e não grava.
    [Fact]
    public async Task Ficha_PercentagemDeUso_NoEditar_GravaComOGuardar_SoAvisos()
    {
        SeedUser("4997", "Ferramentas Uso", "provider-ferr-uso", "FerrUso1!");
        using var client = await LoginClientAsync("4997", "FerrUso1!");
        var toolId = await SeedRealToolAsync();

        var sem = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}"));
        Assert.DoesNotContain("ferrPercentagemUso", sem, StringComparison.Ordinal);
        Assert.Matches("<input id=\"ferr-edit-uso\" name=\"uso_percentagem\"[^>]*placeholder=\"sem registo\" class=\"ferr-sem-registo-campo\"", sem);
        Assert.True(Posicao(sem, "id=\"ferr-edit-qty\"") < Posicao(sem, "id=\"ferr-edit-uso\""));
        Assert.True(Posicao(sem, "id=\"ferr-edit-machines\"") < Posicao(sem, "id=\"ferr-edit-type\""));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.Add(new DMO.Alpha.Core.Tools.FerramentaUtilizacao { ToolId = toolId, Percentagem = 62.4m, RegistadaEmUtc = DateTime.UtcNow.AddDays(-1) });
            db.FerramentaLembretesUtilizacao.Add(new DMO.Alpha.Core.Tools.FerramentaLembreteUtilizacao { ToolId = toolId, CriadoEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var com = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}"));
        Assert.Matches("<input id=\"ferr-edit-uso\" name=\"uso_percentagem\"[^>]*value=\"62\"", com);
        Assert.DoesNotContain("class=\"ferr-sem-registo-campo\"", com, StringComparison.Ordinal);

        int Leituras()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<DmoDbContext>().FerramentaUtilizacoes.Count(u => u.ToolId == toolId);
        }
        async Task<string> GuardarAsync(string uso)
        {
            var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
            var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
                ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"),
                ("uso_percentagem", uso), ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return WebUtility.HtmlDecode(await GetPageHtmlAsync(client, response.Headers.Location!.ToString()));
        }

        await GuardarAsync("");
        await GuardarAsync("62");
        Assert.Equal(1, Leituras());

        var ilegivel = await GuardarAsync("muito");
        Assert.Equal(1, Leituras());
        Assert.Contains("O % uso «muito» não é um número inteiro positivo e não foi gravado", ilegivel, StringComparison.Ordinal);

        var depois = await GuardarAsync("75");
        Assert.Equal(2, Leituras());
        Assert.Matches("<input id=\"ferr-edit-uso\" name=\"uso_percentagem\"[^>]*value=\"75\"", depois);
        Assert.DoesNotContain("id=\"ferrUtilizacaoLembrete\"", depois, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ferrUsoAviso\"", depois, StringComparison.Ordinal);
        using (var scope = _factory.Services.CreateScope())
        {
            var ultima = scope.ServiceProvider.GetRequiredService<DmoDbContext>().FerramentaUtilizacoes
                .Where(u => u.ToolId == toolId).OrderByDescending(u => u.Id).First();
            Assert.Equal((75m, DMO.Alpha.Core.Tools.FerramentaUtilizacao.OrigemManual, (long?)null), (ultima.Percentagem, ultima.Origem, ultima.Consumo));
        }

        // 100 ou mais grava sem aviso nenhum (Diogo, 23/10/2026: só a cor muda).
        var cem = await GuardarAsync("100");
        Assert.Equal(3, Leituras());
        Assert.DoesNotContain("id=\"ferrUsoAviso\"", cem, StringComparison.Ordinal);
        var centoEUm = await GuardarAsync("101");
        Assert.Equal(4, Leituras());
        Assert.DoesNotContain("id=\"ferrUsoAviso\"", centoEUm, StringComparison.Ordinal);

        var acima = await GuardarAsync("120%");
        Assert.Equal(5, Leituras());
        Assert.DoesNotContain("id=\"ferrUsoAviso\"", acima, StringComparison.Ordinal);
    }
}
