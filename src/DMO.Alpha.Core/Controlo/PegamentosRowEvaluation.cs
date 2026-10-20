namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O eixo/valor de uma linha de Pegamentos que entra na verificação
/// independente do corredor (manual §7 L338: «Costura, Contra costura e
/// Média são verificadas independentemente»).
/// </summary>
public enum PegamentosEvaluatedAxis
{
    Costura = 1,

    ContraCostura = 2,

    Media = 3,

    /// <summary>
    /// A ovalização com sinal: alerta quando toca ou passa a tolerância,
    /// para mais ou para menos (Diogo, 19/10/2026, F8: «se qualquer medição
    /// ultrapassar os 0.2, seja -0.2 ou +0.2»).
    /// </summary>
    Ovalizacao = 4
}

/// <summary>
/// Um valor medido/derivado avaliado contra o corredor do seu componente:
/// o valor e se atingiu ou ultrapassou um limite (alerta informativo —
/// manual L360: «Os alertas dimensionais não bloqueiam automaticamente a
/// produção. Eles avisam, destacam e suportam a análise humana»).
/// </summary>
public sealed record PegamentosEvaluatedValue(
    PegamentosEvaluatedAxis Axis,
    decimal ValueMm,
    bool Alert);

/// <summary>
/// Avaliação backend de uma linha de medição de Pegamentos: os derivados
/// canónicos (ovalização com sinal preservado — null quando a contra
/// costura está ausente; média) e o corredor contra o qual a linha foi
/// avaliada (nominal congelado na linha ± tolerância congelada no registo).
/// <see cref="IsNotEvaluable"/> quando a linha não tem nominal — sem
/// corredor para o componente, estado válido (nenhum nominal inventado).
/// <see cref="HasAlert"/> quando qualquer um dos valores avaliáveis
/// atingiu ou ultrapassou um limite: alerta informativo da linha que
/// nenhuma média pode cancelar (spec L109-110).
/// </summary>
public sealed record PegamentosRowEvaluation(
    decimal? OvalizacaoMm,
    decimal MediaMm,
    decimal? CorridorLowerMm,
    decimal? CorridorUpperMm,
    IReadOnlyList<PegamentosEvaluatedValue> Values)
{
    /// <summary>True quando a linha não tem corredor (nominal gargalo ausente).</summary>
    public bool IsNotEvaluable => CorridorLowerMm is null;

    /// <summary>True quando algum valor avaliável atingiu ou ultrapassou um limite do corredor.</summary>
    public bool HasAlert => Values.Any(value => value.Alert);
}
