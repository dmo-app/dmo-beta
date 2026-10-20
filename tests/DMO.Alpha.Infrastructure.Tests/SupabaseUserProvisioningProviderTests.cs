using System.Net;
using System.Text.Json;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Tests;

/// <summary>
/// Prova determinística do <see cref="SupabaseUserProvisioningProvider"/> contra
/// um handler HTTP falso (o padrão provado do payload é o MESMO do teste ao
/// vivo existente <see cref="SupabaseAuthenticationProviderIntegrationTests"/>:
/// POST auth/v1/admin/users com apikey+Bearer SECRET, email/password,
/// email_confirm true e user_metadata.requires_password_change true). O gate
/// LIVE (projeto real) pertence à capitã — nenhum teste aqui contacta um
/// Supabase real. Vocabulário fechado: 422 duplicado → EmailExists; outro
/// 422/5xx → ProviderFailure; reset 404 → IdentityNotFound.
/// </summary>
public sealed class SupabaseUserProvisioningProviderTests
{
    /// <summary>Handler falso: captura o pedido e devolve a resposta escolhida.</summary>
    private sealed class FakeHttpHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            CapturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return respond(request, CapturedBody ?? string.Empty);
        }
    }

    private static SupabaseUserProvisioningProvider CreateProvider(
        FakeHttpHandler handler,
        string url = "https://test-project.supabase.co",
        string secretKey = "OPAQUE-TEST-SECRET")
    {
        var options = Options.Create(new SupabaseAuthOptions
        {
            Url = url,
            PublishableKey = "OPAQUE-TEST-PUBLISHABLE",
            SecretKey = secretKey
        });

        return new SupabaseUserProvisioningProvider(
            new HttpClient(handler),
            options,
            NullLogger<SupabaseUserProvisioningProvider>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    // ------------------------------------------------------------------
    // CreateAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Success_SendsTheProvenAdminPayloadWithSecretKeyAndParsesIdentity()
    {
        var handler = new FakeHttpHandler((_, _) =>
            Json(HttpStatusCode.OK, """{"id":"11111111-2222-3333-4444-555555555555","email":"novo@empresa.pt"}"""));
        using var provider = CreateProvider(handler);

        var result = await provider.CreateAsync("novo@empresa.pt", "Temporaria12!");

        Assert.Equal(UserProvisioningCreateStatus.Success, result.Status);
        Assert.Equal("11111111-2222-3333-4444-555555555555", result.ProviderUserId);

        // O pedido canónico: POST auth/v1/admin/users com apikey+Bearer
        // SECRET (nunca a publishable) — o MESMO padrão do ChangePasswordAsync
        // in-repo e do teste ao vivo existente.
        var request = handler.CapturedRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://test-project.supabase.co/auth/v1/admin/users", request.RequestUri!.ToString());
        Assert.Equal("OPAQUE-TEST-SECRET", request.Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("OPAQUE-TEST-SECRET", request.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        var root = body.RootElement;
        Assert.Equal("novo@empresa.pt", root.GetProperty("email").GetString());
        Assert.Equal("Temporaria12!", root.GetProperty("password").GetString());
        Assert.True(root.GetProperty("email_confirm").GetBoolean());

        // O bootstrap (USERS.md §3): a identidade nasce com a marca de
        // mudança obrigatória — o 1.º login força a password privada.
        var metadata = root.GetProperty("user_metadata");
        Assert.True(metadata.GetProperty("requires_password_change").GetBoolean());
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail422_IsTypedEmailExists()
    {
        var handler = new FakeHttpHandler((_, _) => Json(
            HttpStatusCode.UnprocessableEntity,
            """{"code":422,"msg":"A user with this email address has already been registered"}"""));
        using var provider = CreateProvider(handler);

        var result = await provider.CreateAsync("duplicado@empresa.pt", "Temporaria12!");

        Assert.Equal(UserProvisioningCreateStatus.EmailExists, result.Status);
        Assert.Null(result.ProviderUserId);
    }

    [Fact]
    public async Task CreateAsync_Other422OrServerError_IsTypedProviderFailure()
    {
        var other422 = new FakeHttpHandler((_, _) => Json(
            HttpStatusCode.UnprocessableEntity,
            """{"code":422,"msg":"Password should be at least 6 characters"}"""));
        using (var provider = CreateProvider(other422))
        {
            var result = await provider.CreateAsync("novo@empresa.pt", "curta");
            Assert.Equal(UserProvisioningCreateStatus.ProviderFailure, result.Status);
        }

        var serverError = new FakeHttpHandler((_, _) => Json(
            HttpStatusCode.InternalServerError,
            """{"msg":"unexpected"}"""));
        using (var provider = CreateProvider(serverError))
        {
            var result = await provider.CreateAsync("novo@empresa.pt", "Temporaria12!");
            Assert.Equal(UserProvisioningCreateStatus.ProviderFailure, result.Status);
        }
    }

    [Fact]
    public async Task CreateAsync_UnconfiguredInstallation_IsTypedProviderFailure()
    {
        var handler = new FakeHttpHandler((_, _) => Json(HttpStatusCode.OK, """{"id":"x"}"""));
        using var provider = CreateProvider(handler, url: "", secretKey: "");

        var result = await provider.CreateAsync("novo@empresa.pt", "Temporaria12!");

        // Sem instalação configurada (URL/SECRET vazios) nenhuma chamada é
        // feita: a recusa é tipada, nunca um crash.
        Assert.Equal(UserProvisioningCreateStatus.ProviderFailure, result.Status);
        Assert.Null(handler.CapturedRequest);
    }

    // ------------------------------------------------------------------
    // ResetAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResetAsync_Success_PutsNewTemporaryPasswordAndRearmsTheForcedChange()
    {
        var handler = new FakeHttpHandler((_, _) => Json(
            HttpStatusCode.OK,
            """{"id":"11111111-2222-3333-4444-555555555555"}"""));
        using var provider = CreateProvider(handler);

        var result = await provider.ResetAsync("11111111-2222-3333-4444-555555555555", "NovaTemporaria34!");

        Assert.Equal(UserProvisioningResetStatus.Success, result.Status);

        // O pedido canónico do reset: PUT auth/v1/admin/users/{id} — o
        // MESMO padrão provado pelo ChangePasswordAsync in-repo.
        var request = handler.CapturedRequest!;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(
            "https://test-project.supabase.co/auth/v1/admin/users/11111111-2222-3333-4444-555555555555",
            request.RequestUri!.ToString());
        Assert.Equal("OPAQUE-TEST-SECRET", request.Headers.GetValues("apikey").Single());

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        Assert.Equal("NovaTemporaria34!", body.RootElement.GetProperty("password").GetString());
        Assert.True(body.RootElement
            .GetProperty("user_metadata")
            .GetProperty("requires_password_change")
            .GetBoolean());
    }

    [Fact]
    public async Task CreateAndReset_FourDigitPin_GoesToSupabaseWithTheShortPasswordPrefix()
    {
        // O Supabase recusa menos de 6 caracteres; o PIN segue com prefixo.
        var handler = new FakeHttpHandler((_, _) =>
            Json(HttpStatusCode.OK, """{"id":"11111111-2222-3333-4444-555555555555"}"""));
        using var provider = CreateProvider(handler);

        await provider.CreateAsync("novo@empresa.pt", "0427");
        using (var body = JsonDocument.Parse(handler.CapturedBody!))
            Assert.Equal("Dmo#1:0427", body.RootElement.GetProperty("password").GetString());

        await provider.ResetAsync("11111111-2222-3333-4444-555555555555", "9035");
        using (var body = JsonDocument.Parse(handler.CapturedBody!))
            Assert.Equal("Dmo#1:9035", body.RootElement.GetProperty("password").GetString());
    }

    [Fact]
    public async Task ResetAsync_404_IsTypedIdentityNotFound()
    {
        var handler = new FakeHttpHandler((_, _) => Json(HttpStatusCode.NotFound, """{"msg":"User not found"}"""));
        using var provider = CreateProvider(handler);

        var result = await provider.ResetAsync("00000000-0000-0000-0000-000000000000", "NovaTemporaria34!");

        Assert.Equal(UserProvisioningResetStatus.IdentityNotFound, result.Status);
    }

    [Fact]
    public async Task ResetAsync_ServerError_IsTypedProviderFailure()
    {
        var handler = new FakeHttpHandler((_, _) => Json(
            HttpStatusCode.InternalServerError,
            """{"msg":"unexpected"}"""));
        using var provider = CreateProvider(handler);

        var result = await provider.ResetAsync("11111111-2222-3333-4444-555555555555", "NovaTemporaria34!");

        Assert.Equal(UserProvisioningResetStatus.ProviderFailure, result.Status);
    }
    [Fact]
    public async Task DeleteAsync_UsesCanonicalProviderIdAndSecretKey()
    {
        var handler = new FakeHttpHandler((_, _) => Json(HttpStatusCode.NoContent, ""));
        using var provider = CreateProvider(handler);
        var result = await provider.DeleteAsync("11111111-2222-3333-4444-555555555555");
        Assert.Equal(UserProvisioningDeleteStatus.Success, result.Status);
        var request = handler.CapturedRequest!;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://test-project.supabase.co/auth/v1/admin/users/11111111-2222-3333-4444-555555555555",
            request.RequestUri!.ToString());
        Assert.Equal("OPAQUE-TEST-SECRET", request.Headers.GetValues("apikey").Single());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, UserProvisioningDeleteStatus.IdentityNotFound)]
    [InlineData(HttpStatusCode.InternalServerError, UserProvisioningDeleteStatus.ProviderFailure)]
    public async Task DeleteAsync_ProviderResponsesAreTyped(HttpStatusCode status, UserProvisioningDeleteStatus expected)
    {
        var handler = new FakeHttpHandler((_, _) => Json(status, "{}"));
        using var provider = CreateProvider(handler);
        var result = await provider.DeleteAsync("11111111-2222-3333-4444-555555555555");
        Assert.Equal(expected, result.Status);
    }
}
