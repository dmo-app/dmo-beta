using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O REGISTO DE COMPARAÇÃO de pesos (o registo do histórico tipo
/// «Comparação» do dmo-html): a comparação das novas leituras dos
/// contra-moldes contra o Peso inicial de uma produção, guardada como facto
/// histórico sob o controlo_id da produção (o html: <c>comparacao_id</c> com
/// <c>controlo_id</c> + <c>peso_id</c> + <c>cm_id</c>). Os inputs consumidos
/// são CONGELADOS no momento da criação como evidência histórica (o padrão
/// do Peso): o grau inteiro da temperatura, as densidades (água e vidro), os
/// volumes e os fatos humanos da ferramenta usada (referência + lote
/// congelados — o histórico sobrevive à edição/eliminação da Tool). Os
/// derivados (pesos dos CM, pares, Δ médios) calculam-se à LEITURA a partir
/// dos inputs congelados pela <see cref="PesoComparacaoCalculation"/> —
/// nunca reescritos por uma edição posterior das Tools ou da configuração.
/// </summary>
public sealed class PesoComparacao
{
    public int Id { get; set; }

    /// <summary>
    /// controlo_id da produção a que esta comparação pertence — o registo
    /// vive sob o controlo (a LEI da app: o contexto vem do jobon pelo
    /// controlo_id). Obrigatório: a comparação compara contra o Peso
    /// inicial de UMA produção.
    /// </summary>
    public int ControloId { get; set; }

    public Controlo Controlo { get; set; } = null!;

    /// <summary>peso_id do Peso inicial (a referência da comparação).</summary>
    public int PesoInicialId { get; set; }

    public Peso PesoInicial { get; set; } = null!;

    /// <summary>tool_id canónica do contra-molde pesado (CM).</summary>
    public string CmToolId { get; set; } = null!;

    public Tool CmTool { get; set; } = null!;

    /// <summary>
    /// Grau inteiro da temperatura da água (°C) usado no lookup da tabela
    /// canónica — o valor consumido, persistido (o padrão do Peso).
    /// </summary>
    public int WaterTemperatureC { get; set; }

    /// <summary>Densidade da água (g/cm³) consumida — evidência histórica congelada.</summary>
    public decimal WaterDensityGramsPerCm3 { get; set; }

    /// <summary>Densidade do vidro (g/cm³) consumida — evidência histórica congelada.</summary>
    public decimal GlassDensityGramsPerCm3 { get; set; }

    /// <summary>volume_marisa (cm³) consumido do Peso inicial — congelado.</summary>
    public decimal VolumeMarisaCm3 { get; set; }

    /// <summary>volume_puncao (cm³) consumido — congelado.</summary>
    public decimal VolumePuncaoCm3 { get; set; }

    /// <summary>Referência canónica da CM congelada à criação (evidência histórica).</summary>
    public string? CmToolReferenceFrozen { get; set; }

    /// <summary>Lote da CM congelado à criação (evidência histórica).</summary>
    public string? CmToolLotFrozen { get; set; }

    /// <summary>Nota do operador (opcional), como no registo do html.</summary>
    public string? Note { get; set; }

    /// <summary>O momento da confirmação (UTC, gravado pelo backend) — a data do histórico.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>As novas leituras dos contra-moldes (factos históricos append-only).</summary>
    public ICollection<PesoComparacaoMeasurement> Measurements { get; set; } = new List<PesoComparacaoMeasurement>();
}

/// <summary>
/// Uma leitura de contra-molde da comparação (measurement row): o
/// número/posição CM visível é dado de medição ao nível da linha (nunca um
/// tool_id), a posição é a ordem de registo e a massa de água observada é
/// registada na totalidade — facto histórico, nunca reescrito nem apagado.
/// </summary>
public sealed class PesoComparacaoMeasurement
{
    public int Id { get; set; }

    public int ComparacaoId { get; set; }

    public PesoComparacao Comparacao { get; set; } = null!;

    /// <summary>Posição da leitura na comparação (1-based, ordem de registo).</summary>
    public int RowPosition { get; set; }

    /// <summary>Número/posição CM visível registado pelo operador (row-level measurement data).</summary>
    public string CmSubject { get; set; } = null!;

    /// <summary>Massa de água medida (g) — o valor observado, na totalidade.</summary>
    public decimal WaterWeightGrams { get; set; }
}
