using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Valida a registration DI do Program.cs no host Web real para o corte
/// Controlo ensure+Resumo: o handler do ensure do controlo_id
/// (get-or-create idempotente por jobon_id) e a query purpose-specific
/// do browser «Procurar resumo» (filtro por referência aplicado no
/// servidor) resolvem para as implementações certas com o tempo de vida
/// Scoped exigido. Mantém-se mínimo e seguro — não semeia dados para não
/// interferir com a base InMemory fixa partilhada entre testes.
/// </summary>
public sealed class ControloResumoServicesRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ControloResumoServicesRegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ControloResumoServices_ResolveAsScopedImplementations()
    {
        // Dois scopes independentes provam o tempo de vida Scoped: a mesma
        // instância dentro de um scope e instâncias distintas entre scopes
        // (não é transient nem singleton — evita captive dependency).
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var ensureControlo = scope1.ServiceProvider.GetRequiredService<IEnsureControloForJobOnHandler>();
        Assert.IsType<EnsureControloForJobOnHandler>(ensureControlo);
        Assert.Same(ensureControlo, scope1.ServiceProvider.GetRequiredService<IEnsureControloForJobOnHandler>());
        Assert.NotSame(ensureControlo, scope2.ServiceProvider.GetRequiredService<IEnsureControloForJobOnHandler>());

        var resumoProductions = scope1.ServiceProvider.GetRequiredService<IResumoProductionQuery>();
        Assert.IsType<ResumoProductionQuery>(resumoProductions);
        Assert.Same(resumoProductions, scope1.ServiceProvider.GetRequiredService<IResumoProductionQuery>());
        Assert.NotSame(resumoProductions, scope2.ServiceProvider.GetRequiredService<IResumoProductionQuery>());
    }
}
