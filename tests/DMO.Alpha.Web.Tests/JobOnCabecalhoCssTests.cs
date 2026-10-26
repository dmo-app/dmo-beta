namespace DMO.Alpha.Web.Tests;

public class JobOnCabecalhoCssTests
{
    // DMO-431: a Referência e a Produção do cabeçalho do Job On voltaram a sobrepor-se
    // (a fila com largura 0 encolhia a procura por baixo do dropdown). A regra final
    // que dá espaço entre os dois campos e deixa a referência inteira não pode desaparecer.
    [Fact]
    public void CabecalhoDoJobOn_ReferenciaEProducaoNaoSeTocam()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "version.props")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var css = File.ReadAllText(Path.Combine(dir!, "src", "DMO.Alpha.Web", "wwwroot", "css", "dmo-jobon-folha.css"));
        var fim = css[css.LastIndexOf("DMO-431", StringComparison.Ordinal)..];
        Assert.Contains(".page-head .pick-row{width:auto;min-width:0;gap:12px;flex-wrap:nowrap}", fim);
        Assert.Contains(".page-head .pick-row .ref-search{flex:none;width:calc(11ch + 44px)}", fim);
        Assert.Contains(".page-head .head-right{min-width:max-content}", fim);
    }
}
