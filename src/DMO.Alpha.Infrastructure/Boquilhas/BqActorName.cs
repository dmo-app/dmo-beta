namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// O nome de quem corrige ou apaga um movimento, tal como fica congelado:
/// sem espaços à volta, cortado aos 254 caracteres da coluna; vazio passa a null.
/// </summary>
internal static class BqActorName
{
    public const int MaxLength = 254;

    public static string? Freeze(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }
}
