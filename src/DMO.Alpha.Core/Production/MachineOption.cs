namespace DMO.Alpha.Core.Production;

/// <summary>
/// Read model purpose-specific da máquina de produção persistida, para
/// seleção humana explícita no fluxo de criação do Job On: o machine_id
/// canónico (única identidade) e o code descritivo tal como persistido.
/// </summary>
public sealed record MachineOption(int MachineId, string Code);
