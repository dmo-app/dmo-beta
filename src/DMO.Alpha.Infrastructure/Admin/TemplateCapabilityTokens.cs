namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// Canonical storage serialization of a Template's capability strings
/// (suite precedent: <c>ToolTypeTokens</c>/<c>BqMovementTypeTokens</c>).
/// The persisted form is a single text column holding the de-duplicated,
/// ordinal-sorted, comma-joined capability names — a deterministic SET
/// representation whose value is identical regardless of the order in
/// which the capabilities were provided. Capability names themselves
/// (dots, letters) never contain the separator, and the set members are
/// validated by the owning services (the Beta vocabulary — see
/// <c>DmoCapabilities</c>), never by the storage.
/// </summary>
public static class TemplateCapabilityTokens
{
    /// <summary>Set member separator of the persisted form.</summary>
    public const string Separator = ",";

    /// <summary>
    /// Serializes capability names into the canonical storage string:
    /// trimmed, empty entries dropped, de-duplicated (ordinal), sorted
    /// (ordinal), comma-joined. An empty/absent set serializes to the
    /// empty string.
    /// </summary>
    public static string ToStorage(IReadOnlyList<string> capabilities)
    {
        return string.Join(
            Separator,
            capabilities
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Parses the canonical storage string back into the capability set
    /// (empty for null/blank storage).
    /// </summary>
    public static IReadOnlyList<string> FromStorage(string? storage)
    {
        if (string.IsNullOrWhiteSpace(storage))
        {
            return [];
        }

        return storage.Split(
            Separator,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
