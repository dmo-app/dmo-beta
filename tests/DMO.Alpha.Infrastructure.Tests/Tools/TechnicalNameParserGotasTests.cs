using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// As GOTAS pelo nome técnico do molde (Diogo, 28/10/2026: «ver se o jobon já
/// tem em consideração nome técnico para mudar gota e meter número correto»):
/// 55 = dupla (2); 44 e 4T = tripla (3); 50 e 5T (B3) = 3; 95 (B1, C2) = 4.
/// Os nomes são os da tabela «Codificação Moldes e acessórios - MG».
/// </summary>
public sealed class TechnicalNameParserGotasTests
{
    [Theory]
    [InlineData("M107595MPPSX", 4)]
    [InlineData("M107595MFPSV", 4)]
    [InlineData("M581055MPPSX", 2)]
    [InlineData("M581055MFPSV", 2)]
    [InlineData("M581044MPPSX", 3)]
    [InlineData("M58104TMFPSV", 3)]
    [InlineData("M581050MPPSX", 3)]
    [InlineData("M58105TMFPSV", 3)]
    [InlineData("m5810.95.mp.ps.x_so", 4)]
    public void Molde_DizAsGotasPelaDimensao(string nome, int gotas)
    {
        Assert.Equal(gotas, TechnicalNameParser.Gotas(nome));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABT188NX34")]
    [InlineData("M002064MPSSX")]
    [InlineData("texto livre")]
    public void SemNomeAcessorioOuOutraDimensao_NaoDiz(string? nome)
    {
        Assert.Null(TechnicalNameParser.Gotas(nome));
    }
}
