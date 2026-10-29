using System.Text.RegularExpressions;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// DMO-404 (beta.90): em «Procurar boquilha» o filtro de Lote vem logo a
/// seguir à pesquisa por Referência e filtra a lista por esse lote.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task ProcurarBoquilha_OFiltroLoteVemLogoDepoisDaReferencia_EFiltraPorEsseLote()
    {
        SeedUser("4924", "Lote BQ", "catalog-lote", "CatalogPass1!", withFerramentas: true);
        using var client = await LoginClientAsync("4924", "CatalogPass1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            db.Machines.Add(machine);
            foreach (var lote in new[] { "24/01", "24/02", "25/01" })
            {
                var tool = new Tool { ToolId = $"lote-{lote.Replace("/", "")}", Type = ToolType.Bq, Reference = "T173", Lot = lote, BqAccountedQuantity = 120 };
                db.Tools.Add(tool);
                db.ToolMachineLinks.Add(new ToolMachineLink { Tool = tool, Machine = machine });
            }
            await db.SaveChangesAsync();
        }

        var todas = await client.GetStringAsync("/Boquilhas?ficheiros=1&q=t173");
        Assert.Equal(3, Regex.Matches(todas, "name=\"bq_tool\"").Count);
        Assert.True(Posicao(todas, "for=\"bq-tool-search\"") < Posicao(todas, "for=\"bq-tool-lot\""));
        Assert.True(Posicao(todas, "for=\"bq-tool-lot\"") < Posicao(todas, "for=\"bq-tool-machine\""));

        var soUm = await client.GetStringAsync("/Boquilhas?ficheiros=1&q=t173&lote=24%2F02");
        Assert.Single(Regex.Matches(soUm, "name=\"bq_tool\""));
        Assert.Contains("data-dmo-open=\"/Ferramentas/Ficha?tool_id=lote-2402\"", soUm);
        var campoLote = Regex.Match(soUm, "<input[^>]*id=\"bq-tool-lot\"[^>]*>");
        Assert.True(campoLote.Success, "falta o campo Lote");
        Assert.Contains("value=\"24/02\"", campoLote.Value);
    }

    [Fact]
    public async Task FichaDaBoquilha_TemOsBotoesNoTopo_ESeguemOsQuatroMiniCartoesDeDados()
    {
        SeedUser("4925", "Ficha BQ", "catalog-ficha", "CatalogPass1!", withFerramentas: true);
        using var client = await LoginClientAsync("4925", "CatalogPass1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b1 = new Machine { Code = "B1" };
            var c2 = new Machine { Code = "C2" };
            db.Machines.AddRange(b1, c2);
            var tool = new Tool { ToolId = "ficha-bq-1", Type = ToolType.Bq, Reference = "T173", Lot = "24", BqAccountedQuantity = 120 };
            db.Tools.Add(tool);
            db.ToolMachineLinks.AddRange(
                new ToolMachineLink { Tool = tool, Machine = b1 },
                new ToolMachineLink { Tool = tool, Machine = c2 });
            await db.SaveChangesAsync();
        }

        var html = await client.GetStringAsync("/Boquilhas?ficheiros=1&ferramenta=ficha-bq-1");

        var ficha = html[Posicao(html, "id=\"bq-tool-sheet\"")..];
        // Os botões ficam no topo da ficha, antes dos dados.
        var botoes = Posicao(ficha, "Editar ficha / duplicar lote");
        Assert.True(botoes < Posicao(ficha, "Fechar ficha"));
        Assert.True(Posicao(ficha, "Fechar ficha") < Posicao(ficha, "bq-sheet-dados"));
        // Os quatro mini-cartões, por esta ordem, com o que a ficha tem.
        var dados = ficha[Posicao(ficha, "bq-sheet-dados")..];
        dados = dados[..Posicao(dados, "id=\"bqSemJobOnMovimentos\"")];
        Assert.Contains("<span>Quantidade do lote</span><strong>120</strong>", dados);
        Assert.Contains("<span>Registos</span><strong>0</strong>", dados);
        Assert.Contains("<span>Movimentos sem Job On</span><strong>0</strong>", dados);
        Assert.Contains("<span>Linhas compatíveis</span><strong>2</strong>", dados);
        Assert.True(Posicao(dados, "Quantidade do lote") < Posicao(dados, "<span>Registos</span>"));
        Assert.True(Posicao(dados, "<span>Registos</span>") < Posicao(dados, "<span>Movimentos sem Job On</span>"));
        Assert.True(Posicao(dados, "<span>Movimentos sem Job On</span>") < Posicao(dados, "<span>Linhas compatíveis</span>"));
    }
}
