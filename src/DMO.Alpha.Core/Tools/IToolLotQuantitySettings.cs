namespace DMO.Alpha.Core.Tools;

public static class ToolLotQuantityKeys
{
    public const string Prefix = "Ferramentas:LotQuantity:";
    public static string For(ToolType type, string machineCode) => Prefix + type.ToString().ToUpperInvariant() + ":" + machineCode;
}

public sealed record ToolLotQuantityDefault(int MachineId, string MachineCode, ToolType Type, int? Quantity)
{
    public string SettingKey => ToolLotQuantityKeys.For(Type, MachineCode);
}

public sealed record ToolLotQuantitySuggestion(int? Quantity, IReadOnlyList<ToolLotQuantityDefault> Machines);
public enum SaveToolLotQuantitiesResult { Success, UnknownDefinition }

/// <summary>Uma quantidade submetida que não foi gravada: ilegível ou negativa; a definição mantém o valor anterior.</summary>
public sealed record IgnoredToolLotQuantity(string MachineCode, ToolType Type, string Submitted);

/// <summary>
/// O resultado de gravar as quantidades por lote. Com Success, as válidas
/// ficaram gravadas e <see cref="Ignored"/> lista as que ficaram de fora
/// (decisão do proprietário, 16/10/2026: avisar em vez de recusar tudo).
/// </summary>
public sealed record SaveToolLotQuantitiesOutcome(SaveToolLotQuantitiesResult Result, IReadOnlyList<IgnoredToolLotQuantity> Ignored)
{
    public static SaveToolLotQuantitiesOutcome Refused(SaveToolLotQuantitiesResult result) => new(result, Array.Empty<IgnoredToolLotQuantity>());
}

public interface IToolLotQuantitySettings
{
    Task<IReadOnlyList<ToolLotQuantityDefault>> ListAsync(CancellationToken cancellationToken = default);
    Task<ToolLotQuantitySuggestion> SuggestAsync(ToolType type, IReadOnlyList<int> machineIds, CancellationToken cancellationToken = default);
    Task<SaveToolLotQuantitiesOutcome> SaveAsync(IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);
}
