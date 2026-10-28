using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// O aviso do nome técnico que não bate (Diogo, 28/10/2026, o OP 99 PMD 02):
/// um nome que não segue a regra dos desenhos ou que é de outro tipo.
/// </summary>
public sealed class TechnicalNameParserAvisoTests
{
    [Theory]
    [InlineData(ToolType.Cm, "M544744MPPSX")]
    [InlineData(ToolType.Cm, "MST10095MPNNX")]
    [InlineData(ToolType.Mf, "M00664TMFNNV")]
    [InlineData(ToolType.Bq, "ABS023SC4713")]
    [InlineData(ToolType.Cm, null)]
    [InlineData(ToolType.Bq, " ")]
    public void UmNomeQueBate_NaoTemAviso(ToolType tipo, string? nome) =>
        Assert.Null(TechnicalNameParser.Aviso(tipo, nome));

    [Fact]
    public void UmaCmComONomeDeUmMf_Avisa()
    {
        var aviso = TechnicalNameParser.Aviso(ToolType.Cm, "M00664TMFNNV");
        Assert.NotNull(aviso);
        Assert.StartsWith("O nome técnico é de uma MF, mas esta ferramenta é uma CM", aviso);
    }

    [Fact]
    public void UmaBqComONomeDeUmMolde_Avisa() =>
        Assert.StartsWith("O nome técnico é de uma CM, mas esta ferramenta é uma BQ", TechnicalNameParser.Aviso(ToolType.Bq, "M544744MPPSX"));

    [Fact]
    public void UmNomeForaDaRegra_Avisa() =>
        Assert.StartsWith("O nome técnico não segue a regra dos desenhos: uma CM é M + modelo", TechnicalNameParser.Aviso(ToolType.Cm, "5447T173"));
}
