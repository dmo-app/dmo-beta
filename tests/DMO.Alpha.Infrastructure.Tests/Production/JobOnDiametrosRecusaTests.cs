using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Um diâmetro com mais de um número é recusado ao gravar (Diogo, 28/10/2026: «no corpo não deve
/// ter dois valores, deveria ser um valor e a tolerância»); a tolerância vai no campo dela.
/// </summary>
public sealed class JobOnDiametrosRecusaTests
{
    [Theory]
    [InlineData("mf_corpo", "52.60 / 48.80", "mf_corpo")]
    [InlineData("cm_pata", "12.40 12.30", "cm_pata")]
    [InlineData("mf_exterior", "103.5", null)]
    [InlineData("cm_gargalo", "21,50", null)]
    [InlineData("mf_corpo_tol", "0.05", null)]
    public void DoisNumerosNumDiametro_ERecusado(string chave, string valor, string? recusada)
    {
        Assert.Equal(recusada, JobOnFolhaRegras.PrimeiraChaveLonga(new Dictionary<string, string?> { [chave] = valor }, null));
    }
}
