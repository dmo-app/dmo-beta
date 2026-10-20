using System.Net;
using System.Text.Json;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Authentication;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// O ADMIN cria, repõe ou elimina um utilizador e o browser sai (fecha o
/// separador, a rede cai) enquanto a resposta do Supabase vem a caminho
/// (deep sweep, 18/10/2026). O Supabase já gravou; o DMO acaba o que falta
/// em vez de ficar a meio.
/// </summary>
public sealed class UserAdminServicePedidoCortadoTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly SupabaseFalso _supabase = new();
    private readonly SupabaseUserProvisioningProvider _provider;
    private readonly UserAdminService _service;

    public UserAdminServicePedidoCortadoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _provider = new SupabaseUserProvisioningProvider(
            new HttpClient(_supabase) { BaseAddress = new Uri("https://test-project.supabase.co/") },
            Options.Create(new SupabaseAuthOptions
            {
                Url = "https://test-project.supabase.co",
                PublishableKey = "PUB",
                SecretKey = "SECRET"
            }),
            NullLogger<SupabaseUserProvisioningProvider>.Instance);
        _service = new UserAdminService(
            _db, _provider, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Criar_OBrowserSaiDuranteOSupabase_OUtilizadorFicaCriado()
    {
        using var browser = new CancellationTokenSource();
        _supabase.Browser = browser;

        var outcome = await _service.CreateDmoUserAsync(
            new CreateDmoUser("Ana Teste", "0042", "ana@empresa.pt"), browser.Token);

        Assert.Equal(CreateDmoUserResult.Success, outcome.Result);
        var user = await _db.Users.AsNoTracking().SingleAsync();
        Assert.Equal("0042", user.OperatorId);
        Assert.Equal(_supabase.Identidades.Keys.Single(), user.ProviderUserId);
        Assert.DoesNotContain(_supabase.Pedidos, p => p.StartsWith("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReporAcesso_OBrowserSaiDuranteOSupabase_ASessaoAntigaCaiEOPinVolta()
    {
        await SeedUserAsync("0042", "id-0042", "ana@empresa.pt");
        using var browser = new CancellationTokenSource();
        _supabase.Browser = browser;

        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("0042"), browser.Token);

        Assert.Equal(ResetUserAccessResult.Success, outcome.Result);
        Assert.False(string.IsNullOrEmpty(outcome.TemporaryPassword));
        var user = await _db.Users.AsNoTracking().SingleAsync();
        Assert.Equal(1, user.SessionVersion);
        Assert.True(user.RequiresPasswordChange);
    }

    [Fact]
    public async Task Eliminar_OBrowserSaiDuranteOSupabase_OUtilizadorSaiDaLista()
    {
        await SeedUserAsync("0042", "id-0042", "ana@empresa.pt");
        using var browser = new CancellationTokenSource();
        _supabase.Browser = browser;

        var outcome = await _service.DeleteUserAsync(new DeleteUser("0042"), browser.Token);

        Assert.Equal(DeleteUserResult.Success, outcome.Result);
        Assert.Empty(_supabase.Identidades);
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    private async Task SeedUserAsync(string operatorId, string providerUserId, string email)
    {
        _supabase.Identidades[providerUserId] = email;
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = $"Operador {operatorId}",
            ProviderUserId = providerUserId,
            Email = email
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Grava como o Supabase e, antes de a resposta chegar ao DMO, o browser
    /// sai: um pedido ainda ligado ao browser perde a resposta.
    /// </summary>
    private sealed class SupabaseFalso : HttpMessageHandler
    {
        public Dictionary<string, string> Identidades { get; } = new();
        public List<string> Pedidos { get; } = new();
        public CancellationTokenSource? Browser { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            var id = request.RequestUri.Segments[^1];
            HttpResponseMessage resposta;
            if (request.Method == HttpMethod.Post)
            {
                var body = await request.Content!.ReadAsStringAsync(CancellationToken.None);
                var email = JsonDocument.Parse(body).RootElement.GetProperty("email").GetString()!;
                if (Identidades.ContainsValue(email))
                {
                    return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                    {
                        Content = new StringContent("""{"msg":"A user with this email address has already been registered"}""")
                    };
                }
                var novo = "id-" + Guid.NewGuid().ToString("N");
                Identidades[novo] = email;
                resposta = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{"id":"{{novo}}"}""") };
            }
            else if (!Identidades.ContainsKey(id))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
            }
            else
            {
                if (request.Method == HttpMethod.Delete)
                    Identidades.Remove(id);
                resposta = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            }

            Browser?.Cancel();
            await Task.Delay(50, cancellationToken);
            return resposta;
        }
    }
}
