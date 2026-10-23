using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// PERF-01: a medição do pedido completo — o cabeçalho Server-Timing nas
/// páginas (não nos ficheiros estáticos), o número de consultas igual ao que
/// o EF executou, uma linha no log sem valores do pedido, e desligável.
/// </summary>
public sealed class MedicaoPedidosTests
{
    private const string Sentinela = "SENTINELA-PERF01-7f3a";
    private static readonly Regex Consultas = new("db;dur=[0-9.]+;desc=\"(\\d+) consultas\"");

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, params (string, string)[] fields)
    {
        var html = await (await client.GetAsync(path)).Content.ReadAsStringAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException("Sem token antiforgery.");
        return await client.PostAsync(path, HtmlFormHelpers.BuildFormPost(
            fields.Append(("__RequestVerificationToken", token)).ToArray()));
    }

    private static async Task<HttpClient> LoginAdminAsync(WebApplicationFactory<Program> factory, CustomWebApplicationFactory baseFactory)
    {
        const string email = "admin.perf01@dmo.test";
        baseFactory.AuthProvider.AddAccount(email, "perf01-admin", Sentinela, email);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            db.SaveChanges();
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await PostFormAsync(client, "/login", ("identifier", email), ("password", Sentinela), ("returnUrl", "/Admin/Users"));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    [Fact]
    public async Task PaginaAutenticada_TemServerTiming_EOsEstaticosNao_EOLogNaoLevaValores()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        var logs = new CapturingLoggerProvider();
        using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        using var client = await LoginAdminAsync(factory, baseFactory);

        using var page = await client.GetAsync("/Admin/Users?procura=" + Sentinela);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var timing = string.Join(", ", page.Headers.GetValues("Server-Timing"));
        Assert.Matches(Consultas, timing);
        Assert.Contains("app;dur=", timing);
        Assert.Contains("total;dur=", timing);
        Assert.True(page.Headers.CacheControl?.NoStore);

        using var css = await client.GetAsync("/css/dmo-account.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.False(css.Headers.Contains("Server-Timing"));

        var linhas = logs.Entries.Where(e => e.Category == "DMO.Medicao").Select(e => e.Message).ToList();
        Assert.Contains(linhas, l => l.StartsWith("Pedido GET /Admin/Users", StringComparison.Ordinal) && l.Contains("-> 200") && l.Contains(" consultas"));
        Assert.Contains(linhas, l => l.StartsWith("Pedido POST /login", StringComparison.OrdinalIgnoreCase) && l.Contains("-> 302"));
        Assert.DoesNotContain(linhas, l => l.Contains("/css/", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(Sentinela, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("admin.perf01", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Desligada_NaoPoeOCabecalho_NemALinha_EONoStoreFica()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        var logs = new CapturingLoggerProvider();
        using var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Dmo:MedicaoPedidos:Ligada", "false");
            b.ConfigureLogging(l => l.AddProvider(logs));
        });
        using var client = await LoginAdminAsync(factory, baseFactory);

        using var page = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.False(page.Headers.Contains("Server-Timing"));
        Assert.True(page.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(logs.Entries, e => e.Category == "DMO.Medicao");
    }

    /// <summary>
    /// No PostgreSQL descartável do CI (o InMemory não executa comandos): o
    /// número no cabeçalho é o dos comandos que o EF executou nesse pedido, e
    /// o valor escrito (que vai como parâmetro da consulta) não chega ao log.
    /// </summary>
    [SkippableFact]
    public async Task NoPostgres_ONumeroDeConsultasBateComOsComandos_EOParametroNaoVaiParaOLog()
    {
        var connection = Environment.GetEnvironmentVariable("DMO_POSTGRES_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(connection), "DMO_POSTGRES_CONNECTION is not configured (disposable PostgreSQL test database).");
        var cs = new Npgsql.NpgsqlConnectionStringBuilder(connection!);
        Skip.IfNot(cs.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true,
            "DMO_POSTGRES_CONNECTION must name a disposable database containing «test».");

        await using (var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>().UseNpgsql(cs.ConnectionString).Options))
            await db.Database.MigrateAsync();

        var contador = new ContadorDeComandos();
        var logs = new CapturingLoggerProvider();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:DmoDatabase", cs.ConnectionString);
            b.UseSetting("Dmo:DataProtectionKeyRingPath", Path.Combine(Path.GetTempPath(), "dmo-perf01-tests", Guid.NewGuid().ToString("N")));
            b.ConfigureLogging(l => l.AddProvider(logs));
            b.ConfigureServices(s => s.ConfigureDbContext<DmoDbContext>(o => o.AddInterceptors(contador)));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var html = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        contador.Zerar();
        using var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", Sentinela), ("password", Sentinela), ("returnUrl", "/"), ("__RequestVerificationToken", token)));

        var match = Consultas.Match(string.Join(", ", login.Headers.GetValues("Server-Timing")));
        Assert.True(match.Success);
        Assert.True(contador.Comandos > 0, "o login devia consultar a base");
        Assert.Equal(contador.Comandos, int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(logs.Entries, e => e.Category == "DMO.Medicao" && e.Message.Contains($"{contador.Comandos} consultas"));
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(Sentinela, StringComparison.Ordinal));
    }

    private sealed class ContadorDeComandos : DbCommandInterceptor
    {
        private int _comandos;
        public int Comandos => Volatile.Read(ref _comandos);
        public void Zerar() => Interlocked.Exchange(ref _comandos, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _comandos);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _comandos);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _comandos);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _comandos);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Category, string Message)> _entries = new();
        public IReadOnlyList<(string Category, string Message)> Entries => _entries.ToArray();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);
        public void Dispose() { }

        private sealed class CapturingLogger(string category, ConcurrentQueue<(string, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue((category, formatter(state, exception) + (exception is null ? "" : " " + exception)));
        }
    }
}
