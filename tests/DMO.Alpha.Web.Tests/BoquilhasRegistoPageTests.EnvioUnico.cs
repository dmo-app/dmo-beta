using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Deep sweep, 18/10/2026, B-boquilhas-2: a ligação caía depois de o servidor
/// gravar um movimento, o ecrã ficava com a quantidade escrita e «Guardar
/// movimento» outra vez gravava o mesmo movimento duas vezes. O mesmo envio
/// (a mesma chave X-Dmo-Envio) grava uma vez e recebe o mesmo redirect.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    private async Task<(HttpClient Client, int TraceId, string Token)> PrepararEnvioUnicoAsync(
        string operatorId, WebApplicationFactory<Program>? factory = null)
    {
        await SeedToolAsync("BQ-ENVIO-" + operatorId, "RGT" + operatorId[^3..], "1");
        var trace = await SeedTraceAsync("BQ-ENVIO-" + operatorId);
        SeedUser(operatorId, "Envio Único", "provider-envio-" + operatorId, "EnvioPass1!");
        var client = (factory ?? _factory).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginToken = await GetVerificationTokenAsync(client, "/login");
        var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId), ("password", "EnvioPass1!"), ("returnUrl", "/"), ("__RequestVerificationToken", loginToken)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return (client, trace.Id, await GetVerificationTokenAsync(client, TraceUrl(trace.Id)));
    }

    private static Task<HttpResponseMessage> PostMovementComChaveAsync(HttpClient client, int traceId, string token, string chave, int quantity) =>
        client.PostComChaveAsync($"{PagePath}?handler=RegisterMovement", chave,
            ("trace_id", traceId.ToString()), ("movement_type", "saida"), ("quantity", quantity.ToString()),
            ("__RequestVerificationToken", token));

    [Fact]
    public async Task RegisterMovement_SameEnvioTwice_SavesOnceAndAnswersWithTheSameRedirect()
    {
        var (client, traceId, token) = await PrepararEnvioUnicoAsync("4601");
        using var _ = client;
        var chave = EnvioUnicoPedidos.NovaChave();

        var primeiro = await PostMovementComChaveAsync(client, traceId, token, chave, 54);
        // A ligação caiu: o operador carrega outra vez em «Guardar movimento».
        var repetido = await PostMovementComChaveAsync(client, traceId, token, chave, 54);

        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, repetido.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        var movimentos = await ReadMovementsAsync(traceId);
        Assert.Single(movimentos);
        Assert.Equal(54, movimentos[0].Quantity);
    }

    [Fact]
    public async Task RegisterMovement_OtherEnvio_IsANewMovement()
    {
        // Dois movimentos iguais de propósito (duas guias) são dois envios.
        var (client, traceId, token) = await PrepararEnvioUnicoAsync("4602");
        using var _ = client;

        await PostMovementComChaveAsync(client, traceId, token, EnvioUnicoPedidos.NovaChave(), 54);
        await PostMovementComChaveAsync(client, traceId, token, EnvioUnicoPedidos.NovaChave(), 54);

        Assert.Equal(2, (await ReadMovementsAsync(traceId)).Count);
    }

    [Fact]
    public async Task RegisterMovement_SameEnvioAtTheSameTime_TheSecondWaitsAndSavesNothing()
    {
        // O segundo chega enquanto o primeiro ainda grava (o servidor lento).
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IRegisterBqMovementHandler>(provider =>
                new GravaDevagar(ActivatorUtilities.CreateInstance<RegisterBqMovementHandler>(provider)))));
        var (client, traceId, token) = await PrepararEnvioUnicoAsync("4603", factory);
        using var _ = client;
        var chave = EnvioUnicoPedidos.NovaChave();

        var respostas = await Task.WhenAll(
            PostMovementComChaveAsync(client, traceId, token, chave, 28),
            PostMovementComChaveAsync(client, traceId, token, chave, 28));

        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
        Assert.Equal(respostas[0].Headers.Location, respostas[1].Headers.Location);
        Assert.Single(await ReadMovementsAsync(traceId));
    }

    private sealed class GravaDevagar(IRegisterBqMovementHandler inner) : IRegisterBqMovementHandler
    {
        public async Task<RegisterBqMovementOutcome> HandleAsync(RegisterBqMovement command, CancellationToken cancellationToken = default)
        {
            await Task.Delay(400, cancellationToken);
            return await inner.HandleAsync(command, cancellationToken);
        }

        public async Task<RegisterBqMovementOutcome> HandleForContextAsync(RegisterBqMovementForContext command, CancellationToken cancellationToken = default)
        {
            await Task.Delay(400, cancellationToken);
            return await inner.HandleForContextAsync(command, cancellationToken);
        }
    }
}
