using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnFolhaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardarOuDuplicar_PreservaTextoEPrecisaoDosCamposNaoAlterados(bool duplicar)
    {
        var (origemId, _) = await SeedJobOnAsync();
        var valores = new Dictionary<string, string?>
        {
            ["an_referencia"] = "0012", ["pu_referencia"] = "0007",
            ["arr_referencia"] = "0008", ["cs_referencia"] = "0009",
            ["pu_versao"] = "01.00", ["pu_desenho"] = "0012.345",
            ["arr_versao"] = "02.00", ["arr_desenho"] = "0003",
            ["cm_pata"] = "12.345", ["mf_corpo"] = "12,345",
            ["cm_utilizacao"] = "2,500", ["cab_peso"] = "365.000"
        };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var folha = new JobOnFolha { JobOnId = origemId };
            foreach (var (chave, valor) in valores) folha.DefinirValor(chave, valor);
            db.JobOnFolhas.Add(folha);
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha
            {
                JobOnId = origemId, Ordem = 0, Designacao = "Bucha Marcada",
                Valor = "0012.345", QtdMaq = "2,500"
            });
            (await db.JobOns.SingleAsync(j => j.Id == origemId)).CaloteMm = 1.234m;
            await db.SaveChangesAsync();
        }

        using var client = await EntrarAsync("4791");
        var url = duplicar ? $"/JobOn/Folha?duplicar={origemId}"
            : $"/JobOn/Folha?jobon_id={origemId}&editar=1";
        var html = await HtmlAsync(client, url);
        foreach (var (chave, valor) in valores)
            Assert.Equal(valor, ValorDoCampo(html, JobOnFolhaCampos.IdDe(chave)));
        Assert.Equal("1.234", ValorDoCampo(html, "lightCalote"));
        Assert.Equal("0012.345", ValorDoCampo(html, "jobon-cal-0-valor"));
        Assert.Equal("2,500", ValorDoCampo(html, "jobon-cal-0-qtd-maq"));

        var mudancas = new List<(string, string)> { ("jobon-notas-gerais", "Só esta nota mudou") };
        if (duplicar) mudancas.Add(("lightProduction", "202602"));
        var (acao, campos) = EnvioDoFormulario.Preencher(html, mudancas.ToArray());
        var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        var destinoId = duplicar
            ? await LerAsync(db => db.JobOns.Where(j => j.ProductionNumber == "202602").Select(j => j.Id).SingleAsync())
            : origemId;
        var gravada = await LerAsync(db => db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == destinoId));
        foreach (var (chave, valor) in valores) Assert.Equal(valor, gravada.Valor(chave));
        Assert.Equal("Só esta nota mudou", gravada.NotasGerais);
        Assert.Equal(1.234m, await LerAsync(db => db.JobOns.Where(j => j.Id == destinoId).Select(j => j.CaloteMm).SingleAsync()));
        var cal = await LerAsync(db => db.JobOnFolhaCalLinhas.AsNoTracking().SingleAsync(l => l.JobOnId == destinoId));
        Assert.Equal("0012.345", cal.Valor);
        Assert.Equal("2,500", cal.QtdMaq);
        // Duplicar não altera a folha nem a calote da origem.
        var origem = await LerAsync(db => db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == origemId));
        foreach (var (chave, valor) in valores) Assert.Equal(valor, origem.Valor(chave));
    }

    [Fact]
    public async Task Leitura_RemoveZerosDecimaisDasMedidas_MasPreservaIdentificadores()
    {
        var (id, _) = await SeedJobOnAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnFolhas.Add(new JobOnFolha
            {
                JobOnId = id, AnReferencia = "0012", PuVersao = "01.00", PuDesenho = "0012.345",
                CmPata = "365.00", MfCorpo = "36.850", PiDiametro = "2,00", CabPeso = "100.00"
            });
            await db.SaveChangesAsync();
        }
        using var client = await EntrarAsync("4792");
        var html = await HtmlAsync(client, $"/JobOn/Folha?jobon_id={id}");
        Assert.Contains("0012", html);
        Assert.Contains("01.00", html);
        Assert.Contains("0012.345", html);
        Assert.Contains("365", html);
        Assert.Contains("36.85", html);
        Assert.DoesNotContain("365.00", html);
        Assert.DoesNotContain("36.850", html);
        Assert.DoesNotContain("2,00", html);
        Assert.DoesNotContain("100.00", html);
    }
}
