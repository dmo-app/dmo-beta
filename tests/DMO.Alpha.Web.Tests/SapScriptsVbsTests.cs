using System.Text.RegularExpressions;
using DMO.Alpha.Web.Services;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Os scripts do SAP correm no VBScript do Windows, não no VBA: o «Like» do
/// VBA deu «Sub ou Function não definidas» (800A0023) no DMO_SAP_DIARIO.vbs
/// no PC do SAP (Diogo, 29/10/2026).
/// </summary>
public sealed class SapScriptsVbsTests
{
    [Fact]
    public void OsScriptsDoSap_NaoUsamOLikeDoVba()
    {
        var assembly = typeof(SapScripts).Assembly;
        var nomes = assembly.GetManifestResourceNames().Where(n => n.StartsWith("Sap.", StringComparison.Ordinal) && n.EndsWith(".vbs", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Contains("Sap.DMO_SAP_DIARIO.vbs", nomes);

        foreach (var nome in nomes)
        {
            using var stream = assembly.GetManifestResourceStream(nome)!;
            var codigo = string.Join("\n", new StreamReader(stream).ReadToEnd().Split('\n').Select(l => l.Split('\'')[0]));
            Assert.DoesNotMatch(new Regex(@"\sLike\s", RegexOptions.IgnoreCase), codigo);
        }
    }

    // Diogo, 29/10/2026: «consultar apenas MG» (plant): a ZPPG só procura no centro MG.
    [Fact]
    public void OScriptDaZppg_ProcuraSoNaPlantMg()
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream("Sap.DMO_USO_SAP.vbs")!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Contains("Const CENTRO = \"MG\"", vbs);
        Assert.Matches(new Regex(@"searchField\.Text = codigo\s+SoCentroMg session\s+session\.findById\(""wnd\[0\]""\)\.sendVKey 8"), vbs);
    }

    // Diogo, 29/10/2026: «prendeu na pass» e bloqueou o PC (DMO-661): sem senha os
    // scripts que entram no SAP param logo; com o SAP já aberto a senha ainda se escreve.
    [Theory]
    [InlineData("Sap.DMO_USO_SAP.vbs")]
    [InlineData("Sap.DMO_FABRICO_SAP.vbs")]
    public void SemSenha_OScriptParaLogo(string nome)
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream(nome)!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Matches(new Regex(@"If SENHA = """" Or InStr\(SENHA, Chr\(123\) & Chr\(123\)\) > 0 Then\s+\S.*Falta a senha do SAP.*\s+WScript\.Quit 1"), vbs);
        Assert.True(vbs.IndexOf("WScript.Quit 1", StringComparison.Ordinal) < vbs.IndexOf("Do", vbs.IndexOf("Falta a senha", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.DoesNotContain("Or sapAberto", vbs);
    }

    // Diogo, 29/10/2026: no modo Normal sem ferramentas à espera, a app dizia «A leitura do SAP falhou» (DMO-662).
    [Fact]
    public void SemCodigosAEspera_OUsoAcabaSemErro()
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream("Sap.DMO_USO_SAP.vbs")!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Matches(new Regex(@"If total = 0 Then(\s+'[^\n]*)*\s+If Not automatico Then MsgBox[^\n]*\s+WScript\.Quit 0"), vbs);
    }

    // Diogo, 29/10/2026: «Função impossível» (DMO-677): sem caixa da senha (código 3) o
    // teclado não escreve na janela do SAP que está em uso.
    [Theory]
    [InlineData("Sap.DMO_USO_SAP.vbs")]
    [InlineData("Sap.DMO_FABRICO_SAP.vbs")]
    public void SemCaixaDaSenha_NaoEscrevePeloTeclado(string nome)
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream(nome)!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Contains("If codigo <> 0 And codigo <> 3 Then", vbs);
        Assert.DoesNotContain("If codigo <> 0 Then", vbs);
    }

    // Diogo, 29/10/2026: «não tenho de andar a carregar várias vezes» (DMO-681): o «Por data»
    // lê as produções e também a % uso de todas as ferramentas.
    [Fact]
    public void PorData_TambemLeAPercentagemDeUso()
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream("Sap.DMO_SAP_DIARIO.vbs")!;
        var vbs = new StreamReader(stream).ReadToEnd();
        var data = vbs[vbs.IndexOf("Case \"data\"", StringComparison.Ordinal)..vbs.IndexOf("Case \"ref\"", StringComparison.Ordinal)];

        Assert.Contains("BaixarCodigos", data);
        Assert.Contains("Correr \"DMO_USO_SAP.vbs\", \"\"", data);
    }
}
