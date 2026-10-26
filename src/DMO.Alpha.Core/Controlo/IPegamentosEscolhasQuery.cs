using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Read-only lookups the Pegamentos page needs before its tool and Job On
/// choices: the lots of a reference and the machine codes of the Job On picker.
/// Nothing is created or changed.
/// </summary>
public interface IPegamentosEscolhasQuery
{
    /// <summary>
    /// Every lot of the tool type with this reference (case-insensitive), highest
    /// lot first; empty without a reference.
    /// </summary>
    Task<IReadOnlyList<ToolLookupResult>> LotesDaReferenciaAsync(
        string reference,
        ToolType type,
        CancellationToken cancellationToken = default);

    /// <summary>The machine codes, in code order.</summary>
    Task<IReadOnlyList<string>> CodigosDeMaquinasAsync(CancellationToken cancellationToken = default);
}
