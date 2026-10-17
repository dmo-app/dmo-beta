using System.Text.Json;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class VersaoModel(IConfiguration configuration, IWebHostEnvironment environment) : PageModel
{
    public string EnvironmentLabel => configuration["Dmo:EnvironmentLabel"] ?? environment.EnvironmentName;
    public AppBuildInfo Build => AppBuildInfo.Current;

    /// <summary>As correções por página (o Owner 2026-10-17: «aplicar paginaçao
    /// na pagina das versoes […] assim mantemos uma pagina decente»).</summary>
    public const int FixesPageSize = 10;

    public PagedList<ReleaseEntry> Fixes { get; private set; } = PagedList<ReleaseEntry>.Empty;
    public ListPager FixesPager => ListPager.For(Fixes, fragment: "correcoes");

    // As notas acumulam-se pela ordem em que entram: a mais recente aparece primeiro.
    public void OnGet(int? pagina) =>
        Fixes = PagedList<ReleaseEntry>.FromLoaded(Build.Notes.Fixes.Reverse().ToList(), pagina, FixesPageSize);

    // Descarrega-se como ficheiro (dmo-metadata-<versão>.json), não abre no browser.
    public IActionResult OnGetMetadata() => File(JsonSerializer.SerializeToUtf8Bytes(new
    {
        environment = EnvironmentLabel,
        version = Build.Version,
        revision = Build.Revision,
        sourceState = Build.SourceState,
        buildId = Build.BuildId,
        builtAtUtc = Build.BuiltAtUtc,
        fixes = Build.Notes.Fixes,
        pending = Build.Notes.Pending
    }, MetadataJson), "application/json", $"dmo-metadata-{Build.Version}.json");

    private static readonly JsonSerializerOptions MetadataJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
