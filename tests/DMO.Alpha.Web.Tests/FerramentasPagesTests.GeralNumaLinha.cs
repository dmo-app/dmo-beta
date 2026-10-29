using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    /// <summary>
    /// Garante (DMO-608, Diogo 29/10/2026: «linhas passa a ficar na vertical,
    /// B1, C3 em baixo de B1, reduzir quantidade, tentar meter alinhado»): no
    /// Geral da ficha, cada linha é um bloco próprio (uma por baixo da outra)
    /// e a Utilização está no mesmo quadro dos outros dados. A página vai para o
    /// Chromium (tests/visual/cobertura_a2_browser.py), que confirma que tudo
    /// fica numa só linha, também à largura do painel lateral.
    /// </summary>
    [Fact]
    public async Task Ficha_Geral_LinhasNaVertical_EUtilizacaoNaMesmaLinha()
    {
        SeedUser("4608", "Ferramentas Geral", "provider-ferr-geral-608", "FerrGeral608!");
        string toolId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b1 = new Machine { Code = "B1" };
            var c3 = new Machine { Code = "C3" };
            db.Machines.AddRange(b1, c3);
            var tool = new Tool { ToolId = "Tgeral60801", Type = ToolType.Cm, Reference = "ST100", Lot = "8", BqAccountedQuantity = 96, AssociatedMfReference = "1075" };
            db.Tools.Add(tool);
            await db.SaveChangesAsync();
            db.ToolMachineLinks.AddRange(
                new ToolMachineLink { ToolId = tool.ToolId, MachineId = b1.Id },
                new ToolMachineLink { ToolId = tool.ToolId, MachineId = c3.Id });
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = tool.ToolId, Percentagem = 93.8m, Origem = "manual", RegistadaEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            toolId = tool.ToolId;
        }

        using var client = await LoginClientAsync("4608", "FerrGeral608!");
        var html = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");

        Assert.Contains("id=\"ferrGeral\"", html);
        Assert.Matches("id=\"ferrLinhas\"><span>Linhas</span><strong><span>B1</span><span>C3</span></strong>", html);
        Assert.Contains("id=\"ferrUtilizacao\"", html);
        ExportarParaOBrowser.Gravar("cobertura-a2-ferr-ficha-geral.html", html);
    }
}
