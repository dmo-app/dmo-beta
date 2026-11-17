using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoFolhaProducaoAnteriorPageTests
{
    /// <summary>
    /// Garante: duas pessoas com a folha aberta; a segunda a gravar a produção
    /// anterior depois da primeira recebe o aviso «Outra pessoa gravou» e nada
    /// fica por cima (audit externo S4, 16/11/2026, F6; DMO-760).
    /// </summary>
    [Fact]
    public async Task ProducaoAnterior_DuasPessoasAoMesmoTempo_ASegundaEAvisada()
    {
        var (controloId, pesoId, anteriorJobOnId, maisAntigaJobOnId) = await SeedAsync();
        SeedUser("4461", "Peso Operador", "provider-folha-461", "Folha461!");
        using var client = await LoginClientAsync("4461", "Folha461!");
        var url = $"{PagePath}?controlo_id={controloId}";
        var aberta = await GetPageHtmlAsync(client, url);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(aberta)!;
        var versao = Regex.Match(aberta, "name=\"anterior_versao\" value=\"([^\"]*)\"").Groups[1].Value;
        Assert.Equal("|", versao);

        async Task<string> Guardar(int anterior)
        {
            using var resposta = await client.PostAsync($"{PagePath}?handler=GuardarProducaoAnterior", HtmlFormHelpers.BuildFormPost(
                ("peso_id", pesoId.ToString()), ("controlo_id", controloId.ToString()),
                ("anterior", anterior.ToString()), ("anterior_versao", versao),
                ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
            return Uri.UnescapeDataString(resposta.Headers.Location!.ToString());
        }

        Assert.Contains("anterior_ok=1", await Guardar(anteriorJobOnId));
        var segunda = await Guardar(maisAntigaJobOnId);
        Assert.Contains("anterior_recusa=mudou", segunda);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = db.Pesos.Single(p => p.Id == pesoId);
            var jobOnGuardado = db.Controlos.Single(c => c.Id == peso.ProducaoAnteriorControloId).JobOnId;
            Assert.Equal(anteriorJobOnId, jobOnGuardado);
        }

        var depois = await GetPageHtmlAsync(client, segunda);
        Assert.Contains("Outra pessoa gravou a produção anterior entretanto.", depois);
    }
}
