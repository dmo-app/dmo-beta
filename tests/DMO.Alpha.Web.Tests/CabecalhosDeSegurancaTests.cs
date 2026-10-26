namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Os cabeçalhos de segurança vão em todas as respostas, também nos ficheiros
/// estáticos (auditoria externa 01 do login, 26/10/2026).
/// </summary>
public sealed class CabecalhosDeSegurancaTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CabecalhosDeSegurancaTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/login")]
    [InlineData("/css/dmo-account.css")]
    public async Task Resposta_LevaOsCabecalhosDeSeguranca(string caminho)
    {
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync(caminho);

        Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("SAMEORIGIN", resposta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("same-origin", resposta.Headers.GetValues("Referrer-Policy").Single());
    }
}
