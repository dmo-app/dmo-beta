namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O cálculo dos Pegamentos detido pela aplicação (backend/Core) — o
/// blueprint proíbe o frontend de manter implementações concorrentes
/// (PDF §10 L492: «All calculation is server-side»). Fórmulas canónicas
/// (spec Controlo Create — Pegamentos L94-101; manual §7 L306-326):
/// <code>
/// ovalização = costura − contra costura   (sinal preservado)
/// média      = (costura + contra costura) / 2   (ou = costura, eixo único)
/// corredor   = nominal ± tolerância       (tolerância congelada no registo)
/// </code>
/// A avaliação do corredor é POR LINHA de medição (spec L106) e os valores
/// medidos avaliáveis — costura, contra costura (quando presente) e média
/// — são verificados independentemente (manual L338: «Costura, Contra
/// costura e Média são verificadas independentemente. A Média não pode
/// ocultar uma medição individual má»): atingir o limite, cruzá-lo ou
/// igualá-lo cria alerta informativo para a linha (manual L348-354; spec
/// L107-116: alertas nunca aprovam, rejeitam, param a produção nem
/// substituem a decisão do aprovador).
/// </summary>
public static class PegamentosCalculation
{
    /// <summary>
    /// Default do Owner: a tolerância normal do corredor é ±0,20 mm (spec
    /// L103: «The normal tolerance is the Tool-owned nominal gargalo value
    /// ± 0.20 mm»; manual L342-346). Nunca locked: a configuração do
    /// Controlo pode substituí-la (CONFIGURATION.md L20) e o valor efetivo
    /// é congelado no registo.
    /// </summary>
    public const decimal DefaultToleranceMm = 0.20m;

    /// <summary>
    /// Ovalização (mm) = Costura − Contra costura, com o SINAL PRESERVADO
    /// (decisão do Owner: «+0,3 e −0,3 são fatos distintos»; manual L308:
    /// «O sinal da Ovalização é preservado... não deve ser descartado ou
    /// normalizado silenciosamente»). Null quando a contra costura não foi
    /// medida — a ovalização fica indefinida, não calculada (spec L128-131;
    /// manual L322).
    /// </summary>
    public static decimal? OvalizacaoMm(decimal costuraMm, decimal? contraCosturaMm) =>
        contraCosturaMm is { } contraCostura ? costuraMm - contraCostura : null;

    /// <summary>
    /// Média (mm) = (Costura + Contra costura) / 2 quando ambos os eixos
    /// são medidos; = Costura quando a contra costura está ausente (spec
    /// L132-134 «Média → Costura»; manual L323: «Média assume o valor da
    /// Costura (valor único)»).
    /// </summary>
    public static decimal MediaMm(decimal costuraMm, decimal? contraCosturaMm) =>
        contraCosturaMm is { } contraCostura
            ? (costuraMm + contraCostura) / 2m
            : costuraMm;

    /// <summary>
    /// Limite inferior do corredor = nominal − tolerância (manual L344).
    /// </summary>
    public static decimal CorridorLowerMm(decimal nominalGargaloMm, decimal toleranceMm) =>
        nominalGargaloMm - toleranceMm;

    /// <summary>
    /// Limite superior do corredor = nominal + tolerância (manual L344).
    /// </summary>
    public static decimal CorridorUpperMm(decimal nominalGargaloMm, decimal toleranceMm) =>
        nominalGargaloMm + toleranceMm;

    /// <summary>
    /// Regra de fronteira do corredor: ATINGIR o limite cria alerta, CRUZAR
    /// o limite cria alerta e a IGUALDADE no limite conta como alerta
    /// (manual L348-354; spec L107: «Reaching or exceeding either
    /// tolerance boundary raises an alert for that row»). Um valor
    /// exatamente no limite nunca é silenciosamente aceitável.
    /// </summary>
    public static bool IsAlert(decimal valueMm, decimal corridorLowerMm, decimal corridorUpperMm) =>
        valueMm <= corridorLowerMm || valueMm >= corridorUpperMm;

    /// <summary>
    /// Avaliação completa de uma linha de medição contra o corredor do seu
    /// componente: os derivados (ovalização com sinal, média) e os valores
    /// medidos avaliáveis verificados independentemente (costura, contra
    /// costura quando presente, média). Sem nominal gargalo a linha é
    /// NotEvaluable — sem corredor para o componente, nenhum valor
    /// inventado (decisão do Owner; PDF §10 L486).
    /// </summary>
    public static PegamentosRowEvaluation EvaluateRow(
        decimal? nominalGargaloMm,
        decimal toleranceMm,
        decimal costuraMm,
        decimal? contraCosturaMm)
    {
        var ovalizacao = OvalizacaoMm(costuraMm, contraCosturaMm);
        var media = MediaMm(costuraMm, contraCosturaMm);

        if (nominalGargaloMm is not { } nominal)
        {
            // NotEvaluable: os factos observados existem (ovalização/média
            // derivam quando podem), mas não há corredor — nenhum valor é
            // avaliado, nenhum nominal é inventado.
            return new PegamentosRowEvaluation(
                ovalizacao,
                media,
                null,
                null,
                Array.Empty<PegamentosEvaluatedValue>());
        }

        var lower = CorridorLowerMm(nominal, toleranceMm);
        var upper = CorridorUpperMm(nominal, toleranceMm);

        // Costura, contra costura (quando presente) e média verificadas
        // independentemente contra o corredor (manual L338).
        var values = new List<PegamentosEvaluatedValue>
        {
            new(PegamentosEvaluatedAxis.Costura, costuraMm, IsAlert(costuraMm, lower, upper))
        };

        if (contraCosturaMm is { } contraCostura)
        {
            values.Add(new PegamentosEvaluatedValue(
                PegamentosEvaluatedAxis.ContraCostura,
                contraCostura,
                IsAlert(contraCostura, lower, upper)));
        }

        values.Add(new PegamentosEvaluatedValue(
            PegamentosEvaluatedAxis.Media,
            media,
            IsAlert(media, lower, upper)));

        return new PegamentosRowEvaluation(ovalizacao, media, lower, upper, values);
    }
}
