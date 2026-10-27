namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O rasto de uma leitura do Peso corrigida ou apagada (Decisão 001, como o
/// <see cref="PegamentosMeasurementHistorico"/>): o valor antigo, o novo,
/// quem e quando, gravado na mesma operação da correção. Guarda uma cópia
/// dos factos da leitura: a leitura apagada já não existe. Só se acrescenta,
/// nunca se muda; sai com o Peso.
/// </summary>
public sealed class PesoMeasurementHistorico
{
    public int Id { get; set; }

    /// <summary>O Peso (peso_id) a que a leitura pertence.</summary>
    public int PesoId { get; set; }

    /// <summary>O measurement_id da leitura (sem chave estrangeira: pode ter sido apagada).</summary>
    public int MeasurementId { get; set; }

    /// <summary>A posição da leitura no Peso quando foi corrigida ou apagada.</summary>
    public int Numero { get; set; }

    /// <summary>True quando a leitura foi apagada; os valores «depois» ficam vazios.</summary>
    public bool Apagada { get; set; }

    public string CmSubjectAntes { get; set; } = string.Empty;

    public decimal WaterWeightAntesGrams { get; set; }

    public string? CmSubjectDepois { get; set; }

    public decimal? WaterWeightDepoisGrams { get; set; }

    /// <summary>Quem corrigiu ou apagou (o nome, pelo carimbo dos registos); null sem identidade.</summary>
    public string? Por { get; set; }

    /// <summary>O utilizador de <see cref="Por"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão e quando o utilizador foi eliminado.</summary>
    public Guid? PorUserId { get; set; }

    public DateTime EmUtc { get; set; }
}
