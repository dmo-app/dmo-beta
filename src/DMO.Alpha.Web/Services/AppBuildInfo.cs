using System.Reflection;
using System.Text.Json;

namespace DMO.Alpha.Web.Services;

public sealed record ReleaseEntry(string Id, string Module, string Description);
public sealed record ReleaseNotes(IReadOnlyList<ReleaseEntry> Fixes, IReadOnlyList<ReleaseEntry> Pending);

/// <summary>Identity and notes embedded in the Web assembly that is actually running.</summary>
public sealed class AppBuildInfo
{
    public static AppBuildInfo Current { get; } = new();

    private AppBuildInfo()
    {
        var assembly = typeof(AppBuildInfo).Assembly;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value ?? string.Empty);
        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "indisponível";
        Revision = metadata.GetValueOrDefault("DmoRevision", "unavailable");
        SourceState = metadata.GetValueOrDefault("DmoSourceState", "unverified");
        BuildId = metadata.GetValueOrDefault("DmoBuildId", "unavailable");
        BuiltAtUtc = DateTimeOffset.TryParse(metadata.GetValueOrDefault("DmoBuiltAtUtc"), out var builtAt) ? builtAt : null;
        using var stream = assembly.GetManifestResourceStream("DMO.Alpha.Web.ReleaseNotes.json")
            ?? throw new InvalidOperationException("Release notes are missing from the Web artifact.");
        Notes = JsonSerializer.Deserialize<ReleaseNotes>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Release notes could not be read.");
    }

    public string Version { get; }
    public string Revision { get; }
    public string SourceState { get; }
    public string BuildId { get; }
    public DateTimeOffset? BuiltAtUtc { get; }
    public ReleaseNotes Notes { get; }
    public string ShortRevision => Revision.Length >= 12 && Revision.All(Uri.IsHexDigit) ? Revision[..12] : "indisponível";
    public string SourceDescription => SourceState switch
    {
        "clean" => "Código sem alterações locais",
        "modified" => "Inclui alterações locais ainda não commitadas",
        _ => "Estado da origem não verificado"
    };
}
