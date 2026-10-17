namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Uma leitura dimensional da folha de Pegamentos (measurement row): o
/// gesto em cruz do operador — <see cref="CosturaMm"/> é o eixo 0°
/// (costura) e <see cref="ContraCosturaMm"/> é o eixo 90° (contra costura)
/// do componente identificado pela linha (spec Controlo Create —
/// Pegamentos L94-101: «Costura is the 0° axis; Contra costura is the 90°
/// axis»). A identidade é o measurement_id emitido pelo backend; a linha
/// pertence obrigatoriamente a um <c>pegamentos_id</c>. A costura é o facto
/// base da medição (sempre registada); a contra costura é NULL quando a
/// geometria real não permite uma medição válida a 90° (manual §7 L318-326:
/// «Uma medição nunca é bloqueada apenas porque um dos lados/eixos não está
/// presente... Contra costura em falta (NULL)» — sem bloqueio da base de
/// dados, sem 23502) — a ovalização fica indefinida e a média assume a
/// costura. O <see cref="NominalGargaloMm"/> congela nesta linha, no
/// momento da medição, o nominal gargalo da tool do componente usado na
/// avaliação do corredor (a evidência histórica guarda o valor usado;
/// null quando a tool não tem gargalo — estado NotEvaluable, nunca um
/// nominal inventado). Facto histórico append-only: registado na
/// totalidade, nunca reescrito, compensado nem apagado.
/// </summary>
public sealed class PegamentosMeasurement
{
    public int Id { get; set; }

    public int PegamentosId { get; set; }

    public Pegamentos Pegamentos { get; set; } = null!;

    /// <summary>Posição física da leitura na folha (1-based, ordem de registo).</summary>
    public int RowPosition { get; set; }

    /// <summary>Componente medido (CM/BQ/MF) — a identificação preservada da linha.</summary>
    public PegamentosComponent Component { get; set; }

    /// <summary>Costura — o eixo 0° (mm), o facto base sempre registado.</summary>
    public decimal CosturaMm { get; set; }

    /// <summary>
    /// Contra costura — o eixo 90° (mm). Null quando não medida ou não
    /// aplicável à geometria real do componente (nunca um segundo eixo
    /// fabricado para satisfazer a fórmula — spec L118).
    /// </summary>
    public decimal? ContraCosturaMm { get; set; }

    /// <summary>
    /// Nominal gargalo (mm) da tool do componente consumido pela avaliação
    /// do corredor DESTA linha, congelado no momento da medição como
    /// evidência histórica. Null quando a tool não tinha gargalo registado
    /// nem foi completado no fluxo — a linha fica NotEvaluable (sem
    /// corredor para este componente; nenhum nominal é inventado).
    /// </summary>
    public decimal? NominalGargaloMm { get; set; }
}
