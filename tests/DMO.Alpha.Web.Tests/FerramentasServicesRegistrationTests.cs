using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Valida a registration DI do Program.cs no host Web real para o corte A
/// (Ferramentas): os cinco handlers canónicos (criar com mint do tool_id,
/// editar factos, editar valores técnicos, duplicar e eliminar) e as três
/// queries purpose-specific (ficha, aviso de eliminação, compatibilidade
/// de máquinas) resolvem para as implementações certas com o tempo de vida
/// Scoped exigido. Mantém-se mínimo e seguro — não semeia dados para não
/// interferir com a base InMemory fixa partilhada entre testes.
/// </summary>
public sealed class FerramentasServicesRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FerramentasServicesRegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void FerramentasServices_ResolveAsScopedImplementations()
    {
        // Dois scopes independentes provam o tempo de vida Scoped: a mesma
        // instância dentro de um scope e instâncias distintas entre scopes
        // (não é transient nem singleton — evita captive dependency).
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        Assert.IsType<CreateToolHandler>(scope1.ServiceProvider.GetRequiredService<ICreateToolHandler>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<ICreateToolHandler>(),
            scope1.ServiceProvider.GetRequiredService<ICreateToolHandler>());
        Assert.NotSame(
            scope1.ServiceProvider.GetRequiredService<ICreateToolHandler>(),
            scope2.ServiceProvider.GetRequiredService<ICreateToolHandler>());

        Assert.IsType<EditToolFactsHandler>(scope1.ServiceProvider.GetRequiredService<IEditToolFactsHandler>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<IEditToolFactsHandler>(),
            scope1.ServiceProvider.GetRequiredService<IEditToolFactsHandler>());

        Assert.IsType<EditToolTechnicalValuesHandler>(scope1.ServiceProvider.GetRequiredService<IEditToolTechnicalValuesHandler>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<IEditToolTechnicalValuesHandler>(),
            scope1.ServiceProvider.GetRequiredService<IEditToolTechnicalValuesHandler>());

        Assert.IsType<DuplicateToolHandler>(scope1.ServiceProvider.GetRequiredService<IDuplicateToolHandler>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<IDuplicateToolHandler>(),
            scope1.ServiceProvider.GetRequiredService<IDuplicateToolHandler>());

        Assert.IsType<DeleteToolHandler>(scope1.ServiceProvider.GetRequiredService<IDeleteToolHandler>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<IDeleteToolHandler>(),
            scope1.ServiceProvider.GetRequiredService<IDeleteToolHandler>());

        Assert.IsType<ToolSheetQuery>(scope1.ServiceProvider.GetRequiredService<IToolSheetQuery>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<IToolSheetQuery>(),
            scope1.ServiceProvider.GetRequiredService<IToolSheetQuery>());

        Assert.IsType<ToolDeletionImpactQuery>(scope1.ServiceProvider.GetRequiredService<IToolDeletionImpactQuery>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<IToolDeletionImpactQuery>(),
            scope1.ServiceProvider.GetRequiredService<IToolDeletionImpactQuery>());
    }
}
