using System.Text.RegularExpressions;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    /// <summary>
    /// As ferramentas da produção semeada com nominal do gargalo: só assim o
    /// cartão desenha a linha nova com o campo «numero» (sem nominal não há
    /// leituras, Diogo 17/10/2026).
    /// </summary>
    private async Task ComNominalAsync(params string[] toolIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        foreach (var toolId in toolIds)
        {
            var contexto = await db.ToolTechnicalContexts.SingleOrDefaultAsync(c => c.ToolId == toolId);
            if (contexto is null)
            {
                db.ToolTechnicalContexts.Add(new DMO.Alpha.Core.Tools.ToolTechnicalContext { ToolId = toolId, DiametroGargaloMm = 70.00m });
            }
            else
            {
                contexto.DiametroGargaloMm = 70.00m;
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Envia o formulário que a página desenha, preenchido pelo id dos campos, e devolve para onde levou.</summary>
    private static async Task<string> EnviarFormularioAsync(HttpClient client, string html, params (string IdDoCampo, string Valor)[] campos)
    {
        var (acao, envio) = EnvioDoFormulario.Preencher(html, campos);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(envio));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!.OriginalString;
    }

    /// <summary>A zona das mensagens das medições; falha se a página não a tem.</summary>
    private static string ZonaAvisos(string html)
    {
        var zona = Regex.Match(html, "<div id=\"pegAvisos\"[^>]*>([\\s\\S]*?)\n</div>");
        Assert.True(zona.Success, "A página não tem a zona #pegAvisos.");
        return zona.Groups[1].Value;
    }

    // Diogo, 21/10/2026: cada leitura dos Pegamentos leva o número da CM, da
    // BQ ou do MF lido (ex.: CM n.º 61), opcional, numa coluna «N.º CM».
    [Fact]
    public async Task NumeroFerramenta_RegistarComNumero_GravaEAFolhaMostraNaColunaNoCm()
    {
        SeedUser("4821", "Pegamentos Numero", "provider-peg-numero", "PegNumero1!");
        using var client = await LoginClientAsync("4821", "PegNumero1!");
        var controloId = await SeedRealProductionAsync();
        await ComNominalAsync("T3");
        var url = $"{PagePath}?controlo_id={controloId}";
        // O formulário que a página desenha (regra 9): prova que o envio real leva o «numero».
        var pagina = await GetPageHtmlAsync(client, url);
        var (_, envio) = EnvioDoFormulario.Preencher(pagina, ("numero-Cm", "  61 "), ("costura-Cm", "70.10"), ("contra-Cm", "69.90"));
        Assert.Contains(("numero", "  61 "), envio);
        Assert.Equal(url, await EnviarFormularioAsync(client, pagina, ("numero-Cm", "  61 "), ("costura-Cm", "70.10"), ("contra-Cm", "69.90")));

        using (var scope = _factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .AsNoTracking().SingleAsync();
            Assert.Equal("61", row.NumeroFerramenta);
        }

        var html = await GetPageHtmlAsync(client, url);
        var cartao = Regex.Match(html, "data-peg-component=\"CM\"[\\s\\S]*?</table>").Value;
        Assert.Contains("<th>N.º CM</th>", cartao);
        var linha = Regex.Match(cartao, "<tr id=\"peg-linha-\\d+\">([\\s\\S]*?)</tr>").Groups[1].Value;
        Assert.Matches("<td>1</td>\\s*<td>61<a class=\"peg-valor\" href=\"[^\"]*campo=numero\"", linha);
        // A linha nova tem o campo do número.
        Assert.Matches("<input id=\"numero-Cm\" name=\"numero\" form=\"peg-registar-CM\"[^>]*maxlength=\"20\"", cartao);
        // A Revisão mostra o número também.
        SeedApprover("4822", "provider-peg-numero-chefe", "PegNumeroChefe1!");
        using var chefe = await LoginClientAsync("4822", "PegNumeroChefe1!");
        var consulta = await GetPageHtmlAsync(chefe, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos");
        Assert.Contains("<th>N.º CM</th>", consulta);
        Assert.Matches("<td>1</td>\\s*<td>61</td>", consulta);
    }

    [Fact]
    public async Task NumeroFerramenta_VariasLinhas_CadaUmaComOSeuNumero_SoONumeroContaComoVazia()
    {
        SeedUser("4823", "Pegamentos Numero", "provider-peg-numeros", "PegNumeros1!");
        using var client = await LoginClientAsync("4823", "PegNumeros1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        var token = await GetVerificationTokenAsync(client, url);
        using var posted = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("component", "CM"), ("gargalo", "70.00"),
            ("costura", "70.10"), ("contra_costura", "69.90"), ("numero", "61"),
            ("costura", "70.05"), ("contra_costura", ""), ("numero", ""),
            ("costura", ""), ("contra_costura", ""), ("numero", "99"),
            ("costura", "70,02"), ("contra_costura", ""), ("numero", "A12-" + new string('x', 30)),
            ("__RequestVerificationToken", token)));
        var destino = posted.Headers.Location!.OriginalString;
        Assert.StartsWith(url, destino);

        using (var scope = _factory.Services.CreateScope())
        {
            var rows = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .AsNoTracking().OrderBy(m => m.RowPosition).ToListAsync();
            Assert.Equal(new[] { 70.10m, 70.05m, 70.02m }, rows.Select(r => r.CosturaMm));
            Assert.Equal(new[] { "61", null, "A12-" + new string('x', 16) }, rows.Select(r => r.NumeroFerramenta));
        }

        // Nada se perde em silêncio: a linha 3 (só o número) e o corte da linha 4 ficam ditos.
        var avisos = ZonaAvisos(await GetPageHtmlAsync(client, destino));
        Assert.Contains("A linha 3 só tinha o número e não foi registada.", avisos);
        Assert.Contains("O número da linha 4 tinha mais de 20 caracteres e foi cortado.", avisos);
        Assert.DoesNotContain("O número da linha 1", avisos);
    }

    [Fact]
    public async Task NumeroFerramenta_Corrigir_ONumeroAbreComFocoEGrava()
    {
        SeedUser("4824", "Pegamentos Numero", "provider-peg-numero-corr", "PegNumCorr1!");
        using var client = await LoginClientAsync("4824", "PegNumCorr1!");
        var controloId = await SeedRealProductionAsync();
        await ComNominalAsync("T3");
        var url = $"{PagePath}?controlo_id={controloId}";
        Assert.Equal(url, await EnviarFormularioAsync(client, await GetPageHtmlAsync(client, url), ("costura-Cm", "70.10"), ("contra-Cm", "69.90")));

        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .Select(m => m.Id).SingleAsync();
        }

        var editing = await GetPageHtmlAsync(client, $"{url}&editar_medicao={id}&campo=numero");
        Assert.Matches($"<input id=\"editar-numero-{id}\" name=\"numero\" form=\"peg-editar-{id}\"[^>]*autofocus", editing);
        Assert.DoesNotMatch($"<input id=\"editar-costura-{id}\"[^>]*autofocus", editing);
        // O formulário da correção que a página desenha (regra 9) leva o «numero».
        var (_, envio) = EnvioDoFormulario.Preencher(editing, ($"editar-numero-{id}", " 62 "));
        Assert.Contains(("numero", " 62 "), envio);
        Assert.Equal(url, await EnviarFormularioAsync(client, editing, ($"editar-numero-{id}", " 62 ")));

        using (var scope = _factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .AsNoTracking().SingleAsync();
            Assert.Equal("62", row.NumeroFerramenta);
            Assert.Equal(70.10m, row.CosturaMm);
        }

        // Apagar o número: fica vazio («—»).
        editing = await GetPageHtmlAsync(client, $"{url}&editar_medicao={id}&campo=numero");
        Assert.Equal(url, await EnviarFormularioAsync(client, editing, ($"editar-numero-{id}", "")));
        var html = await GetPageHtmlAsync(client, url);
        Assert.Matches("<td>1</td>\\s*<td>—<a class=\"peg-valor\"", html);
    }

    // Limite de 20 caracteres (regra 3: 19, 20 e 21). «Só avisos»: o 21.º é
    // cortado, mas a página di-lo depois de gravar (revisão dos testes, 21/10/2026).
    [Fact]
    public async Task NumeroFerramenta_Limite20_19e20GravamInteiros_21ECortadoComAviso()
    {
        SeedUser("4825", "Pegamentos Numero", "provider-peg-numero-lim", "PegNumLim1!");
        using var client = await LoginClientAsync("4825", "PegNumLim1!");
        var controloId = await SeedRealProductionAsync();
        await ComNominalAsync("T3");
        var url = $"{PagePath}?controlo_id={controloId}";
        var n19 = "N" + new string('1', 18);
        var n20 = "N" + new string('2', 19);
        var n21 = "N" + new string('3', 20);

        var destino19 = await EnviarFormularioAsync(client, await GetPageHtmlAsync(client, url), ("numero-Cm", n19), ("costura-Cm", "70.10"));
        Assert.Equal(url, destino19);
        var destino20 = await EnviarFormularioAsync(client, await GetPageHtmlAsync(client, url), ("numero-Cm", n20), ("costura-Cm", "70.10"));
        Assert.Equal(url, destino20);
        var avisos20 = ZonaAvisos(await GetPageHtmlAsync(client, destino20));
        Assert.DoesNotContain("foi cortado", avisos20);
        var destino21 = await EnviarFormularioAsync(client, await GetPageHtmlAsync(client, url), ("numero-Cm", n21), ("costura-Cm", "70.10"));

        using (var scope = _factory.Services.CreateScope())
        {
            var numeros = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .AsNoTracking().OrderBy(m => m.RowPosition).Select(m => m.NumeroFerramenta).ToListAsync();
            Assert.Equal(new[] { n19, n20, n21[..20] }, numeros);
        }

        var avisos21 = ZonaAvisos(await GetPageHtmlAsync(client, destino21));
        Assert.Contains("O número da linha 1 tinha mais de 20 caracteres e foi cortado.", avisos21);

        // Na correção também: o corte é dito.
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .OrderBy(m => m.RowPosition).Select(m => m.Id).FirstAsync();
        }
        var editing = await GetPageHtmlAsync(client, $"{url}&editar_medicao={id}&campo=numero");
        var destinoCorrecao = await EnviarFormularioAsync(client, editing, ($"editar-numero-{id}", n21));
        Assert.StartsWith(url, destinoCorrecao);
        var avisosCorrecao = ZonaAvisos(await GetPageHtmlAsync(client, destinoCorrecao));
        Assert.Contains("O número da leitura 1 da CM tinha mais de 20 caracteres e foi cortado.", avisosCorrecao);
        using (var scope = _factory.Services.CreateScope())
        {
            var numero = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .AsNoTracking().Where(m => m.Id == id).Select(m => m.NumeroFerramenta).SingleAsync();
            Assert.Equal(n21[..20], numero);
        }
    }

    // O cartão da BQ e o do MF: cada um diz o seu número e grava-o.
    [Theory]
    [InlineData("BQ", "Bq", "BQ-1")]
    [InlineData("MF", "Mf", "MF-TOOL-9")]
    public async Task NumeroFerramenta_CartaoBqOuMf_ColunaComOSeuNomeEGrava(string componente, string sufixo, string toolId)
    {
        SeedUser("4826", "Pegamentos Numero", "provider-peg-numero-" + sufixo, "PegNumBq1!");
        using var client = await LoginClientAsync("4826", "PegNumBq1!");
        var controloId = await SeedRealProductionAsync();
        await ComNominalAsync(toolId);
        var url = $"{PagePath}?controlo_id={controloId}";
        Assert.Equal(url, await EnviarFormularioAsync(client, await GetPageHtmlAsync(client, url),
            ($"numero-{sufixo}", "7"), ($"costura-{sufixo}", "70.10")));

        using (var scope = _factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .AsNoTracking().SingleAsync();
            Assert.Equal("7", row.NumeroFerramenta);
            Assert.Equal(sufixo, row.Component.ToString());
        }

        var html = await GetPageHtmlAsync(client, url);
        var cartao = Regex.Match(html, $"data-peg-component=\"{componente}\"[\\s\\S]*?</table>").Value;
        Assert.Contains($"<th>N.º {componente}</th>", cartao);
        Assert.Matches("<td>1</td>\\s*<td>7<a class=\"peg-valor\"", cartao);
        var cartaoCm = Regex.Match(html, "data-peg-component=\"CM\"[\\s\\S]*?</table>").Value;
        Assert.Contains("<th>N.º CM</th>", cartaoCm);
        Assert.DoesNotContain($"<th>N.º {componente}</th>", cartaoCm);
    }

    // Uma linha só com o número não é registada, mas o aviso di-lo.
    [Fact]
    public async Task NumeroFerramenta_LinhaSoComONumero_NaoERegistadaEAvisa()
    {
        SeedUser("4827", "Pegamentos Numero", "provider-peg-numero-so", "PegNumSo1!");
        using var client = await LoginClientAsync("4827", "PegNumSo1!");
        var controloId = await SeedRealProductionAsync();
        await ComNominalAsync("T3");
        var url = $"{PagePath}?controlo_id={controloId}";
        var destino = await EnviarFormularioAsync(client, await GetPageHtmlAsync(client, url), ("numero-Cm", "61"));
        Assert.StartsWith(url, destino);

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements.AnyAsync());
        }
        var avisos = ZonaAvisos(await GetPageHtmlAsync(client, destino));
        Assert.Contains("A linha 1 só tinha o número e não foi registada.", avisos);
    }
}
