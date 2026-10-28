namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    /// <summary>
    /// O Novo Peso mostra a temperatura escrita e a densidade da água dela, e
    /// não o texto da tabela (o Diogo, 28/10/2026,
    /// cmsg_01K75FTs9bQnbhhH7qttpgra6yFCa111pk22o44zJ9bmZL); ao escrever, o
    /// peso-live.js pede o texto ao servidor.
    /// </summary>
    [Fact]
    public async Task NovoPeso_MostraATemperaturaEADensidadeDaAgua()
    {
        var (_, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4794", "provider-peso-densidade", "PesoDensidade1!");
        using var client = await LoginClientAsync("4794", "PesoDensidade1!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Peso?controlo_id={controloId}");
        Assert.DoesNotContain("Tabela de densidades", html);
        Assert.Contains("<div class=\"peso-fact\" data-peso-densidade-agua><span>Densidade da água</span><strong>—</strong></div>", html);

        Assert.Equal("{\"texto\":\"20 °C · 0.99717 g/cm³\"}",
            System.Text.RegularExpressions.Regex.Unescape(await client.GetStringAsync("/Controlo/Peso?handler=DensidadeAgua&temperatura=20,4")));
        Assert.Equal("{\"texto\":\"21 °C · 0.99696 g/cm³\"}",
            System.Text.RegularExpressions.Regex.Unescape(await client.GetStringAsync("/Controlo/Peso?handler=DensidadeAgua&temperatura=20.5")));
        Assert.Equal("{\"texto\":null}", await client.GetStringAsync("/Controlo/Peso?handler=DensidadeAgua&temperatura=50"));
        Assert.Equal("{\"texto\":null}", await client.GetStringAsync("/Controlo/Peso?handler=DensidadeAgua&temperatura=abc"));
    }
}
