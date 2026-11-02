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

    // Diogo, 29/10/2026: «preciso disto automático», sem ninguém no PC (DMO-683): a entrada
    // preenche-se primeiro pelo próprio SAP (RSYST-BNAME/RSYST-BCODE), antes do Windows.
    [Theory]
    [InlineData("Sap.DMO_USO_SAP.vbs")]
    [InlineData("Sap.DMO_FABRICO_SAP.vbs")]
    public void AEntrada_PreencheSePeloProprioSap(string nome)
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream(nome)!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Contains("wnd[0]/usr/pwdRSYST-BCODE", vbs);
        Assert.Contains("wnd[0]/usr/txtRSYST-BNAME", vbs);
        Assert.True(vbs.IndexOf("senhaEscrita = EntrarPeloSap()", StringComparison.Ordinal) < vbs.IndexOf("If shell.AppActivate(JANELA_LOGON) Then", StringComparison.Ordinal));
    }
    // Diogo, 02/11/2026: a % uso foi gravada e a app dizia «A leitura do SAP falhou» (DMO-704):
    // com um passo falhado e outro certo, o estado diz também o que correu bem («ok:»).
    [Fact]
    public void UmPassoFalhado_OEstadoDizTambemOQueCorreuBem()
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream("Sap.DMO_SAP_DIARIO.vbs")!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Matches(new Regex(@"If processo\.ExitCode <> 0 Then\s+Falhar nome, ""codigo"" & processo\.ExitCode\s+Else\s+okGeral = okGeral & nome & "" """), vbs);
        Assert.Contains("\"erro \" & Trim(erroGeral) & \" ok:\" & Trim(okGeral) & \" \" & Agora()", vbs);
        Assert.Contains("\"erro \" & Trim(erroGeral) & \" \" & Agora()", vbs);
    }

    // Diogo, 02/11/2026: «a janela já não está a minimizar» (DMO-704): mostrada para
    // desenhar a lista, a janela do SAP volta a minimizar-se; e minimiza-se outra vez
    // depois da entrada com senha.
    [Fact]
    public void AJanelaDoSap_VoltaAMinimizarSe()
    {
        var assembly = typeof(SapScripts).Assembly;
        string Ler(string nome)
        {
            using var stream = assembly.GetManifestResourceStream(nome)!;
            return new StreamReader(stream).ReadToEnd();
        }
        var uso = Ler("Sap.DMO_USO_SAP.vbs");
        var fabrico = Ler("Sap.DMO_FABRICO_SAP.vbs");

        Assert.True(uso.IndexOf(".maximize", StringComparison.Ordinal) < uso.IndexOf("If mostrada Then MinimizarSap session", StringComparison.Ordinal));
        Assert.Contains("mostrada = True", uso);
        Assert.Contains("Sub MinimizarSap(", uso);
        Assert.True(fabrico.IndexOf("MinimizarSap session", fabrico.IndexOf("ctxtCENTRO-LOW\", 30)", StringComparison.Ordinal), StringComparison.Ordinal)
            < fabrico.IndexOf("session.findById(\"wnd[0]/usr/ctxtCENTRO-LOW\").text = CENTRO", StringComparison.Ordinal));
        Assert.Contains("Sub MinimizarSap(", fabrico);
    }

    // Diogo, 02/11/2026 (DMO-707): parou na janela «Moldes Existentes» com a senha por escrever.
    // Na janela de entrada (tem o botão «Logon») a 2.ª caixa é a senha; sem caixas à vista
    // escreve-se pelo teclado (código 4). Sem «Logon» (o SAP já aberto, DMO-677) nada se escreve.
    [Theory]
    [InlineData("Sap.DMO_USO_SAP.vbs")]
    [InlineData("Sap.DMO_FABRICO_SAP.vbs")]
    public void NaJanelaDeEntrada_ASenhaEscreveSe(string nome)
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream(nome)!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Contains("-match '^\\W*log\\s*on\\W*$'", vbs);
        Assert.Contains("if ($logon -and -not $senha -and $caixas.Count -ge 2) { $nome = $caixas[0]; $senha = $caixas[1] }", vbs);
        Assert.Contains("if (-not $nome -or -not $senha) { if ($logon) { exit 4 } else { exit 3 } }", vbs);
        Assert.Contains("If codigo <> 0 And codigo <> 3 Then", vbs);
    }

    // Diogo, 02/11/2026 (DMO-709): o log dizia «código 3 … nada escrito» com a janela de
    // entrada aberta. O código 3 só trava a escrita se uma janela do SAP tiver esse nome
    // (o SAP já aberto, DMO-677); sem nenhuma, escreve-se pelo teclado.
    [Theory]
    [InlineData("Sap.DMO_USO_SAP.vbs")]
    [InlineData("Sap.DMO_FABRICO_SAP.vbs")]
    public void Codigo3SemJanelaDoSapComEsseNome_EscrevePeloTeclado(string nome)
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream(nome)!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Matches(new Regex(@"If codigo = 3 And Not SapTemJanela\(JANELA_LOGON\) Then\s+Registo [^\n]*\s+codigo = 4\s+End If"), vbs);
        Assert.True(vbs.IndexOf("codigo = 4", StringComparison.Ordinal) < vbs.IndexOf("If codigo <> 0 And codigo <> 3 Then", StringComparison.Ordinal));
        Assert.Contains("titulo = s.findById(\"wnd[0]\").Text", vbs);
    }

    // Diogo, 02/11/2026 (DMO-710): o FABRICO acabava com 0 sem dizer se tinha entrado no SAP.
    // O ecrã de entrada (tem a caixa da senha) não conta como «pronto», e o log diz quando
    // o SAP ficou pronto e quantas produções gravou.
    [Fact]
    public void OFabrico_SoAvancaDepoisDeEntrar_EDizNoLog()
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream("Sap.DMO_FABRICO_SAP.vbs")!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Matches(new Regex(@"Campo\(session, ""wnd\[1\]""\) Is Nothing _\s+And Campo\(session, ""wnd\[0\]/usr/pwdRSYST-BCODE""\) Is Nothing Then Exit Do"), vbs);
        Assert.Contains("Registo \"SAP pronto (\" & tituloSap & \")\"", vbs);
        Assert.Contains("Registo total & \" produções gravadas em \" & fso.GetFileName(saida)", vbs);
    }

    // Diogo, 02/11/2026 12:49 (DMO-711): depois de escrever pelo teclado ficou o SAP Logon à
    // frente e o FABRICO falhou. O ecrã de entrada do SAP preenche-se sempre que aparecer
    // (no máximo 2 vezes), e o teclado só escreve com a janela da senha à frente.
    [Theory]
    [InlineData("Sap.DMO_USO_SAP.vbs")]
    [InlineData("Sap.DMO_FABRICO_SAP.vbs")]
    public void AEntradaPeloSap_TentaSempre_EOTecladoSoComAJanelaAFrente(string nome)
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream(nome)!;
        var vbs = new StreamReader(stream).ReadToEnd();

        Assert.Matches(new Regex(@"If entradasSap < 2 Then\s+If EntrarPeloSap\(\) Then\s+senhaEscrita = True\s+entradasSap = entradasSap \+ 1"), vbs);
        Assert.Matches(new Regex(@"If codigo <> 0 And codigo <> 3 Then(\s+'[^\n]*)*\s+If shell\.AppActivate\(JANELA_LOGON\) Then\s+WScript\.Sleep 300\s+shell\.SendKeys"), vbs);
        Assert.Contains("a janela da senha não veio para a frente: nada escrito", vbs);
    }
}
