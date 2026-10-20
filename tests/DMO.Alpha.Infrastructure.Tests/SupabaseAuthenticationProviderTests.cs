using System.Net;
using DMO.Alpha.Infrastructure.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Tests;

/// <summary>
/// Sweep de 19/10/2026, 5.1 (S-1): uma falha do Supabase (sem ligação,
/// tempo esgotado, limite de pedidos, erro do serviço, chaves da app
/// erradas) dizia «Credenciais inválidas.», como uma palavra-passe errada.
/// Agora só o 400/422 do Supabase conta como credenciais erradas; o resto é
/// «serviço indisponível». Contra um handler HTTP falso, sem Supabase real.
/// </summary>
public sealed class SupabaseAuthenticationProviderTests
{
    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private static SupabaseAuthenticationProvider CreateProvider(Func<HttpRequestMessage, HttpResponseMessage> respond, string url = "https://test-project.supabase.co") =>
        new(new HttpClient(new FakeHttpHandler(respond)),
            Options.Create(new SupabaseAuthOptions { Url = url, PublishableKey = "sb_publishable_test", SecretKey = "sb_secret_test" }),
            NullLogger<SupabaseAuthenticationProvider>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"code":"invalid_credentials","msg":"Invalid login credentials"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Invalid login credentials"}""")]
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"code":"validation_failed"}""")]
    public async Task PalavraPasseErrada_SaoCredenciaisErradas(HttpStatusCode status, string body)
    {
        using var provider = CreateProvider(_ => Json(status, body));

        var result = await provider.AuthenticateAsync("operador@empresa.pt", "errada");

        Assert.False(result.Success);
        Assert.False(result.ProviderUnavailable);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, """{"code":"over_request_rate_limit"}""")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"Invalid API key"}""")]
    public async Task OServicoRecusaOuFalha_EServicoIndisponivel(HttpStatusCode status, string body)
    {
        using var provider = CreateProvider(_ => Json(status, body));

        var result = await provider.AuthenticateAsync("operador@empresa.pt", "talvez-certa");

        Assert.False(result.Success);
        Assert.True(result.ProviderUnavailable);
    }

    [Fact]
    public async Task SemLigacaoOuTempoEsgotado_EServicoIndisponivel()
    {
        using var semLigacao = CreateProvider(_ => throw new HttpRequestException("Connection refused"));
        Assert.True((await semLigacao.AuthenticateAsync("operador@empresa.pt", "talvez-certa")).ProviderUnavailable);

        using var tempoEsgotado = CreateProvider(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        Assert.True((await tempoEsgotado.AuthenticateAsync("operador@empresa.pt", "talvez-certa")).ProviderUnavailable);
    }

    [Fact]
    public async Task SemConfiguracao_EServicoIndisponivel()
    {
        using var provider = CreateProvider(_ => throw new InvalidOperationException("não deve chamar o Supabase"), url: "");

        var result = await provider.AuthenticateAsync("operador@empresa.pt", "talvez-certa");

        Assert.False(result.Success);
        Assert.True(result.ProviderUnavailable);
    }

    [Fact]
    public async Task PalavraPasseCerta_Entra()
    {
        using var provider = CreateProvider(_ => Json(HttpStatusCode.OK,
            """{"access_token":"x","user":{"id":"11111111-2222-3333-4444-555555555555","email":"operador@empresa.pt","user_metadata":{}}}"""));

        var result = await provider.AuthenticateAsync("operador@empresa.pt", "certa");

        Assert.True(result.Success);
        Assert.False(result.ProviderUnavailable);
        Assert.Equal("11111111-2222-3333-4444-555555555555", result.ProviderUserId);
    }

    [Fact]
    public void OLoginEsperaNoMaximo15Segundos() =>
        Assert.Equal(TimeSpan.FromSeconds(15), SupabaseAuthenticationProvider.RequestTimeout);
}
