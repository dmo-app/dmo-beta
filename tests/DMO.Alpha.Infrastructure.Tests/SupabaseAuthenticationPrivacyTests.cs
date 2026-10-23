using System.Net;
using DMO.Alpha.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Tests;

public sealed class SupabaseAuthenticationPrivacyTests
{
    private sealed class ResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            { Content = new StringContent("{\"error\":\"private-provider-body-fixture\"}") });
    }
    private sealed class CancelledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }
    private sealed class CollectedLogs : ILogger<SupabaseAuthenticationProvider>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
    private static SupabaseAuthenticationProvider Provider(HttpMessageHandler handler, CollectedLogs logger) =>
        new(new HttpClient(handler), Options.Create(new SupabaseAuthOptions { Url = "https://auth.example.invalid", PublishableKey = "sb_publishable_fixture" }), logger);

    [Fact]
    public async Task FalhaDeLogin_NaoRegistaEmailNemCorpoDoFornecedor()
    {
        var logs = new CollectedLogs();
        using var provider = Provider(new ResponseHandler(), logs);
        Assert.False((await provider.AuthenticateAsync("private-email-fixture@example.invalid", "FixturePassword9!")).Success);
        Assert.NotEmpty(logs.Messages);
        Assert.All(logs.Messages, m =>
        {
            Assert.DoesNotContain("private-provider-body-fixture", m);
            Assert.DoesNotContain("private-email-fixture", m);
        });
    }

    [Fact]
    public async Task CancelamentoDoUtilizador_NaoSeTornaErroDoFornecedor()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var provider = Provider(new CancelledHandler(), new CollectedLogs());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.AuthenticateAsync("fixture@example.invalid", "FixturePassword9!", cts.Token));
    }
}
