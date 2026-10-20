namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O próprio EnvioDoFormulario (fase base dos testes, 19/10/2026): leva o
/// que o browser leva, para os testes dos formulários não passarem por um
/// envio que a página não faz.
/// </summary>
public class EnvioDoFormularioTests
{
    private const string Pagina = """
        <input id="fora" name="fora" value="1" form="registo" />
        <input id="semDono" name="sem_dono" value="x" />
        <form id="registo" method="post" action="/registar?handler=A&amp;b=1">
          <input type="hidden" name="trace_id" value="7" />
          <input id="doOutro" name="do_outro" value="y" form="apagar" />
          <label><input type="radio" name="tipo" value="saida" checked="checked" /></label>
          <label><input type="radio" name="tipo" value="entrada" /></label>
          <input id="quantidade" name="quantidade" value="" />
        </form>
        <form id="apagar" method="post" action="/apagar"></form>
        <textarea id="nota" name="nota" form="registo">antes</textarea>
        <button type="submit" form="registo" name="parte" value="peso" id="botaoA">A</button>
        <button type="submit" form="registo" name="parte" value="peso" id="botaoB" formaction="/outro?handler=B">B</button>
        """;

    [Fact]
    public void OsCamposForaDoFormularioComOFormDeleVao_OsDeDentroDeOutroNao()
    {
        var (acao, campos) = EnvioDoFormulario.Preencher(Pagina, ("quantidade", "3"), ("nota", "depois"));

        Assert.Equal("/registar?handler=A&b=1", acao);
        Assert.Equal(
            [("fora", "1"), ("trace_id", "7"), ("tipo", "saida"), ("quantidade", "3"), ("nota", "depois")],
            campos);
    }

    [Fact]
    public void UmCampoForaDoFormulario_EscolheOFormularioPeloForm()
    {
        var (acao, campos) = EnvioDoFormulario.Preencher(Pagina, ("nota", "só a nota"));

        Assert.Equal("/registar?handler=A&b=1", acao);
        Assert.Contains(("nota", "só a nota"), campos);
        Assert.Contains(("trace_id", "7"), campos);
        Assert.Throws<InvalidOperationException>(() => EnvioDoFormulario.Preencher(Pagina, ("semDono", "z")));
    }

    [Fact]
    public void ORadioEscolhidoPeloNameEValue_TiraOQueVinhaMarcado()
    {
        var (_, campos) = EnvioDoFormulario.Enviar(Pagina, [("quantidade", "3")], radios: [("tipo", "entrada")]);

        Assert.Equal([("tipo", "entrada")], campos.Where(c => c.Nome == "tipo"));
        Assert.Throws<InvalidOperationException>(() =>
            EnvioDoFormulario.Enviar(Pagina, [("quantidade", "3")], radios: [("tipo", "nao_existe")]));
    }

    [Fact]
    public void OBotaoCarregadoLevaONameEValue_EOFormaction()
    {
        var (acaoA, camposA) = EnvioDoFormulario.Enviar(Pagina, [("nota", "n")], idDoBotao: "botaoA");
        var (acaoB, camposB) = EnvioDoFormulario.Enviar(Pagina, [("nota", "n")], idDoBotao: "botaoB");

        Assert.Equal("/registar?handler=A&b=1", acaoA);
        Assert.Equal("/outro?handler=B", acaoB);
        Assert.Equal(("parte", "peso"), camposA[^1]);
        Assert.Equal(("parte", "peso"), camposB[^1]);
        Assert.Equal(("nota", "n"), camposA[^2]);
    }
}
