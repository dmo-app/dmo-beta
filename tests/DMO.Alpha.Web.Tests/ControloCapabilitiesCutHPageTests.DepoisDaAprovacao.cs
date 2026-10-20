using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Diogo, 19/10/2026: uma leitura registada depois da aprovação mantém a
    // aprovação e a Revisão avisa (C9); um Peso novo depois de uma troca tem
    // aprovação própria e a parte volta a «Pendente» (C7).
    [Fact]
    public async Task Revisao_DepoisDaAprovacao_AvisaOuPedeAprovacaoPropria()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }

        SeedChefe("4797", "provider-revisao-depois", "RevisaoDepois1!");
        using var client = await LoginClientAsync("4797", "RevisaoDepois1!");
        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Controlo/Approve?handler=Approve", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("parte", "peso"), ("__RequestVerificationToken", token)))).StatusCode);

        var aprovado = Zona(await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}"), "revisaoPartePeso");
        Assert.Contains(">Aprovado</span>", aprovado);
        Assert.DoesNotContain("data-revisao-depois", aprovado);

        await Task.Delay(20);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = await db.Pesos.SingleAsync(p => p.ControloId == controloId);
            db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "1", WaterWeightGrams = 150m });
            await db.SaveChangesAsync();
        }

        var registado = Zona(await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}"), "revisaoPartePeso");
        Assert.Contains(">Aprovado</span>", registado);
        Assert.Contains("data-revisao-depois=\"registado\"", registado);
        Assert.Contains("Registado depois da aprovação. A aprovação mantém-se.", registado);
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&amp;tipo=peso\"", registado);
        Assert.DoesNotContain("data-revisao-depois=\"novo\"", registado);

        // A CM trocou: o Peso novo pede aprovação própria.
        await Task.Delay(20);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "MF-TOOL-9", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }

        var novo = Zona(await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}"), "revisaoPartePeso");
        Assert.Contains(">Pendente</span>", novo);
        Assert.Contains("Peso novo depois da aprovação: está por aprovar.", novo);
        Assert.DoesNotContain("data-revisao-depois=\"registado\"", novo);
        Assert.Contains(">Aprovar</button>", novo);
    }

    // Diogo, 19/10/2026 (C6): com quem se compara é escolha do operador; a
    // app só sugere. A Revisão usa a produção escolhida no Peso e, sem
    // escolha, não compara.
    [Fact]
    public async Task Revisao_ControloAnterior_EOEscolhidoNoPeso_SemEscolhaSoSugere()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        int anteriorControloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machineId = (await db.JobOns.SingleAsync(j => j.Id == jobOnId)).MachineId;
            var anterior = new JobOn { Reference = "5810", ProductionNumber = "J09", MachineId = machineId };
            db.JobOns.Add(anterior);
            await db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anterior.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            anteriorControloId = controlo.Id;
            foreach (var (controlo_, agua) in new[] { (controloId, 150m), (anteriorControloId, 140m) })
            {
                var peso = new Peso { ControloId = controlo_, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                    WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, GlassDensityGramsPerCm3 = 2.5m };
                peso.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "1", WaterWeightGrams = agua });
                db.Pesos.Add(peso);
            }

            await db.SaveChangesAsync();
        }

        SeedChefe("4798", "provider-revisao-anterior", "RevisaoAnterior1!");
        using var client = await LoginClientAsync("4798", "RevisaoAnterior1!");

        var semEscolha = Zona(await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}"), "revisaoPartePeso");
        Assert.Contains("data-revisao-anterior=\"sugestao\">Por escolher no Peso", semEscolha);
        Assert.DoesNotContain("data-revisao-anterior=\"escolhida\"", semEscolha);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = await db.Pesos.SingleAsync(p => p.ControloId == controloId);
            peso.ProducaoAnteriorControloId = anteriorControloId;
            await db.SaveChangesAsync();
        }

        var escolhida = Zona(await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}"), "revisaoPartePeso");
        Assert.Contains("data-revisao-anterior=\"escolhida\">Produção J09 · B1</small>", escolhida);
        Assert.DoesNotContain("Por escolher no Peso", escolhida);

        var consulta = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso");
        Assert.Contains("data-revisao-anterior=\"escolhida\">Produção J09 · B1</small>", consulta);
    }

    // Diogo, 19/10/2026 (D13): as gravações anteriores do OK/NOK do mesmo
    // controlo aparecem na consulta do Resumo das ferramentas.
    [Fact]
    public async Task RevisaoConsulta_MostraAsGravacoesAnterioresDoOkNok()
    {
        var (_, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var areas = scope.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Controlo.ControloAreaStateHandler>();
            Assert.True(await areas.SetAsync(controloId, "CM", "OK", null, "4801", setComment: false));
            Assert.True(await areas.SetAsync(controloId, "CM", "NOK", null, "4802", setComment: false));
        }

        SeedChefe("4799", "provider-revisao-historico", "RevisaoHistorico1!");
        using var client = await LoginClientAsync("4799", "RevisaoHistorico1!");
        var consulta = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=ferramentas");
        var antes = Zona(consulta, "data-revisao-historico=\"CM\"", "</tr>");
        Assert.Contains("antes: OK", antes);
        Assert.Contains("4801", antes);
        Assert.DoesNotContain("data-revisao-historico=\"BQ\"", consulta);
    }

    /// <summary>O HTML de uma zona: do seu id (ou marca) até ao fim; falha se não existe.</summary>
    private static string Zona(string html, string marca, string? fim = null)
    {
        var inicio = html.IndexOf(marca.Contains('=') ? marca : $"id=\"{marca}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"A zona {marca} não está na página.");
        var resto = html[inicio..];
        if (fim is null)
        {
            // A parte acaba onde começa a seguinte (revisao-parte).
            var seguinte = resto.IndexOf("class=\"revisao-parte\"", 10, StringComparison.Ordinal);
            return seguinte > 0 ? resto[..seguinte] : resto;
        }

        var ate = resto.IndexOf(fim, StringComparison.Ordinal);
        return ate > 0 ? resto[..ate] : resto;
    }
}
