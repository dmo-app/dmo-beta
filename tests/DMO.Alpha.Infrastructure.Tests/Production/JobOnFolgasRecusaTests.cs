using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Uma folga com mais de um número é recusada ao gravar (Diogo, 28/10/2026:
/// «o Job On não deve aceitar mais que 1 valor por cada zona»).
/// </summary>
public sealed class JobOnFolgasRecusaTests
{
    [Theory]
    [InlineData("mf_folga1", "0.12 / 0.16", "mf_folga1")]
    [InlineData("cm_folga2", "0.02 0.03", "cm_folga2")]
    [InlineData("mf_folga3", "0.16", null)]
    [InlineData("cm_notas", "0.12 / 0.16", null)]
    public void DoisNumerosNumaFolga_ERecusada(string chave, string valor, string? recusada)
    {
        Assert.Equal(recusada, JobOnFolhaRegras.PrimeiraChaveLonga(new Dictionary<string, string?> { [chave] = valor }, null));
    }
}
