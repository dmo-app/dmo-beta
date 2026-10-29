using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Um diâmetro antigo com dois números separa-se sozinho, ao abrir e ao gravar: o primeiro fica no valor, o
/// segundo vai para a «Tol.» vazia, e o Guardar deixa de ser recusado (Diogo, 29/10/2026: «carreguei guardar
/// depois de iniciar um jobon e partiu a página»).
/// </summary>
public sealed class JobOnDiametrosSepararTests
{
    [Fact]
    public void DoisNumeros_VaiOSegundoParaATolVazia_EOGuardarPassa()
    {
        var folha = JobOnFolhaRegras.NormalizarFolha(new Dictionary<string, string?> { ["mf_corpo"] = "52.60 / 0.05" });

        Assert.Equal("52.60", folha["mf_corpo"]);
        Assert.Equal("0.05", folha["mf_corpo_tol"]);
        Assert.Null(JobOnFolhaRegras.PrimeiraChaveLonga(folha, null));
    }

    [Fact]
    public void TolJaEscrita_FicaComoEsta()
    {
        var folha = JobOnFolhaRegras.SepararDiametros(new Dictionary<string, string?> { ["cm_pata"] = "12.40 12.30", ["cm_pata_tol"] = "0.1" });

        Assert.Equal("12.40", folha["cm_pata"]);
        Assert.Equal("0.1", folha["cm_pata_tol"]);
    }

    [Fact]
    public void UmNumero_NaoMuda()
    {
        var antes = new Dictionary<string, string?> { ["mf_exterior"] = "103.5" };
        Assert.Same(antes, JobOnFolhaRegras.SepararDiametros(antes));
    }
}
