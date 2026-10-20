using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Teste do agente de 19/10/2026 (T04fe55b): os filtros da Revisão trocam
    // só a lista, e o dia do calendário abria com os filtros de quando a
    // página abriu. O calendário diz ao browser (data-dmo-filtros) de que
    // formulário tirar os filtros atuais; o teste do JavaScript
    // (calendario-filtros.test.cjs) prova o clique. Aqui: o formulário e os
    // campos que o calendário nomeia existem na página que o servidor desenha.
    [Fact]
    public async Task Revisao_CalendarioNomeiaOFormularioDosFiltros_ComOsCamposQueOServidorGuarda()
    {
        await SeedRealProductionAsync();
        SeedChefe("4799", "provider-revisao-calendario-filtros", "RevisaoCalendario1!");
        using var client = await LoginClientAsync("4799", "RevisaoCalendario1!");

        var pagina = await GetPageHtmlAsync(client, "/Controlo/Approve?estado=pendente&maquina=B1");
        var calendario = Regex.Match(pagina, "<div[^>]*id=\"dmoCalendario\"[^>]*>");
        Assert.True(calendario.Success, "A página não tem o calendário.");
        var filtros = Regex.Match(calendario.Value, "data-dmo-filtros=\"(?<v>[^\"]+)\"");
        Assert.True(filtros.Success, "O calendário não diz de onde vêm os filtros.");
        var partes = filtros.Groups["v"].Value.Split(' ');
        var form = Regex.Match(pagina, $"<form[^>]*id=\"{Regex.Escape(partes[0])}\"[\\s\\S]*?</form>");
        Assert.True(form.Success, $"Não há o formulário #{partes[0]}.");
        Assert.Equal(new[] { "procura", "estado", "maquina" }, partes[1..]);
        foreach (var campo in partes[1..])
        {
            Assert.Matches($"<(input|select)[^>]*name=\"{campo}\"", form.Value);
        }
        // Os mesmos filtros que o servidor já põe nos dias (o caminho sem JavaScript).
        Assert.Matches("id=\"dmoDia-1\"[^>]*href=\"[^\"]*estado=pendente[^\"]*maquina=B1|href=\"[^\"]*estado=pendente[^\"]*maquina=B1[^\"]*\"[^>]*id=\"dmoDia-1\"", pagina);
    }
}
