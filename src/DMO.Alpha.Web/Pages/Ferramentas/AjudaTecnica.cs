namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// Onde a app usa cada secção da informação técnica (Diogo, 20/10/2026:
/// «adicionar um triângulo de informação em cada secção para explicar em que
/// funções esses dados são usados»). Os mesmos textos no registo e na ficha.
/// </summary>
public static class AjudaTecnica
{
    /// <summary>Processo, peso nominal e punção (CM); marisa (BQ): PesoCalculation e PesoToolFactsQuery.</summary>
    public const string Peso =
        "Usados no Peso. O processo (NNPB ou PS) escolhe a densidade do vidro. " +
        "A punção (CM) e a marisa (BQ) entram no peso do vidro: (capacidade + marisa − punção) × densidade. " +
        "O peso nominal é o peso do desenho com que o peso médio é comparado. O processo, a punção e a marisa também aparecem no Job On.";

    /// <summary>O gargalo nominal de cada componente: PegamentosQuery e RecordPegamentosMeasurementHandler.</summary>
    public const string Gargalo =
        "Usado nos Pegamentos: é o diâmetro nominal do gargalo com que as medições desta ferramenta são comparadas. " +
        "Sem ele não se gravam Pegamentos; se faltar, pode ser escrito na própria folha dos Pegamentos. Também aparece no Job On.";

    /// <summary>A geometria do tampão da CM: PesoCapCalculation, PesoCapSupportQuery e PesoSuposicaoCalculation.</summary>
    public const string Tampao =
        "Usados no Peso: com o diâmetro e a calote calcula-se o volume do tampão, " +
        "para saber quanto do peso vem da calote e simular outra calote. Também aparecem no Job On.";
}
