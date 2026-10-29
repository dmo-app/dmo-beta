using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O travão do posto da RI (vistoria ao backend, 27/10/2026): o posto abre sem
/// sessão e o PIN pode ter 4 algarismos. A conta nunca fica bloqueada; a
/// origem confere um PIN de cada vez e, da 5.ª falha em diante, espera antes
/// de conferir.
/// </summary>
public sealed class PostoTravaoTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PinCerto = "4821";

    private readonly CustomWebApplicationFactory _factory;

    public PostoTravaoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    [Fact]
    public void Espera_SoDepoisDaQuintaFalha_ECrescendoAteTrintaSegundos()
    {
        for (var falhas = 0; falhas < TravaoDoPosto.FalhasSemEspera; falhas++)
        {
            Assert.Equal(TimeSpan.Zero, TravaoDoPosto.Espera(falhas));
        }

        Assert.Equal(TimeSpan.FromSeconds(2), TravaoDoPosto.Espera(5));
        Assert.Equal(TimeSpan.FromSeconds(4), TravaoDoPosto.Espera(6));
        Assert.Equal(TimeSpan.FromSeconds(16), TravaoDoPosto.Espera(8));
        Assert.Equal(TimeSpan.FromSeconds(30), TravaoDoPosto.Espera(9));
        Assert.Equal(TimeSpan.FromSeconds(30), TravaoDoPosto.Espera(500));
    }

    [Fact]
    public async Task MesmaOrigem_UmPinDeCadaVez_OutraOrigemNaoEspera()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var travao = new TravaoDoPosto(cache, (_, _) => Task.CompletedTask);

        var primeira = await travao.EsperarVezAsync("10.0.0.1", CancellationToken.None);
        var segunda = travao.EsperarVezAsync("10.0.0.1", CancellationToken.None);
        await Task.Delay(100);
        Assert.False(segunda.IsCompleted);

        using (var outra = await travao.EsperarVezAsync("10.0.0.2", CancellationToken.None))
        {
            // Outra origem entra logo, mesmo com a primeira ainda a conferir.
        }

        primeira.Dispose();
        using var vez = await segunda.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Posto_PinsErrados_EsperamCadaVezMais_EOPinCertoEntraNaMesma()
    {
        var esperas = new List<TimeSpan>();
        var reparador = new PostoReparador(Guid.NewGuid(), "Reparador do Posto");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(sp => new TravaoDoPosto(
                sp.GetRequiredService<IMemoryCache>(),
                (espera, _) =>
                {
                    lock (esperas)
                    {
                        esperas.Add(espera);
                    }

                    return Task.CompletedTask;
                }));
            services.AddScoped<IPostoRi>(_ => new PostoComUmReparador(reparador));
        }));
        SemearUtilizador(factory, reparador);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        for (var tentativa = 1; tentativa <= 6; tentativa++)
        {
            var resposta = await EntrarAsync(client, reparador.Id, "0000");
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        // As 5 primeiras falhas conferem logo; a 6.ª espera 2 s.
        Assert.Equal([TimeSpan.FromSeconds(2)], esperas);

        var certa = await EntrarAsync(client, reparador.Id, PinCerto);
        Assert.Equal(HttpStatusCode.Redirect, certa.StatusCode);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], esperas);
    }

    [Fact]
    public async Task Posto_PinsErrados_ComXForwardedForDiferenteDeCadaVez_EsperaNaMesma()
    {
        // AUTH-B02 (auditoria do Codex, 29/10/2026): o travão usava o IP já
        // substituído pelo X-Forwarded-For, que o cliente escolhe; cada
        // tentativa com uma origem «nova» nunca esperava. Agora o travão usa
        // o IP real da ligação (HttpContext.Items, guardado antes do
        // middleware do X-Forwarded-For correr), por isso mudar o cabeçalho
        // a cada pedido já não livra das esperas.
        var esperas = new List<TimeSpan>();
        var reparador = new PostoReparador(Guid.NewGuid(), "Reparador do Posto");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(sp => new TravaoDoPosto(
                sp.GetRequiredService<IMemoryCache>(),
                (espera, _) =>
                {
                    lock (esperas)
                    {
                        esperas.Add(espera);
                    }

                    return Task.CompletedTask;
                }));
            services.AddScoped<IPostoRi>(_ => new PostoComUmReparador(reparador));
        }));
        SemearUtilizador(factory, reparador);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        for (var tentativa = 1; tentativa <= 6; tentativa++)
        {
            var resposta = await EntrarAsync(client, reparador.Id, "0000", xForwardedFor: $"203.0.113.{tentativa}");
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        // Se o X-Forwarded-For falso contasse, as 6 tentativas eram 6 origens
        // novas e nunca esperavam. Como não conta, a 6.ª espera 2 s na mesma.
        Assert.Equal([TimeSpan.FromSeconds(2)], esperas);
    }

    private static async Task<HttpResponseMessage> EntrarAsync(HttpClient client, Guid utilizador, string pin, string? xForwardedFor = null)
    {
        var pagina = await client.GetAsync("/Posto");
        pagina.EnsureSuccessStatusCode();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await pagina.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("No antiforgery token found on page.");
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/Posto")
        {
            Content = HtmlFormHelpers.BuildFormPost(
                ("utilizador", utilizador.ToString()),
                ("pin", pin),
                ("__RequestVerificationToken", token))
        };
        if (xForwardedFor is not null)
        {
            pedido.Headers.Add("X-Forwarded-For", xForwardedFor);
        }

        return await client.SendAsync(pedido);
    }

    private static void SemearUtilizador(WebApplicationFactory<Program> factory, PostoReparador reparador)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Users.Add(new User
        {
            Id = reparador.Id,
            OperatorId = "4390",
            Name = reparador.Nome,
            ProviderUserId = "provider-posto-travao"
        });
        db.SaveChanges();
    }

    private sealed class PostoComUmReparador(PostoReparador reparador) : IPostoRi
    {
        public Task<IReadOnlyList<PostoReparador>> ListarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PostoReparador>>([reparador]);

        public Task<bool> TemPinAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == reparador.Id);

        public Task<PinGravacao> DefinirPinAsync(Guid userId, string? pin, CancellationToken cancellationToken = default) =>
            Task.FromResult(PinGravacao.Invalido);

        public Task<bool> ConfereAsync(Guid userId, string? pin, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == reparador.Id && pin == PinCerto);
    }
}
