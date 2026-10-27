namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O pedido do «Comparar peso» (Diogo, 27/10/2026): o Peso de onde se parte
/// (na página do Peso) ou a referência da CM escrita no Histórico, e a
/// máquina. Sem referência nem máquina, valem as do Peso.
/// </summary>
public sealed record PesoCompararPedido(
    int? PesoId = null,
    string? CmReferencia = null,
    string? Maquina = null);

/// <summary>
/// As CM compatíveis com o pedido: a mesma referência da CM e a mesma
/// máquina (Diogo, 27/10/2026: «escolher CM compatíveis: mesma referência e
/// máquina»), o Peso mais recente de cada CM.
/// </summary>
public sealed record PesoCompararCms(
    int? PesoBaseId,
    string? CmReferencia,
    string? Maquina,
    /// <summary>As referências de CM que começam pelo texto escrito, para escolher quando há mais do que uma.</summary>
    IReadOnlyList<string> Referencias,
    /// <summary>As máquinas com Pesos desta referência.</summary>
    IReadOnlyList<string> Maquinas,
    IReadOnlyList<PesoCompararLinha> Linhas);

/// <summary>
/// Um Peso na lista do «Comparar peso»: a CM (o lote), a produção, o tampão
/// com que se pesou e o peso médio do vidro.
/// </summary>
public sealed record PesoCompararLinha(
    int PesoId,
    string CmToolId,
    string? CmReferencia,
    string? CmLote,
    string? Referencia,
    string? Producao,
    string? Maquina,
    DateTime? RegistadoEmUtc,
    int Leituras,
    decimal? PesoMedioGrams,
    decimal GlassDensityGramsPerCm3,
    /// <summary>O tampão congelado no Peso; sem ele, o do Job On e depois o da ficha da CM. Null quando não há nenhum.</summary>
    PesoTampao? Tampao);

/// <summary>A consulta do «Comparar peso». Só leitura.</summary>
public interface IPesoCompararQuery
{
    Task<PesoCompararCms> ListarAsync(PesoCompararPedido pedido, CancellationToken cancellationToken = default);
}
