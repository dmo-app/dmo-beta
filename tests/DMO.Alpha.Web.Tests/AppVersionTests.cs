using System.Net;
using System.Text.Json;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DMO.Alpha.Web.Tests;

public sealed class AppVersionTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Dmo:EnvironmentLabel", "DEV");
            builder.UseSetting("ConnectionStrings:DmoDatabase", "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Timeout=1");
            builder.UseSetting("Dmo:DevConnectionProbe", "false");
            builder.UseSetting("Dmo:Version", "fake-runtime-version");
            builder.UseSetting("RAILWAY_GIT_COMMIT_SHA", "fake-runtime-revision");
            builder.UseSetting("Dmo:DataProtectionKeyRingPath", Path.Combine(Path.GetTempPath(), "dmo-version-tests", Guid.NewGuid().ToString("N")));
        });

    [Fact]
    public async Task Metadata_IdentifiesActualArtifact_WithoutLoginOrDatabase_AndCannotBeRenamedByRuntimeSettings()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/versao?handler=Metadata");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal($"dmo-metadata-{AppBuildInfo.Current.Version}.json", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = json.RootElement;
        Assert.Equal("DEV", metadata.GetProperty("environment").GetString());
        Assert.Equal(AppBuildInfo.Current.Version, metadata.GetProperty("version").GetString());
        Assert.Equal(AppBuildInfo.Current.Revision, metadata.GetProperty("revision").GetString());
        Assert.Matches("^[a-f0-9]{32}$", metadata.GetProperty("buildId").GetString()!);
        Assert.Equal(TimeSpan.Zero, metadata.GetProperty("builtAtUtc").GetDateTimeOffset().Offset);
        Assert.DoesNotContain("fake-runtime", await response.Content.ReadAsStringAsync());
        var fixes = metadata.GetProperty("fixes").EnumerateArray().Select(f => f.GetProperty("id").GetString()).ToArray();
        Assert.Contains("DMO-009", fixes);
        Assert.Equal(fixes.Length, fixes.Distinct().Count());
        Assert.True(metadata.GetProperty("pending").GetArrayLength() > 0);
    }

    [Fact]
    public async Task LoginBadge_LinksToVersion_AndVersionPageListsFixesAndPendingWork()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/login");
        Assert.Contains($"data-dmo-version=\"{AppBuildInfo.Current.Version}\"", login);
        Assert.Contains($"data-dmo-build=\"{AppBuildInfo.Current.BuildId}\"", login);
        Assert.Contains("href=\"/versao\"", login);
        var page = await client.GetStringAsync("/versao");
        Assert.Contains(">Correções</h3>", page);
        Assert.Contains(AppBuildInfo.Current.Notes.Fixes[^1].Id, page);   // a mais recente primeiro
        Assert.DoesNotContain("DMO-001 ·", page);
        Assert.DoesNotContain(">Consultar metadata<", page);
        Assert.Contains("Descarregar metadata (.json)", page);
        Assert.Contains("DMO-P02", page);
        Assert.Contains(AppBuildInfo.Current.Revision, page);
        Assert.Contains(AppBuildInfo.Current.BuildId, page);
    }

    [Fact]
    public async Task VersionPage_PagesTheFixes_TenPerPage_NewestFirst()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var fixes = AppBuildInfo.Current.Notes.Fixes;
        var pages = (int)Math.Ceiling(fixes.Count / 10.0);

        var first = await client.GetStringAsync("/versao");
        Assert.Contains($">1 de {pages}</span>", first);
        Assert.Contains($"{fixes[^1].Id} ·", first);
        Assert.Contains($"{fixes[^10].Id} ·", first);
        Assert.DoesNotContain($"{fixes[^11].Id} ·", first);
        Assert.Contains("/versao?pagina=2#correcoes", first);

        var last = await client.GetStringAsync($"/versao?pagina={pages}");
        Assert.Contains($">{pages} de {pages}</span>", last);
        Assert.Contains("DMO-001 ·", last);
        Assert.DoesNotContain($"{fixes[^1].Id} ·", last);
        Assert.Contains("DMO-P02", last);   // os pendentes aparecem em todas as páginas
    }
}
