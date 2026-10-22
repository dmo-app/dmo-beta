using System.Net;
using DMO.Alpha.Core.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Abre o cadeado das Definições gerais num cliente de teste: cria o PIN das
/// Definições (se ainda não for este) e escreve-o, como o utilizador faria.
/// </summary>
internal static class DefinicoesPin
{
    public const string Pin = "2468";

    public static async Task CriarAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var pin = scope.ServiceProvider.GetRequiredService<IPinDasDefinicoes>();
        if (!await pin.ConfereAsync(Pin))
        {
            Assert.Equal(PinGravacao.Gravado, await pin.DefinirAsync(Pin));
        }
    }

    public static async Task<HttpClient> DesbloquearAsync(IServiceProvider services, HttpClient client)
    {
        await CriarAsync(services);
        var html = await (await client.GetAsync("/Definicoes")).Content.ReadAsStringAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        using var resposta = await client.PostAsync("/Definicoes?handler=Desbloquear",
            HtmlFormHelpers.BuildFormPost(("pin", Pin), ("secao", "pessoal"), ("__RequestVerificationToken", token)));
        Assert.True(resposta.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.OK);
        if (resposta.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.EndsWith("pin=ok", resposta.Headers.Location!.OriginalString);
        }

        return client;
    }
}
