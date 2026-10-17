using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Compatibilidades Tool–máquina: CM, MF e BQ admitem várias máquinas.
/// Compatibilidade não determina a máquina em que a ferramenta está em uso.
/// A identidade continua a ser o ToolId; lotes diferentes mantêm identidades distintas.
/// </summary>
public sealed class ToolMachineLink
{
    /// <summary>tool_id canónico da Tool ligada (parte da PK composta).</summary>
    public string ToolId { get; set; } = null!;

    public Tool Tool { get; set; } = null!;

    /// <summary>machine_id canónico da máquina ligada (parte da PK composto).</summary>
    public int MachineId { get; set; }

    public Machine Machine { get; set; } = null!;
}
