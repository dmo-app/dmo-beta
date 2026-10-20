using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Os três estados da aprovação nos PDFs (sweep de 19/10/2026, C-3): o PDF
/// do Resumo diz «Aprovado», «Não aprovado» (quem, quando e a nota) ou «Por
/// aprovar»; as folhas do Peso e dos Pegamentos mostram a decisão da sua
/// parte, com o nome de quem decidiu. Antes, um «Não aprovar» saía como
/// «Sem registo» e a nota não chegava ao papel.
/// </summary>
public sealed class AprovacaoNosPdfsTests
{
    private static readonly DateTime Em = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ControloApprovalReadModel Aprovado =
        new("4802", Em) { DecididoPorNome = "Chefe Aprovação" };

    private static readonly ControloApprovalReadModel NaoAprovado =
        new(null, null, "4801", Em, "Repetir os pegamentos.") { DecididoPorNome = "Ana Revisão" };

    private static ResumoPdfGenerator.Input Resumo(ControloApprovalReadModel? approval) =>
        new("202605", "5447T173", "B1", null, null, [], null, null, [], approval);

    private static int Ocorrencias(string text, string parte) =>
        (text.Length - text.Replace(parte, "").Length) / parte.Length;

    [Fact]
    public void ResumoPdf_NaoAprovado_MostraQuemQuandoEANota()
    {
        var text = PdfText.Of(new ResumoPdfGenerator().GenerateBytes(Resumo(NaoAprovado)));

        // O cartão da aprovação («Não aprovado», com quem, quando e a nota por
        // baixo) e a tabela «Aprovado por» (a frase inteira).
        Assert.Contains("Não aprovado", text);
        Assert.Contains("Não aprovado por Ana Revisão · 24/07/2026 13:00 · Repetir os pegamentos.", text);
        Assert.Equal(2, Ocorrencias(text, "Ana Revisão · 24/07/2026 13:00 · Repetir os pegamentos."));
        Assert.DoesNotContain("Por aprovar", text);
        Assert.DoesNotContain("Sem registo", text);
    }

    [Fact]
    public void ResumoPdf_Aprovado_MostraONomeDeQuemAprovou()
    {
        var text = PdfText.Of(new ResumoPdfGenerator().GenerateBytes(Resumo(Aprovado)));

        // O cartão (quem e quando por baixo de «Aprovado») e a tabela.
        Assert.Contains("Chefe Aprovação · 24/07/2026 13:00", text);
        Assert.Contains("Chefe Aprovação 24/07/2026 13:00", text);
        Assert.DoesNotContain("Não aprovado", text);
        Assert.DoesNotContain("Operador 4802", text);
    }

    [Fact]
    public void ResumoPdfCompleto_AFolhaDoPesoMostraADecisaoDoPeso_EADosPegamentosNenhuma()
    {
        // Só o Peso se aprova (Diogo, 19/10/2026): a folha dos Pegamentos não
        // tem selo de aprovação.
        var peso = new PesoReadModel(1, "T3", "5447", "3", "BQ-TOOL-9", "T173", "33", "NNPB", 20, .99821m, 2.50m,
            10m, 30m, 138m, [new PesoMeasurementReadModel(1, 1, "61", 71.10m, 71.38m, 142.41m)],
            new PesoAveragesReadModel(71.10m, 71.38m, 142.41m), ControloId: 1);
        var input = Resumo(Aprovado) with
        {
            PesoDetalhe = peso,
            PegamentosDetalhes = [PdfRecoveryTests.ReferenceSheet()],
            PesoApproval = Aprovado
        };

        var text = PdfText.Of(new ResumoPdfGenerator().GenerateBytes(input));

        Assert.Contains("Aprovado por Chefe Aprovação · 24/07/2026 13:00", text);
        Assert.DoesNotContain("APROVADO —", text);
    }

    [Fact]
    public void PesoPdf_OsTresEstados()
    {
        string Texto(ControloApprovalReadModel? approval) => PdfText.Of(new PesoPdfGenerator().GenerateBytes(
            new PesoPdfGenerator.Input("202602", "1075C142", "B1", "cm", "ST100", "8", "NNPB", 25, .99603m, 2.5m, 10m, 24.42m, 138m,
                [new PesoPdfGenerator.Reading(1, "61", 71.10m, 71.38m, 142.41m)], new PesoAveragesReadModel(71.10m, 71.38m, 142.41m),
                null, "", approval)));

        Assert.Contains("Aprovado por Chefe Aprovação · 24/07/2026 13:00", Texto(Aprovado));
        Assert.Contains("Não aprovado por Ana Revisão · 24/07/2026 13:00 · Repetir os pegamentos.", Texto(NaoAprovado));
        Assert.Contains("Por aprovar", Texto(null));
        Assert.Contains("Por aprovar", Texto(new ControloApprovalReadModel(null, null)));
    }

    [Fact]
    public void PegamentosPdf_SemSeloDeAprovacao()
    {
        var text = PdfText.Of(new PegamentosPdfGenerator().GenerateBytes(
            PegamentosPdfGenerator.FromSheet(PdfRecoveryTests.ReferenceSheet())));

        Assert.DoesNotContain("APROVADO", text);
        Assert.DoesNotContain("Por aprovar", text);
    }
}
