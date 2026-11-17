using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O PDF dos Pegamentos não leva selo de aprovação, em nenhuma forma (audit
/// externo S5, 16/11/2026, M3: «Aprovado por 123» passava no teste, que só
/// procurava «APROVADO» em maiúsculas).
/// </summary>
public sealed class PegamentosPdfSemAprovadoTests
{
    /// <summary>Garante: nem «aprovado» nem «aprovar» aparecem no PDF dos Pegamentos, com qualquer caixa.</summary>
    [Fact]
    public void PegamentosPdf_SemAprovadoEmNenhumaCaixa()
    {
        var text = PdfText.Of(new PegamentosPdfGenerator().GenerateBytes(
            PegamentosPdfGenerator.FromSheet(PdfRecoveryTests.ReferenceSheet())));

        Assert.DoesNotContain("aprovad", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aprovar", text, StringComparison.OrdinalIgnoreCase);
    }
}
