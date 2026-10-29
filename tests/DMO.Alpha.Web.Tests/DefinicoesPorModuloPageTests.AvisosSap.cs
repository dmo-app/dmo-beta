using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task Sap_AFichaMostraFornecedorECompraNaInformacaoOpcional_EOSimGravaAPeca()
    {
        var id = SeedToolComNomeTecnico("TESTESAP663", "7");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = db.Tools.Single(t => t.ToolId == id);
            tool.SapPecas = 98;
            tool.SapFornecedor = "Fornecedor de teste";
            tool.SapDataAquisicao = new DateOnly(2025, 12, 19);
            await db.SaveChangesAsync();
        }
        using var client = await LoginOperatorAsync("7663", DmoCapabilities.Ferramentas);
        var url = "/Ferramentas/Ficha?tool_id=" + id;
        var html = await client.GetStringAsync(url);
        var opcional = ZonaUsoSap(html, "ferr-edit-opcional");
        Assert.Contains("Fornecedor de teste", opcional);
        Assert.Contains("19/12/2025", opcional);
        Assert.Contains("SAP tem 98", ZonaUsoSap(html, "sap-aviso-pecas"));
        var (acao, campos) = EnvioDoFormulario.Enviar(html, [], idDoBotao: "sap-pecas-sim");
        var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        using var leitura = _factory.Services.CreateScope();
        var gravada = await leitura.ServiceProvider.GetRequiredService<DmoDbContext>().Tools.AsNoTracking().SingleAsync(t => t.ToolId == id);
        Assert.Equal(98, gravada.AnQuantity);
        var depois = await client.GetStringAsync(url);
        Assert.DoesNotContain("id=\"sap-aviso-pecas\"", depois);
    }

    [Fact]
    public async Task Sap_ONaoMantemAQuantidadeENaoVoltaAMostrarOMesmoAviso()
    {
        var id = SeedToolComNomeTecnico("TESTESAP664", "7");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = db.Tools.Single(t => t.ToolId == id);
            tool.AnQuantity = 12;
            tool.SapPecas = 98;
            await db.SaveChangesAsync();
        }
        using var client = await LoginOperatorAsync("7664", DmoCapabilities.Ferramentas);
        var url = "/Ferramentas/Ficha?tool_id=" + id;
        var html = await client.GetStringAsync(url);
        Assert.Contains("SAP tem 98", ZonaUsoSap(html, "sap-aviso-pecas"));
        var (acao, campos) = EnvioDoFormulario.Enviar(html, [], idDoBotao: "sap-pecas-nao");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);
        using var leitura = _factory.Services.CreateScope();
        var toolLida = await leitura.ServiceProvider.GetRequiredService<DmoDbContext>().Tools.AsNoTracking().SingleAsync(t => t.ToolId == id);
        Assert.Equal(12, toolLida.AnQuantity);
        Assert.Equal(98, toolLida.SapPecasIgnoradas);
        Assert.DoesNotContain("id=\"sap-aviso-pecas\"", await client.GetStringAsync(url));
    }

    [Fact]
    public async Task Sap_UmaDecisaoSemAntiforgeryNaoGrava()
    {
        using var client = await LoginOperatorAsync("7665", DmoCapabilities.Ferramentas);
        var resposta = await client.PostAsync("/Ferramentas/AvisosSap", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["tool_id"] = "nao-existe", ["versao"] = "0", ["campo"] = "pecas", ["decisao"] = "sim" }));
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
