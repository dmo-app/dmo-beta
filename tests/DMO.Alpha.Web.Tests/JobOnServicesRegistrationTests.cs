using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Valida a registration DI do Program.cs no host Web real para o corte de
/// criação do Job On: o handler de criação e as queries de leitura (consulta
/// dos Job Ons e máquinas persistidas) resolvem para as implementações
/// certas com o tempo de vida Scoped exigido. Mantém-se mínimo e seguro —
/// não semeia dados para não interferir com a base InMemory fixa partilhada
/// entre testes.
/// </summary>
public sealed class JobOnServicesRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public JobOnServicesRegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void JobOnServices_ResolveAsScopedImplementations()
    {
        // Dois scopes independentes provam o tempo de vida Scoped: a mesma
        // instância dentro de um scope e instâncias distintas entre scopes
        // (não é transient nem singleton — evita captive dependency).
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var handler = scope1.ServiceProvider.GetRequiredService<ICreateJobOnHandler>();
        Assert.IsType<CreateJobOnHandler>(handler);
        Assert.Same(handler, scope1.ServiceProvider.GetRequiredService<ICreateJobOnHandler>());
        Assert.NotSame(handler, scope2.ServiceProvider.GetRequiredService<ICreateJobOnHandler>());

        // O write path da EDIÇÃO (a produção re-planeada): o mesmo tempo
        // de vida Scoped do create.
        var updateHandler = scope1.ServiceProvider.GetRequiredService<IUpdateJobOnHandler>();
        Assert.IsType<UpdateJobOnHandler>(updateHandler);
        Assert.Same(updateHandler, scope1.ServiceProvider.GetRequiredService<IUpdateJobOnHandler>());
        Assert.NotSame(updateHandler, scope2.ServiceProvider.GetRequiredService<IUpdateJobOnHandler>());

        var jobOnList = scope1.ServiceProvider.GetRequiredService<IJobOnListQuery>();
        Assert.IsType<JobOnListQuery>(jobOnList);
        Assert.Same(jobOnList, scope1.ServiceProvider.GetRequiredService<IJobOnListQuery>());
        Assert.NotSame(jobOnList, scope2.ServiceProvider.GetRequiredService<IJobOnListQuery>());

        var machineList = scope1.ServiceProvider.GetRequiredService<IMachineListQuery>();
        Assert.IsType<MachineListQuery>(machineList);
        Assert.Same(machineList, scope1.ServiceProvider.GetRequiredService<IMachineListQuery>());
        Assert.NotSame(machineList, scope2.ServiceProvider.GetRequiredService<IMachineListQuery>());

        // A query canónica do contexto de produção por jobon_id (construída
        // na ronda histórica, agora LIGADA ao host pelo corte de criação do
        // Job On — a página e os módulos seguintes consomem-na).
        var productionContext = scope1.ServiceProvider.GetRequiredService<IProductionContextQuery>();
        Assert.IsType<ProductionContextQuery>(productionContext);
        Assert.Same(productionContext, scope1.ServiceProvider.GetRequiredService<IProductionContextQuery>());
        Assert.NotSame(productionContext, scope2.ServiceProvider.GetRequiredService<IProductionContextQuery>());
    }
}
