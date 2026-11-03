using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Pages.JobOn;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Cada pessoa escolhe nas Definições › A minha conta os avisos do Job On
/// que não quer ver (Diogo, 30/10/2026: «ao usar map abre sempre imensos
/// avisos do armazém»). O desligado sai; os outros ficam.
/// </summary>
public sealed class JobOnAvisosPessoaisTests
{
    private static readonly JobOnToolFacts Facto = new(
        "t1", "CM", "5447T173", "3", null, null, null, null, null, null, null, null, [], null,
        "CM 5447T173 / lote 3 também está associado a 202601 (B1). Esta informação não impede a sua escolha.")
    {
        AvisoArmazem = "CM 5447T173 / lote 3 não está registada no Armazém e não tem estado."
    };

    [Fact]
    public void ArmazemDesligado_SaiSoOAvisoDoArmazem()
    {
        var escondidos = AvisosPessoais.Ler(AvisosPessoais.Armazem);

        var avisos = IndexModel.AvisosDasFerramentas(Facto, comAvisoArmazem: true, escondidos);

        Assert.DoesNotContain(avisos, a => a.Texto.Contains("não está registada no Armazém", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Texto.Contains("também está associado", StringComparison.Ordinal));
    }

    [Fact]
    public void SemNadaDesligado_MostraTodos()
    {
        var avisos = IndexModel.AvisosDasFerramentas(Facto, comAvisoArmazem: true, AvisosPessoais.Ler(null));

        Assert.Equal(2, avisos.Count);
    }

    [Fact]
    public void LerEEscrever_IgnoramChavesQueNaoExistem()
    {
        var lidos = AvisosPessoais.Ler("b2, inventado ,uso-paralelo");

        Assert.Equal(new[] { "b2", "uso-paralelo" }, lidos.OrderBy(c => c));
        Assert.Equal("b2,uso-paralelo", AvisosPessoais.Escrever(lidos));
    }
}
