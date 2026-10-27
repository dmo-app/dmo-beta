using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A GOTA DE CADA LINHA (Diogo, 27/10/2026: «nas definições PU NNPB deve ter
/// secções e gota … B1 tem 4 gotas, a C3 tem 3»): cada secção tem uma
/// posição por gota e secções × gota são os punções que a linha leva.
/// </summary>
public partial class PuncoesPageTests
{
    [Fact]
    public async Task Gotas_NasDefinicoes_B1Com4EC3Com3PorOmissao_GravamSeERecusamForaDe1a4()
    {
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DmoDbContext>();
            foreach (var codigo in new[] { "B1", "C3" }.Where(c => !db.Machines.Any(m => m.Code == c)).ToArray())
            {
                db.Machines.Add(new DMO.Alpha.Core.Production.Machine { Code = codigo });
            }

            db.SaveChanges();
        }

        SeedUser("4331", "Operador Gotas", "provider-puncoes-gotas", "PuncoesGotas1!");
        using var client = await DefinicoesPin.DesbloquearAsync(_factory.Services, await LoginClientAsync("4331", "PuncoesGotas1!"));
        try
        {
            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, "/Definicoes?secao=controlo&sub=puncoes"));
            var cartao = Bloco(html, "data-dmo-puncoes-seccoes", "table");
            Assert.Equal("Linha Secções Gota Punções B1 48 (por omissão) C3 36 (por omissão)", Texto(cartao));
            Assert.Contains("aria-label=\"Gota da linha B1\" required", Bloco(cartao, "data-dmo-linha=\"B1\"", "tr"));
            Assert.Contains("value=\"3\" aria-label=\"Gota da linha C3\"", Bloco(cartao, "data-dmo-linha=\"C3\"", "tr"));

            var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
            var invalido = await client.PostAsync("/Controlo/Definicoes?handler=SavePuncoesSeccoesTodas", HtmlFormHelpers.BuildFormPost(
                ("maquina", "B1"), ("seccoes", "12"), ("gotas", "5"), ("__RequestVerificationToken", token)));
            Assert.Contains("seccoes=GotasInvalidas", invalido.Headers.Location!.ToString());

            var gravado = await client.PostAsync("/Controlo/Definicoes?handler=SavePuncoesSeccoesTodas", HtmlFormHelpers.BuildFormPost(
                ("maquina", "B1"), ("seccoes", "10"), ("gotas", "4"), ("maquina", "C3"), ("seccoes", "8"), ("gotas", "2"),
                ("__RequestVerificationToken", token)));
            Assert.Contains("seccoes=Gravado", gravado.Headers.Location!.ToString());

            using (var scope = _factory.Services.CreateScope())
            {
                var linhas = scope.ServiceProvider.GetRequiredService<PuncoesLinhaDefinicoes>();
                var b1 = await linhas.LinhaAsync("B1");
                Assert.Equal((10, 4, 40), (b1.Seccoes, b1.Gotas, b1.Puncoes));
                var c3 = await linhas.LinhaAsync("C3");
                Assert.Equal((8, 2, 16, false), (c3.Seccoes, c3.Gotas, c3.Puncoes, c3.GotasPorOmissao));
            }

            var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, "/Definicoes?secao=controlo&sub=puncoes&seccoes=GotasInvalidas"));
            Assert.Contains("Indique a gota com um número inteiro de 1 a 4. Não foi guardada.", depois);
            Assert.Equal("Linha Secções Gota Punções B1 40 C3 16", Texto(Bloco(depois, "data-dmo-puncoes-seccoes", "table")));
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey.StartsWith(PuncoesLinhaDefinicoes.Prefixo)));
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Gotas_DaLinha_DitamAsPosicoesDeCadaSeccao_NaFolhaENoPdf()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4330", "Operador Posições", "provider-puncoes-pos", "PuncoesPos1!");
        using var client = await LoginClientAsync("4330", "PuncoesPos1!");
        var lista = await SeedTabelaAprovadaAsync(controloId);

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var linhas = scope.ServiceProvider.GetRequiredService<PuncoesLinhaDefinicoes>();
                Assert.Equal(PuncoesSeccoesGravacao.Gravado, await linhas.GuardarGotasAsync("C2", "3"));
                Assert.Equal(PuncoesSeccoesGravacao.GotasInvalidas, await linhas.GuardarGotasAsync("C2", "0"));
            }

            // 12 secções × 3 gotas = 36 punções na linha: A1, B1, C1, A2…; sem a posição D.
            var pagina = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&vista=folha");
            var zona = Zona(WebUtility.HtmlDecode(pagina));
            Assert.Equal(lista[2].Numero, Celula(zona, "C1"));
            Assert.Equal(lista[3].Numero, Celula(zona, "A2"));
            Assert.Equal(lista[35].Numero, Celula(zona, "C12"));
            Assert.DoesNotContain("data-pu-posicao=\"D1\"", zona);
            Assert.Contains("12 secções com 3 gotas, posições A a C (36 punções)", zona);
            Assert.Equal("Punções verificados 51 Linha C2 36 Reserva 15", Texto(Bloco(zona, "data-pu-totais", "dl")));

            var token = HtmlFormHelpers.ExtractRequestVerificationToken(pagina)!;
            var pdf = await client.PostAsync("/Controlo/Puncoes?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
                ("controlo_id", controloId.ToString()), ("__RequestVerificationToken", token)));
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey.StartsWith(PuncoesLinhaDefinicoes.Prefixo)));
            db.SaveChanges();
        }
    }
}
