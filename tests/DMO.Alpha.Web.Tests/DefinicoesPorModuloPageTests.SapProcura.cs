using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Os códigos de procura no SAP mudam-se no UI (Diogo, 28/10/2026: «eu pedi
/// para alterar no UI», «não deve ser só para BQ»): a tabela no Admin › SAP
/// para cada tipo e Linha, e o «Código SAP» no Opções da ficha de cada
/// ferramenta. Os códigos que o script pede seguem os dois.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task ATabelaDosCodigosDeProcura_GravaNoAdminEMudaOsCodigosPedidos()
    {
        var referencia = "R" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "T" + Guid.NewGuid().ToString("N")[..10], Type = ToolType.Mf, Reference = referencia, Lot = "6" });
            db.SaveChanges();
        }

        using var admin = await LoginAdminAsync();
        var sap = await (await admin.GetAsync("/Definicoes?secao=admin&sub=sap")).Content.ReadAsStringAsync();
        Assert.Contains("id=\"admSapProcura\"", sap);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(sap)!;

        var invalido = await admin.PostAsync("/Admin/Definicoes?handler=SaveSapProcura", HtmlFormHelpers.BuildFormPost(
            ("procura_tipo", "mf"), ("procura_linha", ""), ("procura_antes", "M\""), ("procura_meio", "referencia"),
            ("procura_depois", ""), ("procura_lote", "true"), ("__RequestVerificationToken", token)));
        Assert.EndsWith("sap=procura-invalida", invalido.Headers.Location!.OriginalString);

        var gravado = await admin.PostAsync("/Admin/Definicoes?handler=SaveSapProcura", HtmlFormHelpers.BuildFormPost(
            ("procura_tipo", "mf"), ("procura_linha", ""), ("procura_antes", "mf"), ("procura_meio", "referencia"),
            ("procura_depois", ""), ("procura_lote", "true"), ("__RequestVerificationToken", token)));
        Assert.EndsWith("sap=ok-procura", gravado.Headers.Location!.OriginalString);
        Assert.Contains("MF[refer", await (await admin.GetAsync("/Definicoes?secao=admin&sub=sap")).Content.ReadAsStringAsync());

        try
        {
            using var scope = _factory.Services.CreateScope();
            var codigos = await scope.ServiceProvider.GetRequiredService<IFerramentasUsoSap>().TodosOsCodigosAsync();
            Assert.Contains("MF" + referencia + "06", codigos);
        }
        finally
        {
            var tirada = await admin.PostAsync("/Admin/Definicoes?handler=SaveSapProcura", HtmlFormHelpers.BuildFormPost(
                ("procura_tipo", "mf"), ("procura_linha", ""), ("procura_tirar", "1"), ("__RequestVerificationToken", token)));
            Assert.EndsWith("sap=ok-procura-tirada", tirada.Headers.Location!.OriginalString);
        }
    }

    [Fact]
    public async Task OCodigoSapDaFicha_GanhaATabela_EVazioVoltaAEla()
    {
        var nome = "MCS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPNNX";
        var toolId = SeedToolComNomeTecnico(nome, "8");
        var client = await LoginOperatorAsync("7192", DmoCapabilities.Ferramentas);
        var ficha = await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync();
        Assert.Contains(nome + "08", ficha);
        // O Opções já não tem o «Código SAP» (Diogo, 29/10/2026: «remover este código SAP das opções»).
        Assert.DoesNotContain("data-dmo-dialog=\"ferr-codigo-sap-dialog\"", ficha);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(ficha)!;

        var gravado = await client.PostAsync("/Ferramentas/Ficha?handler=CodigoSap", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("codigo_sap", "x" + nome + "08"), ("__RequestVerificationToken", token)));
        Assert.Contains("guardado=codigo-sap", gravado.Headers.Location!.OriginalString);
        using (var scope = _factory.Services.CreateScope())
        {
            var codigos = await scope.ServiceProvider.GetRequiredService<IFerramentasUsoSap>().TodosOsCodigosAsync();
            Assert.Contains("X" + nome + "08", codigos);
            Assert.DoesNotContain(nome + "08", codigos);
        }

        await client.PostAsync("/Ferramentas/Ficha?handler=CodigoSap", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("codigo_sap", ""), ("__RequestVerificationToken", token)));
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.Contains(nome + "08", await scope.ServiceProvider.GetRequiredService<IFerramentasUsoSap>().TodosOsCodigosAsync());
        }
    }

    // Diogo, 29/10/2026: no Opções do SAP, «Pedir só as à espera» passa a «Atualizar» (DMO-598).
    [Fact]
    public async Task OBotaoDoSap_DizAtualizar_ENaoPedirSoAsAEspera()
    {
        var client = await LoginOperatorAsync("7193", DmoCapabilities.Ferramentas);
        var js = await (await client.GetAsync("/js/ferramentas-sap.js")).Content.ReadAsStringAsync();

        Assert.Contains("todas ? \"Atualizar\" : \"Pedir todas\"", js);
        Assert.DoesNotContain("Pedir só as à espera", js);
    }

    // Diogo, 29/10/2026: «Dias de produções» na mesma linha, à frente da hora.
    [Fact]
    public async Task AHoraEOsDiasDoSap_FicamNaMesmaLinha()
    {
        using var admin = await LoginAdminAsync();
        var html = await (await admin.GetAsync("/Definicoes?secao=admin&sub=sap")).Content.ReadAsStringAsync();

        Assert.Matches(new System.Text.RegularExpressions.Regex("<div class=\"adm-sap-hora-dias\"[^>]*display:flex[^>]*>\\s*<div class=\"dmo-field\">\\s*<label for=\"admSapHora\">.*?<label for=\"admSapDias\">", System.Text.RegularExpressions.RegexOptions.Singleline), html);
    }
}
