namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Contexto técnico estável de uma Tool canónica (regra Ferramentas «Tool-owned
/// technical values»): factos reutilizáveis do próprio <c>tool_id</c> que não
/// variam por módulo nem por produção. Uma linha 1:1 por Tool, dona do próprio
/// tool_id — nunca uma cópia privada de uma função consumidora. Neste corte
/// persistem-se os valores consumidos pelo Peso (CM → processo, peso nominal e
/// volume_puncao; BQ → volume_marisa) e o diametro_gargalo consumido pelos
/// Pegamentos (corte Pegamentos production-scoped: coluna aditiva que nasce
/// NULL — o valor é completado depois a partir do registo da Tool ou do fluxo
/// da folha de Pegamentos, gravando sempre no tool_id owner; a tool continua
/// a única dona editável do facto — a folha não cria cópia concorrente).
/// A geometria TP de referência D/R_draw/h_ref (+ R base, R esfera e volume
/// derivados do desenho) pertence ao corte A (Ferramentas): as 6 colunas
/// Tp* persistem os valores DO DESENHO tal como registados — a app NUNCA
/// recalcula os derivados (R base = D/2, R esfera, volume): registo
/// truthful, os valores derivados são registados pelo operador como factos
/// da folha (worked example: valores reais da folha genesis — D 28,45 ·
/// R 24,11 · sagitta 4,5 · R base 14,225 · R esfera 28,84061800082953 ·
/// volume 1438,3860504277206 mm³).
/// Os valores são opcionais: podem não existir quando o tool_id é criado e são
/// completados depois.
/// </summary>
public sealed class ToolTechnicalContext
{
    /// <summary>tool_id canónico — PK e FK 1:1 para a Tool dona dos valores.</summary>
    public string ToolId { get; set; } = null!;

    public Tool Tool { get; set; } = null!;

    /// <summary>
    /// Processo de fabrico da CM (NNPB/PS). Contexto CM Tool-owned consumido
    /// por fluxos como o Peso (resolução da densidade do vidro por processo).
    /// Null enquanto não estiver registado.
    /// </summary>
    public CmProcess? Process { get; set; }

    /// <summary>
    /// Peso nominal da CM (g), consumido pelo Peso como contexto de exibição.
    /// Null enquanto não estiver registado; a ausência não bloqueia o Peso.
    /// </summary>
    public decimal? NominalWeightGrams { get; set; }

    /// <summary>
    /// volume_puncao da CM (cm³), termo do cálculo principal do Peso.
    /// Null enquanto não estiver registado.
    /// </summary>
    public decimal? VolumePuncaoCm3 { get; set; }

    /// <summary>
    /// volume_marisa da BQ (cm³), termo do cálculo principal do Peso.
    /// Null enquanto não estiver registado.
    /// </summary>
    public decimal? VolumeMarisaCm3 { get; set; }

    /// <summary>
    /// diametro_gargalo (mm) — o nominal gargalo da Tool (CM, BQ ou MF),
    /// consumido pelos Pegamentos como o centro do corredor de tolerância
    /// por componente (spec Controlo Create — Pegamentos L103: «The normal
    /// tolerance is the Tool-owned nominal gargalo value ± 0.20 mm»;
    /// L146-148: cm_id/bq_id/mf_id → tool_id → gargalo). Null enquanto não
    /// estiver registado: a ausência é um estado válido (a tool sem gargalo
    /// deixa o componente NotEvaluable, nenhum nominal é inventado) e o
    /// valor é completado no fluxo da folha de Pegamentos, gravando neste
    /// tool_id owner — nunca no registo consumidor.
    /// </summary>
    public decimal? DiametroGargaloMm { get; set; }

    /// <summary>
    /// TP — diâmetro de abertura D (mm) do desenho da CM. Geometria de
    /// referência TP/calote, facto estável do tool_id (regra «TP drawing/
    /// reference geometry»: não varia por módulo nem por produção; a mesma
    /// tool corrige-a no mesmo tool_id, um lote diferente é uma tool nova).
    /// Null enquanto não registada.
    /// </summary>
    public decimal? TpDiametroMm { get; set; }

    /// <summary>
    /// TP — raio do desenho R_draw (mm). Null enquanto não registado.
    /// </summary>
    public decimal? TpRaioMm { get; set; }

    /// <summary>
    /// TP — profundidade de calote de referência/original h_ref (mm).
    /// Null enquanto não registado.
    /// </summary>
    public decimal? TpSagittaMm { get; set; }

    /// <summary>
    /// TP — raio da base (Ø/2) do desenho (mm), registado tal como vem da
    /// folha: a app NUNCA recalcula derivados (registo truthful).
    /// Null enquanto não registado.
    /// </summary>
    public decimal? TpRaioBaseMm { get; set; }

    /// <summary>
    /// TP — raio da esfera perfeita (mm) derivado do desenho, registado tal
    /// como vem da folha (nunca recalculado pela app). Null enquanto não
    /// registado.
    /// </summary>
    public decimal? TpRaioEsferaMm { get; set; }

    /// <summary>
    /// TP — volume da calote (mm³) derivado do desenho, registado tal como
    /// vem da folha (nunca recalculado pela app). Null enquanto não
    /// registado.
    /// </summary>
    public decimal? TpVolumeMm3 { get; set; }

    /// <summary>
    /// O link do mcaliper, onde se registam as medições desta ferramenta
    /// (Diogo, 17/10/2026). Associado no Controlo Criar; o Controlo Aprovar
    /// copia-o. Null enquanto não associado.
    /// </summary>
    public string? McaliperUrl { get; set; }

    /// <summary>O comprimento máximo do link do mcaliper.</summary>
    public const int McaliperUrlMaxLength = 2000;
}
