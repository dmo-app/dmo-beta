namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Um envio com a chave do DmoTroca (o cabeçalho X-Dmo-Envio), como o browser
/// o faz quando grava sem recarregar. O mesmo envio outra vez leva a mesma
/// chave (deep sweep, 18/10/2026, B-boquilhas-2).
/// </summary>
internal static class EnvioUnicoPedidos
{
    public const string Cabecalho = "X-Dmo-Envio";

    public static string NovaChave() => Guid.NewGuid().ToString("N");

    public static Task<HttpResponseMessage> PostComChaveAsync(
        this HttpClient client, string url, string chave, params (string name, string value)[] campos)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, url) { Content = HtmlFormHelpers.BuildFormPost(campos) };
        pedido.Headers.Add(Cabecalho, chave);
        return client.SendAsync(pedido);
    }
}
