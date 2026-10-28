using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// O «Criar» do Controlo anterior com as duas opções (Diogo, 28/10/2026,
/// cmsg_01K75FTs9bQnbhhH7qttpgraWdjopABExEAtaLgq6ub4Yh: «meter uma opção
/// para associar a um Job On, ter as duas»): o Peso sem Job On, como antes,
/// ou um Job On anterior da referência, que abre o Peso dessa produção.
/// </summary>
public partial class PesoFolhaProducaoAnteriorPageTests
{
    [Fact]
    public async Task CriarControloAnterior_SemJobOnOuNumJobOnAnterior()
    {
        var (controloId, pesoId, anteriorJobOnId, maisAntigaJobOnId) = await SeedAsync();
        SeedUser("4412", "Peso Operador", "provider-folha-associar", "Folha412!");
        using var client = await LoginClientAsync("4412", "Folha412!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?comparacao_de={pesoId}");

        // Sem Job On, como antes.
        Assert.Contains("Peso só para comparação: não fica associado a nenhum Job On.", html);
        // Ou num Job On anterior da referência, o mais recente primeiro.
        var form = html[html.IndexOf("data-peso-comparacao-jobon", StringComparison.Ordinal)..];
        form = form[..form.IndexOf("</form>", StringComparison.Ordinal)];
        Assert.Contains("Ou num Job On anterior", form);
        Assert.True(form.IndexOf($"<option value=\"{anteriorJobOnId}\">202604 · B1</option>", StringComparison.Ordinal)
            < form.IndexOf($"<option value=\"{maisAntigaJobOnId}\">", StringComparison.Ordinal));

        // «Abrir» leva ao Peso dessa produção.
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        using var abrir = await client.PostAsync($"{PagePath}?handler=AbrirJobOn", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", anteriorJobOnId.ToString()),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, abrir.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var controloDaAnterior = db.Controlos.Single(c => c.JobOnId == anteriorJobOnId).Id;
        Assert.NotEqual(controloId, controloDaAnterior);
        Assert.Equal($"/Controlo/Peso?controlo_id={controloDaAnterior}", abrir.Headers.Location!.ToString());
    }
}
