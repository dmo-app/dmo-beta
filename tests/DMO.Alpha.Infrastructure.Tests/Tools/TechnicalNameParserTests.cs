using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

public sealed class TechnicalNameParserTests
{
    [Theory]
    [InlineData("ABS023SC4713", "S023", ToolType.Bq, "neck ring")]
    [InlineData("A.S.S023.S.F", "S023", ToolType.Bq, "blow head (marisa)")]
    [InlineData("APS023SD02", "S023", ToolType.Cm, "plunger (punção)")]
    [InlineData("AT0020SF0-A", "0020", ToolType.Cm, "baffle (tampão)")]
    [InlineData("ASC190", "C190", ToolType.Bq, "blow head (marisa)")]
    [InlineData("ARST12PA", "ST12", null, "cooling tube")]
    public void Accessory_ReadsTheReferenceFromTheGrammar(string name, string reference, ToolType? type, string family)
    {
        var decoded = TechnicalNameParser.Decode(name);

        Assert.Equal(reference, decoded.InferredReference);
        Assert.Equal(type, decoded.InferredType);
        Assert.Equal(family, decoded.Family);
    }

    [Fact]
    public void Mold_KeepsTheModelAndType()
    {
        var decoded = TechnicalNameParser.Decode("M00664TMFNNV");

        Assert.Equal("0066", decoded.InferredReference);
        Assert.Equal(ToolType.Mf, decoded.InferredType);
    }
}
