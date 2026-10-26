using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DMO.Alpha.Web.Tests;

// Sweep de 19/10/2026, 5.5 (OP-2) e 5.4 (OP-1): o arranque diz no log que
// variável falta (nunca o valor) e a «Referência do erro» do ecrã fica no log.
public sealed class StartupConfigurationCheckTests
{
    private const string SupabaseUrl = "https://exemplo.supabase.co";
    private const string PublishableKey = "sb_publishable_teste-nao-mostrar";
    private const string SecretKey = "sb_secret_teste-nao-mostrar";

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IConfiguration ProducaoCompleta(params (string Key, string? Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["Dmo:AuthenticationProvider"] = "Supabase",
            ["ConnectionStrings:DmoDatabase"] = "Host=db;Database=postgres;Username=app;Password=nao-mostrar",
            ["Dmo:SupabaseUrl"] = SupabaseUrl,
            ["Dmo:SupabasePublishableKey"] = PublishableKey,
            ["Dmo:SupabaseSecretKey"] = SecretKey
        };
        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return Config(values.Select(kv => (kv.Key, kv.Value)).ToArray());
    }

    [Fact]
    public void Producao_ComTudoConfigurado_NaoDizNada()
    {
        Assert.Empty(StartupConfigurationCheck.Problems(ProducaoCompleta(), isDevelopment: false, useSupabase: true));
    }

    [Theory]
    [InlineData("Dmo:SupabaseUrl", "Dmo__SupabaseUrl")]
    [InlineData("Dmo:SupabasePublishableKey", "Dmo__SupabasePublishableKey")]
    [InlineData("Dmo:SupabaseSecretKey", "Dmo__SupabaseSecretKey")]
    [InlineData("ConnectionStrings:DmoDatabase", "ConnectionStrings__DmoDatabase")]
    public void Producao_SemUmaVariavel_DizONomeComoErro_NuncaOsValores(string key, string variable)
    {
        var problems = StartupConfigurationCheck.Problems(ProducaoCompleta((key, null)), isDevelopment: false, useSupabase: true);

        var problem = Assert.Single(problems);
        Assert.Equal(LogLevel.Error, problem.Level);
        Assert.StartsWith(variable, problem.Message);
        Assert.DoesNotContain("nao-mostrar", problem.Message);
    }

    [Fact]
    public void Producao_ComOEnderecoDoSupabaseSemSerUmUrl_ConsideraOEmFalta()
    {
        var problem = Assert.Single(StartupConfigurationCheck.Problems(
            ProducaoCompleta(("Dmo:SupabaseUrl", "exemplo.supabase.co")), isDevelopment: false, useSupabase: true));

        Assert.Equal(LogLevel.Error, problem.Level);
        Assert.StartsWith("Dmo__SupabaseUrl", problem.Message);
        Assert.DoesNotContain("exemplo.supabase.co", problem.Message);
    }

    [Fact]
    public void Producao_ComFornecedorDesconhecido_Avisa()
    {
        var problem = Assert.Single(StartupConfigurationCheck.Problems(
            ProducaoCompleta(("Dmo:AuthenticationProvider", "Supabse")), isDevelopment: false, useSupabase: true));

        Assert.Equal(LogLevel.Warning, problem.Level);
        Assert.Contains("Dmo__AuthenticationProvider=Local or Supabase", problem.Message);
    }

    [Fact]
    public void Producao_SemFornecedorDeclarado_UsaOLoginProprio_SemAviso()
    {
        // Vazio fora de Development = Local (o servidor do portátil): nada a dizer
        // além da ligação à base, que aqui está definida.
        Assert.Empty(StartupConfigurationCheck.Problems(
            ProducaoCompleta(("Dmo:AuthenticationProvider", null)), isDevelopment: false, useSupabase: false));
    }

    [Fact]
    public void Development_NoComputador_ComInMemory_NaoDizNada()
    {
        Assert.Empty(StartupConfigurationCheck.Problems(Config(), isDevelopment: true, useSupabase: false));
    }

    [Theory]
    [InlineData("RAILWAY_SERVICE_ID")]
    [InlineData("WEBSITE_SITE_NAME")]
    [InlineData("CONTAINER_APP_NAME")]
    public void Development_NumServicoAlojado_AvisaQueOsDetalhesDosErrosFicamVisiveis(string marker)
    {
        var problem = Assert.Single(StartupConfigurationCheck.Problems(
            ProducaoCompleta((marker, "servico")), isDevelopment: true, useSupabase: true));

        Assert.Equal(LogLevel.Warning, problem.Level);
        Assert.StartsWith("ASPNETCORE_ENVIRONMENT=Development", problem.Message);
    }

    [Fact]
    public void Arranque_EmProducaoSemAsChavesDoSupabase_DeixaNoLogOsNomesDasVariaveis()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = ProducaoFactory(logs);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        var errors = logs.Entries.Where(e => e.Level == LogLevel.Error).Select(e => e.Message).ToList();
        Assert.Contains(errors, m => m.StartsWith("Dmo__SupabaseUrl", StringComparison.Ordinal));
        Assert.Contains(errors, m => m.StartsWith("Dmo__SupabasePublishableKey", StringComparison.Ordinal));
        Assert.Contains(errors, m => m.StartsWith("Dmo__SupabaseSecretKey", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ErroNoPedido_AReferenciaDoEcraFicaNoLog()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = ProducaoFactory(logs).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IAuthenticationProvider, ThrowingAuthenticationProvider>()));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(login)!;

        using var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "referencia@example.invalid"), ("password", "test-only"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var reference = Regex.Match(html, "Referência do erro: <code>([^<]+)</code>").Groups[1].Value;
        Assert.NotEmpty(reference);
        var line = Assert.Single(logs.Entries, e => e.Message.Contains(reference, StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains(typeof(InvalidOperationException).FullName!, line.Message);
        Assert.Contains("POST /login", line.Message);
    }

    private static WebApplicationFactory<Program> ProducaoFactory(CapturingLoggerProvider logs) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Dmo:AuthenticationProvider", "Supabase");
            builder.UseSetting("ConnectionStrings:DmoDatabase",
                "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Timeout=1");
            builder.UseSetting("Dmo:DevConnectionProbe", "false");
            builder.UseSetting("Dmo:DataProtectionKeyRingPath",
                Path.Combine(Path.GetTempPath(), "dmo-startup-tests", Guid.NewGuid().ToString("N")));
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });

    private sealed class ThrowingAuthenticationProvider : IAuthenticationProvider
    {
        public Task<AuthenticateResult> AuthenticateAsync(string identifier, string password,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("falha de teste");

        public Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IdentityExistsAsync(string identifier,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
