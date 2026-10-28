using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

#pragma warning disable EF1001 // QueryCompiler é API interna do EF; só conta as consultas.

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Quantas consultas faz um GET do Registo das Boquilhas (beta.68). Conta as
/// consultas LINQ executadas pelo EF (uma consulta = um comando SQL no
/// PostgreSQL) durante o pedido, sessão incluída, com várias máquinas,
/// produções anteriores, registos e movimentos. Os limites são os números
/// medidos depois da redução: subir um deles é uma regressão de desempenho.
/// </summary>
public sealed class BoquilhasRegistoConsultasTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _baseFactory;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public BoquilhasRegistoConsultasTests(CustomWebApplicationFactory baseFactory, ITestOutputHelper output)
    {
        _baseFactory = baseFactory;
        _baseFactory.ResetState();
        _output = output;

        // Uma base em memória própria, com o contador de consultas.
        var databaseName = "BoquilhasConsultas-" + Guid.NewGuid().ToString("N");
        _factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped(_ => new DbContextOptionsBuilder<DmoDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ReplaceService<IQueryCompiler, CountingQueryCompiler>()
                .Options)));
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Registo_GetFazPoucasConsultas()
    {
        var seed = await SeedAsync();
        using var client = await LoginClientAsync();

        var counts = new Dictionary<string, int>
        {
            ["registo"] = await CountGetAsync(client, "/Boquilhas"),
            ["bq_id com registo"] = await CountGetAsync(client, $"/Boquilhas?bq_id={seed.ContextWithTrace}"),
            ["bq_id sem registo"] = await CountGetAsync(client, $"/Boquilhas?bq_id={seed.ContextWithoutTrace}"),
            ["trace_id associado"] = await CountGetAsync(client, $"/Boquilhas?trace_id={seed.AssociatedTrace}"),
            ["trace_id sem Job On"] = await CountGetAsync(client, $"/Boquilhas?trace_id={seed.UnresolvedTrace}"),
            ["historico"] = await CountGetAsync(client, "/Boquilhas?historico=1"),
            ["historico grelha"] = await CountGetAsync(client, "/Boquilhas?historico=1&vista=matriz")
        };
        foreach (var (url, count) in counts)
        {
            _output.WriteLine($"{url}: {count}");
        }

        // Antes (beta.65): 8, 20, 18, 17, 13, 8 e 10. Inclui a consulta da sessão.
        // beta.103: +1 nas duas aberturas por bq_id, a consulta única do aviso
        // «O Job On mudou depois deste registo».
        // beta.187: +1 nas aberturas com registo. As contas leem só os números
        // de todos os movimentos e uma segunda consulta lê as linhas dos 25
        // da página, em vez de todos os movimentos por inteiro.
        // beta.81: +1 nas aberturas com bq_id, a consulta única dos lotes do
        // «Alterar lote» (Diogo, 26/10/2026).
        // 28/10/2026: +1 nas aberturas com registo, os movimentos da mesma BQ
        // nas outras produções (o Saldo é da BQ, DMO-574); +2 na grelha do
        // histórico, o Saldo refeito sobre os registos das BQ da lista.
        var limits = new Dictionary<string, int>
        {
            ["registo"] = 4,
            ["bq_id com registo"] = 13,
            ["bq_id sem registo"] = 11,
            ["trace_id associado"] = 10,
            ["trace_id sem Job On"] = 9,
            ["historico"] = 5,
            ["historico grelha"] = 8
        };
        // O contador está ligado: cada GET lê pelo menos a sessão e o painel.
        Assert.All(counts, count => Assert.True(count.Value >= 4, $"{count.Key}: {count.Value}"));
        Assert.All(limits, limit => Assert.True(
            counts[limit.Key] <= limit.Value,
            $"{limit.Key}: {counts[limit.Key]} consultas (limite {limit.Value})."));
    }

    private async Task<int> CountGetAsync(HttpClient client, string url)
    {
        CountingQueryCompiler.Reset();
        var response = await client.GetAsync(url);
        var count = CountingQueryCompiler.Count;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return count;
    }

    private sealed record Seed(int ContextWithTrace, int ContextWithoutTrace, int AssociatedTrace, int UnresolvedTrace);

    /// <summary>
    /// Seis máquinas com a produção corrente (datada a cobrir hoje), duas
    /// produções anteriores da mesma boquilha em cada máquina, máquinas
    /// compatíveis, registos com movimentos e um registo sem Job On.
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var today = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var codes = new[] { "B1", "B2", "B3", "C1", "C2", "C3" };
        var machines = codes.Select(code => new Machine { Code = code }).ToList();
        db.Machines.AddRange(machines);

        var currentContexts = new List<BqContext>();
        for (var i = 0; i < machines.Count; i++)
        {
            var toolId = $"BQ-CONS-{i}";
            db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = $"T{100 + i}", Lot = "1", BqAccountedQuantity = 120 });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = toolId, Machine = machines[i] });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = toolId, Machine = machines[(i + 1) % machines.Count] });

            for (var previous = 2; previous >= 1; previous--)
            {
                var old = new JobOn
                {
                    Reference = $"R{i}",
                    ProductionNumber = $"P{i}-{previous}",
                    Machine = machines[i],
                    InicioEm = today.AddDays(-30 * previous),
                    FimEm = today.AddDays(-30 * previous + 20)
                };
                db.BqContexts.Add(new BqContext { JobOn = old, ToolId = toolId });
            }

            var current = new JobOn
            {
                Reference = $"R{i}",
                ProductionNumber = $"P{i}-0",
                Machine = machines[i],
                InicioEm = today.AddDays(-3),
                FimEm = today.AddDays(10)
            };
            var context = new BqContext { JobOn = current, ToolId = toolId };
            db.BqContexts.Add(context);
            currentContexts.Add(context);
        }

        db.Tools.Add(new Tool { ToolId = "BQ-CONS-AVULSO", Type = ToolType.Bq, Reference = "T900", Lot = "2", BqAccountedQuantity = 80 });
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ-CONS-AVULSO", Machine = machines[0] });
        await db.SaveChangesAsync();

        // Registos nas cinco primeiras máquinas; a sexta fica sem registo.
        var traces = currentContexts.Take(5)
            .Select(context => new BqRepairTrace { BqContextId = context.Id })
            .ToList();
        var unresolved = new BqRepairTrace { ToolId = "BQ-CONS-AVULSO" };
        db.BqRepairTraces.AddRange(traces);
        db.BqRepairTraces.Add(unresolved);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        foreach (var trace in traces.Append(unresolved))
        {
            for (var k = 0; k < 6; k++)
            {
                db.BqMovements.Add(new BqMovement
                {
                    BqRepairTraceId = trace.Id,
                    Type = k % 2 == 0 ? BqMovementType.Saida : BqMovementType.Entrada,
                    Quantity = 3,
                    OccurredAtUtc = now.AddDays(-k).AddMinutes(-trace.Id),
                    CreatedAtUtc = now,
                    RegisteredByOperatorId = "4700",
                    BqRepairerNameFrozen = "Reparador",
                    MachineCodeFrozen = trace.BqContextId is null ? null : codes[traces.IndexOf(trace)]
                });
            }
        }

        await db.SaveChangesAsync();
        return new Seed(currentContexts[0].Id, currentContexts[5].Id, traces[1].Id, unresolved.Id);
    }

    private async Task<HttpClient> LoginClientAsync()
    {
        const string operatorId = "4700";
        const string password = "ConsultasPass1!";
        _baseFactory.AuthProvider.AddAccount($"{operatorId}@dmo.local", "provider-bq-consultas", password, $"{operatorId}@dmo.local");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Consultas", ProviderUserId = "provider-bq-consultas" };
            var template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-bq-consultas",
                Name = "Boquilhas consultas",
                Capabilities = new[] { DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate, DMO.Alpha.Core.Admin.DmoCapabilities.Boquilhas }
            };
            db.Users.Add(user);
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            db.TemplateAssignments.Add(new DMO.Alpha.Core.Admin.TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginPage = await client.GetAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await loginPage.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("Sem token antiforgery no /login.");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>Conta cada consulta LINQ executada (síncrona ou assíncrona).</summary>
    private sealed class CountingQueryCompiler(
        IQueryContextFactory queryContextFactory,
        ICompiledQueryCache compiledQueryCache,
        ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
        Microsoft.EntityFrameworkCore.Storage.IDatabase database,
        Microsoft.EntityFrameworkCore.Diagnostics.IDiagnosticsLogger<DbLoggerCategory.Query> logger,
        Microsoft.EntityFrameworkCore.Infrastructure.ICurrentDbContext currentContext,
        IEvaluatableExpressionFilter evaluatableExpressionFilter,
        Microsoft.EntityFrameworkCore.Metadata.IModel model)
        : QueryCompiler(queryContextFactory, compiledQueryCache, compiledQueryCacheKeyGenerator,
            database, logger, currentContext, evaluatableExpressionFilter, model)
    {
        private static int _count;

        public static int Count => Volatile.Read(ref _count);

        public static void Reset() => Interlocked.Exchange(ref _count, 0);

        public override TResult Execute<TResult>(System.Linq.Expressions.Expression query)
        {
            Interlocked.Increment(ref _count);
            return base.Execute<TResult>(query);
        }

        public override TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression query, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ExecuteAsync<TResult>(query, cancellationToken);
        }
    }
}
