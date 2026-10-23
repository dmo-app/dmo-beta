using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DMO.Alpha.Web.Tests;

public sealed class ReadinessPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public ReadinessPageTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ProcessoEFluxoLocal_RespondemSemSessao()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var path in new[] { "/health/live", "/health/ready" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        }
    }

    [Fact]
    public async Task ConfiguracaoAuthIncompleta_NaoEstaPronta_MasProcessoVive()
    {
        using var broken = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Dmo:AuthenticationProvider", "Supabase")
            .UseSetting("Dmo:SupabaseUrl", "")
            .UseSetting("Dmo:SupabasePublishableKey", "")
            .UseSetting("Dmo:SupabaseSecretKey", ""));
        using var client = broken.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }
}
