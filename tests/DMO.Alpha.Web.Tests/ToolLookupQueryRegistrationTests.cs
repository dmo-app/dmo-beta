using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Valida a registration DI do Program.cs no host Web real: o surface nativo
/// IToolLookupQuery resolve para a implementação ToolLookupQuery com o tempo
/// de vida Scoped exigido. Mantém-se mínimo e seguro — não semeia dados para
/// não interferir com a base InMemory fixa partilhada entre testes.
/// </summary>
public sealed class ToolLookupQueryRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ToolLookupQueryRegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void IToolLookupQuery_ResolvesAsScopedToolLookupQuery()
    {
        // Dois scopes independentes provam o tempo de vida Scoped: a mesma
        // instância dentro de um scope e instâncias distintas entre scopes
        // (não é transient nem singleton — evita captive dependency).
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var query = scope1.ServiceProvider.GetRequiredService<IToolLookupQuery>();

        Assert.IsType<ToolLookupQuery>(query);

        // Scoped, não transient: o mesmo scope devolve a mesma instância.
        Assert.Same(query, scope1.ServiceProvider.GetRequiredService<IToolLookupQuery>());

        // Scoped, não singleton: um scope diferente devolve outra instância.
        Assert.NotSame(query, scope2.ServiceProvider.GetRequiredService<IToolLookupQuery>());
    }
}
