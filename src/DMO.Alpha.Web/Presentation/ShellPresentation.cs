namespace DMO.Alpha.Web.Presentation;

// Presentation values only. This contract neither resolves authorization nor
// models production, movements, workflow or any other domain entity.
public sealed record ShellPresentation(
    ModulePresentation? CurrentModule,
    string ActiveShellDestination,
    string? ActiveFunctionKey,
    IReadOnlyList<FunctionPresentation> Functions,
    // Primary nav destinations of this build (H-3 wayfinding): the shell bar
    // shows what exists — real hrefs for live surfaces, never only a disabled
    // placeholder. Selected marks the current module's entry.
    IReadOnlyList<DestinationPresentation> Destinations,
    DestinationPresentation Planeamento,
    SessionPresentation Session,
    IReadOnlyDictionary<string, ActionAvailability> ActionAvailability,
    SidepanelPresentation? Sidepanel = null,
    // Atalhos no fim da barra de funções para outra página do mesmo nome
    // (os dois Controlos); o «with { Functions = … }» das páginas mantém-nos.
    IReadOnlyList<FunctionPresentation>? ModuleLinks = null);

/// <summary>
/// Shell-owned side panel state for the active destination. Presentation only:
/// the side panel is a shell component; which variant is open (or closed) is a
/// shared shell state, not a module page of its own.
/// </summary>
public enum ShellSidepanelKind
{
    /// <summary>No side panel is open for the active destination.</summary>
    None = 0,

    /// <summary>Planning side panel: current day first, then the following productions.</summary>
    Planeamento = 1,

    /// <summary>Boquilhas machine side panel: production machines with the current BQ context.</summary>
    Boquilhas = 2
}

public sealed record SidepanelPresentation(ShellSidepanelKind Kind, bool Visible);

public sealed record ModulePresentation(string Key, string Title);
public sealed record DestinationPresentation(string Label, string Href, bool Selected, bool Visible, bool Enabled);
/// <summary>
/// Um separador da barra de funções. Com <paramref name="Children"/>, o
/// separador é um menu (o «Funções» do Controlo) e as entradas abrem num
/// dropdown; fica selecionado quando uma delas está.
/// </summary>
public sealed record FunctionPresentation(
    string Key, string Label, string? Href, string? PanelTarget,
    bool Selected, bool Visible, bool Enabled, string CssClass = "",
    IReadOnlyList<FunctionPresentation>? Children = null);
public sealed record SessionPresentation(string Name, string Title, string LogoutHref, bool ShowLogout = true);
public sealed record ActionAvailability(bool Visible, bool Enabled);
