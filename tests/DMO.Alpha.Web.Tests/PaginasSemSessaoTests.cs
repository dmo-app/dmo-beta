using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Sem sessão só abrem as páginas públicas (vistoria ao backend, 27/10/2026):
/// a regra por omissão pede sessão, e uma página nova que se esqueça do
/// [Authorize] fica fechada. Uma página pública nova tem de entrar nesta lista.
/// </summary>
public sealed class PaginasSemSessaoTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public void PorOmissao_TodasAsPaginasPedemSessao()
    {
        var opcoes = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.NotNull(opcoes.FallbackPolicy);
        Assert.Contains(opcoes.FallbackPolicy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void SemSessao_SoAbremAsPaginasPublicas()
    {
        var publicas = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => (e.RoutePattern.RawText ?? string.Empty).Trim('/').ToLowerInvariant())
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["error", "favicon.ico", "instalar", "login", "logout", "posto", "versao"],
            publicas);
    }
}
