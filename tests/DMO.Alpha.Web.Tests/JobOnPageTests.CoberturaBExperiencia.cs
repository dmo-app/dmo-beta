using System.Net;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A EXPERIÊNCIA NO CRIAR (DMO-399, Diogo, 26/10/2026): o Job On novo tem os
/// dois botões, Produção e Experiência; a experiência abre em branco com o
/// nº AAAA00; no Duplicar não existe.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task FolhaNova_TemOsDoisBotoes_ProducaoEExperiencia_ComAProducaoEscolhida()
    {
        SeedUser("4871", "Chefe experiência", "provider-folha-experiencia-1", "Folha1234!");
        await SeedJobOnCompletoAsync();
        var b2 = await MachineIdAsync("B2");
        using var client = await LoginClientAsync("4871", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?novo=1&machine_id={b2}&inicio_em=2026-11-02");

        Assert.Contains("name=\"tipo\" value=\"producao\" checked", html, StringComparison.Ordinal);
        Assert.Contains("<span>Produção</span>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"tipo\" value=\"experiencia\"", html, StringComparison.Ordinal);
        Assert.Contains("<span>Experiência</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolhaNova_Experiencia_AbreEmBrancoComONumeroAnoZeroZero_EMarcadaComoExperiencia()
    {
        SeedUser("4872", "Chefe experiência", "provider-folha-experiencia-2", "Folha1234!");
        await SeedJobOnCompletoAsync();
        var b2 = await MachineIdAsync("B2");
        using var client = await LoginClientAsync("4872", "Folha1234!");

        var escolha = await GetPageHtmlAsync(client, $"{FolhaPath}?novo=1&machine_id={b2}&inicio_em=2026-11-02");
        var (acao, campos) = EnvioDoFormulario.Enviar(escolha, [("jofReferencia", "9999t1")], radios: [("tipo", "experiencia")]);
        var consulta = string.Join("&", campos.Select(c => $"{Uri.EscapeDataString(c.Nome)}={Uri.EscapeDataString(c.Valor)}"));
        var aberta = await GetPageHtmlAsync(client, $"{acao}?{consulta}");

        Assert.Contains("data-jof-folha=\"editar\" data-jof-criar=\"true\"", aberta, StringComparison.Ordinal);
        Assert.Contains("id=\"lightReference\" name=\"reference\" type=\"text\" value=\"9999T1\"", aberta, StringComparison.Ordinal);
        Assert.Contains("id=\"lightProduction\" name=\"production_number\" type=\"text\" value=\"202600\"", aberta, StringComparison.Ordinal);
        Assert.Contains("class=\"jof-experiencia\" role=\"status\">Experiência</span>", aberta, StringComparison.Ordinal);
        // Já criada a folha, não há escolha de tipo: o tipo segue escondido para o Criar.
        Assert.DoesNotContain("name=\"tipo\" value=\"producao\"", aberta, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"tipo\" value=\"experiencia\" />", aberta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolhaNova_Duplicar_NaoTemOBotaoExperiencia()
    {
        SeedUser("4873", "Chefe experiência", "provider-folha-experiencia-3", "Folha1234!");
        var origem = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4873", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?duplicar={origem}&inicio_em=2026-11-05");

        Assert.Contains(">Novo Job On · duplicado de 202602</span>", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"tipo\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"experiencia\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("jof-experiencia", html, StringComparison.Ordinal);
    }
}
