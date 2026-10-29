using System.Net;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// AUTH-B01 (auditoria de 29/10/2026): um PIN/palavra-passe de 4 dígitos
/// tinha 10 000 tentativas online sem travão real — a 5.ª falha só mudava a
/// mensagem, a palavra-passe certa entrava sempre, de imediato. O Diogo
/// decidiu (29/10/2026, cartão de decisão) bloquear a sério, com a mesma
/// espera exponencial que o Posto já tinha (<see cref="TravaoDoPosto"/>),
/// que se esgota sozinha — nunca um bloqueio permanente a exigir o ADMIN.
/// </summary>
public partial class AuthenticationTests
{
    [Fact]
    public async Task Login_PalavrasPasseErradas_EsperamCadaVezMais_EACertaEntraNaMesmaAposEsperar()
    {
        var esperas = new List<TimeSpan>();
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
        }));
        SeedAdmin("travao-login@example.test", "provider-travao-login", "SenhaCerta123!");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        for (var tentativa = 1; tentativa <= 6; tentativa++)
        {
            var resposta = await LoginAsync(client, "travao-login@example.test", "errada");
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        // As 5 primeiras falhas conferem logo; a 6.ª espera 2 s (como o Posto).
        Assert.Equal([TimeSpan.FromSeconds(2)], esperas);

        // A palavra-passe CERTA, tentada durante a janela de espera, também
        // espera — a espera acontece antes de a conferir, não depois.
        var certa = await LoginAsync(client, "travao-login@example.test", "SenhaCerta123!");
        Assert.Equal(HttpStatusCode.Redirect, certa.StatusCode);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], esperas);
    }

    [Fact]
    public async Task Login_DuasFalhasEDepoisACerta_NaoEspera()
    {
        var esperas = new List<TimeSpan>();
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
        }));
        SeedAdmin("sem-travao@example.test", "provider-sem-travao", "SenhaCerta123!");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        await LoginAsync(client, "sem-travao@example.test", "errada");
        await LoginAsync(client, "sem-travao@example.test", "errada");
        var certa = await LoginAsync(client, "sem-travao@example.test", "SenhaCerta123!");

        Assert.Equal(HttpStatusCode.Redirect, certa.StatusCode);
        Assert.Empty(esperas);
    }

    [Fact]
    public async Task Login_MesmaContaDuasOrigens_UmaPalavraPasseDeCadaVez()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var travao = new TravaoDoPosto(cache, (_, _) => Task.CompletedTask);

        var primeira = await travao.EsperarVezAsync("dmo-login-falhas:duas-origens@example.test", CancellationToken.None);
        var segunda = travao.EsperarVezAsync("dmo-login-falhas:duas-origens@example.test", CancellationToken.None);
        await Task.Delay(100);
        Assert.False(segunda.IsCompleted);

        primeira.Dispose();
        using var vez = await segunda.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
