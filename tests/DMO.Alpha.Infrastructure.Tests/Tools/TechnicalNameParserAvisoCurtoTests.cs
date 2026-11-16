using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>O aviso curto com o «Saber mais» (Diogo, 16/11/2026: «muita informação»).</summary>
public sealed class TechnicalNameParserAvisoCurtoTests
{
    [Fact]
    public void AvisoCurto_DizOTipoEAParteDaAjuda()
    {
        Assert.Equal(("O nome técnico é de uma CM, não de uma MF.", "nome-tecnico-outro-tipo"), TechnicalNameParser.AvisoCurto(ToolType.Mf, "MST7844MPPSX"));
        Assert.Equal(("O nome técnico não segue a regra.", "nome-tecnico-regra"), TechnicalNameParser.AvisoCurto(ToolType.Cm, "5447T173"));
        Assert.Null(TechnicalNameParser.AvisoCurto(ToolType.Cm, "M544744MPPSX"));
    }
}
