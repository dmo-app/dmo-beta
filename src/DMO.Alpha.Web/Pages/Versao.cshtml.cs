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
    public void OnGet() { }

    public IActionResult OnGetMetadata() => new JsonResult(new
    {
        environment = EnvironmentLabel,
        version = Build.Version,
        revision = Build.Revision,
        sourceState = Build.SourceState,
        buildId = Build.BuildId,
        builtAtUtc = Build.BuiltAtUtc,
        fixes = Build.Notes.Fixes,
        pending = Build.Notes.Pending
    });
}
