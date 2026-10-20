namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O rasto de uma leitura dos Pegamentos corrigida ou apagada (sweep de
/// 19/10/2026, 4.6; Decisão 001): o valor antigo, o novo, quem e quando,
/// gravado na mesma operação da correção. Corrigir e apagar continuam
/// livres; depois da aprovação, a aprovação mantém-se e a Revisão avisa
/// «Corrigido depois da aprovação» (C9), com este rasto na consulta. Como os
/// movimentos BQ apagados (<c>BqMovementDeletions</c>), guarda uma cópia dos
/// factos da leitura: a leitura apagada já não existe. Só se acrescenta,
/// nunca se muda; sai com a folha.
/// </summary>
public sealed class PegamentosMeasurementHistorico
{
    public int Id { get; set; }

    /// <summary>A folha (pegamentos_id) a que a leitura pertence.</summary>
    public int PegamentosId { get; set; }

    /// <summary>O measurement_id da leitura (sem chave estrangeira: pode ter sido apagada).</summary>
    public int MeasurementId { get; set; }

    /// <summary>
    /// O N.º da leitura no componente quando foi corrigida ou apagada, o que
    /// a folha mostra (as leituras de cada componente contam de 1).
    /// </summary>
    public int Numero { get; set; }

    public PegamentosComponent Component { get; set; }

    /// <summary>True quando a leitura foi apagada; os valores «depois» ficam vazios.</summary>
    public bool Apagada { get; set; }

    public decimal CosturaAntesMm { get; set; }

    public decimal? ContraCosturaAntesMm { get; set; }

    public decimal? NominalGargaloAntesMm { get; set; }

    public decimal? CosturaDepoisMm { get; set; }

    public decimal? ContraCosturaDepoisMm { get; set; }

    public decimal? NominalGargaloDepoisMm { get; set; }

    /// <summary>Quem corrigiu ou apagou (o nome, pelo carimbo dos registos); null sem identidade.</summary>
    public string? Por { get; set; }

    /// <summary>O utilizador de <see cref="Por"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? PorUserId { get; set; }

    public DateTime EmUtc { get; set; }
}
