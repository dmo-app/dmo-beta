namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Uma leitura física do Peso (measurement row). A identidade é o
/// measurement_id emitido pelo backend; a linha pertence obrigatoriamente a
/// um <c>peso_id</c>. O número/posição CM visível usado dentro do Peso
/// (CmSubject) é dado de medição ao nível da linha — não é outro
/// <c>cm_id</c> nem outro <c>tool_id</c> — e a RowPosition é a posição física
/// da leitura pela ordem de registo (dado usado pelo correspondência de
/// linhas entre Pesos). A massa de água medida é um facto histórico:
/// registada na totalidade, nunca truncada, compensada, reescrita nem
/// apagada.
/// </summary>
public sealed class PesoMeasurement
{
    public int Id { get; set; }

    public int PesoId { get; set; }

    public Peso Peso { get; set; } = null!;

    /// <summary>Posição física da leitura no Peso (1-based, ordem de registo).</summary>
    public int RowPosition { get; set; }

    /// <summary>
    /// Número/posição CM visível registado pelo operador para esta leitura —
    /// row-level measurement data, não identidade canónica.
    /// </summary>
    public string CmSubject { get; set; } = null!;

    /// <summary>Massa de água medida (g) — o valor observado, na totalidade.</summary>
    public decimal WaterWeightGrams { get; set; }
}
