using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Valida a registration DI do Program.cs no host Web real para o corte
/// Pegamentos production-scoped: o handler do registo de medições (a
/// primeira medição cria o registo durável com as tools e a tolerância
/// congeladas; o gargalo em falta é completado no tool_id owner), a query
/// purpose-specific da folha por controlo_id e a leitura da configuração
/// da tolerância resolvem para as implementações certas com o tempo de
/// vida Scoped exigido. Mantém-se mínimo e seguro — não semeia dados para
/// não interferir com a base InMemory fixa partilhada entre testes.
/// </summary>
public sealed class ControloPegamentosServicesRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ControloPegamentosServicesRegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ControloPegamentosServices_ResolveAsScopedImplementations()
    {
        // Dois scopes independentes provam o tempo de vida Scoped: a mesma
        // instância dentro de um scope e instâncias distintas entre scopes
        // (não é transient nem singleton — evita captive dependency).
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var recordMeasurement = scope1.ServiceProvider.GetRequiredService<IRecordPegamentosMeasurementHandler>();
        Assert.IsType<RecordPegamentosMeasurementHandler>(recordMeasurement);
        Assert.Same(recordMeasurement, scope1.ServiceProvider.GetRequiredService<IRecordPegamentosMeasurementHandler>());
        Assert.NotSame(recordMeasurement, scope2.ServiceProvider.GetRequiredService<IRecordPegamentosMeasurementHandler>());

        var pegamentosQuery = scope1.ServiceProvider.GetRequiredService<IPegamentosQuery>();
        Assert.IsType<PegamentosQuery>(pegamentosQuery);
        Assert.Same(pegamentosQuery, scope1.ServiceProvider.GetRequiredService<IPegamentosQuery>());
        Assert.NotSame(pegamentosQuery, scope2.ServiceProvider.GetRequiredService<IPegamentosQuery>());

        var toleranceConfiguration = scope1.ServiceProvider.GetRequiredService<IPegamentosToleranceConfiguration>();
        Assert.IsType<PegamentosToleranceConfiguration>(toleranceConfiguration);
        Assert.Same(toleranceConfiguration, scope1.ServiceProvider.GetRequiredService<IPegamentosToleranceConfiguration>());
        Assert.NotSame(toleranceConfiguration, scope2.ServiceProvider.GetRequiredService<IPegamentosToleranceConfiguration>());
    }
}
