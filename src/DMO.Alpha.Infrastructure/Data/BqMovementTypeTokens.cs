using DMO.Alpha.Core.Boquilhas;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Conversão entre o <see cref="BqMovementType"/> e o token canónico
/// persistido: exatamente "saida", "entrada" e "entrada_sem_reparacao".
/// Os conversores do EF Core são árvores de expressão, pelo que o
/// switch/throw vive aqui dentro de chamadas de método simples.
/// </summary>
internal static class BqMovementTypeTokens
{
    public static string ToStorage(BqMovementType type) => type switch
    {
        BqMovementType.Saida => "saida",
        BqMovementType.Entrada => "entrada",
        BqMovementType.EntradaSemReparacao => "entrada_sem_reparacao",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de movimento desconhecido.")
    };

    public static BqMovementType FromStorage(string token) => token switch
    {
        "saida" => BqMovementType.Saida,
        "entrada" => BqMovementType.Entrada,
        "entrada_sem_reparacao" => BqMovementType.EntradaSemReparacao,
        _ => throw new FormatException($"Tipo de movimento desconhecido: {token}.")
    };
}
