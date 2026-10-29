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
}
