using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

// Diogo, 27/10/2026 (A-0231): no Peso corrige-se ou apaga-se uma leitura a
// qualquer momento. Clica-se na linha da tabela das leituras (sem botões na
// linha); a correção abre no lugar da leitura nova; apagar pergunta antes.
public sealed partial class ControloCapabilitiesCutHPageTests
{
    private async Task<(int PesoId, int[] Leituras)> SeedPesoComLeiturasAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = "T8C", Type = ToolType.Cm, Reference = "ST100", Lot = "8" });
        db.Tools.Add(new Tool { ToolId = "BQ-8C", Type = ToolType.Bq, Reference = "C142", Lot = "182" });
        var peso = new Peso { CmToolId = "T8C", BqToolId = "BQ-8C", Process = "NNPB", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, NominalWeightGrams = 140m,
            GlassDensityGramsPerCm3 = 2.50m, CmToolReferenceFrozen = "ST100", CmToolLotFrozen = "8" };
        db.Pesos.Add(peso);
        await db.SaveChangesAsync();
        var leituras = new[] { ("12", 199.64m), ("13", 201.10m), ("14", 198.80m) }
            .Select((l, i) => new PesoMeasurement { PesoId = peso.Id, RowPosition = i + 1, CmSubject = l.Item1, WaterWeightGrams = l.Item2 })
            .ToList();
        db.PesoMeasurements.AddRange(leituras);
        await db.SaveChangesAsync();
        return (peso.Id, leituras.Select(l => l.Id).ToArray());
    }

    [Fact]
    public async Task PesoLeitura_ALinhaEscolheSeComUmClique_EACorrecaoAbreNoLugarDaLeituraNova()
    {
        var (pesoId, leituras) = await SeedPesoComLeiturasAsync();
        SeedOperacional("4791", "provider-peso-corrigir-1", "PesoCorrigir1!");
        using var client = await LoginClientAsync("4791", "PesoCorrigir1!");

        var folha = await GetPageHtmlAsync(client, $"/Controlo/Peso?peso_id={pesoId}");
        Assert.Contains($"data-peso-leitura-id=\"{leituras[1]}\"", folha);
        Assert.Contains($"corrigir_leitura={leituras[1]}", folha);
        Assert.DoesNotContain("data-peso-correcao", folha);

        var escolhida = await GetPageHtmlAsync(client, $"/Controlo/Peso?peso_id={pesoId}&corrigir_leitura={leituras[1]}");
        Assert.Contains("aria-selected=\"true\"", escolhida);
        Assert.Contains("data-peso-correcao", escolhida);
        Assert.Contains("value=\"13\"", escolhida);
        Assert.Contains("data-peso-apagar", escolhida);
        Assert.DoesNotContain("data-peso-apagar-confirmar", escolhida);

        var pergunta = await GetPageHtmlAsync(client, $"/Controlo/Peso?peso_id={pesoId}&corrigir_leitura={leituras[1]}&apagar_leitura=1");
        Assert.Contains("Apagar a leitura da CM 13", pergunta);
        Assert.Contains("data-peso-apagar-confirmar", pergunta);
    }

    [Fact]
    public async Task PesoLeitura_CorrigirEApagar_PelaFolha_FicaGravadoComRasto()
    {
        var (pesoId, leituras) = await SeedPesoComLeiturasAsync();
        SeedOperacional("4792", "provider-peso-corrigir-2", "PesoCorrigir2!");
        using var client = await LoginClientAsync("4792", "PesoCorrigir2!");
        var url = $"/Controlo/Peso?peso_id={pesoId}&corrigir_leitura={leituras[0]}";

        var token = await GetVerificationTokenAsync(client, url);
        var corrigir = await client.PostAsync("/Controlo/Peso?handler=EditMeasurement", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()), ("measurement_id", leituras[0].ToString()),
            ("cm_subject", "21"), ("water_weight", "199,90"), ("__RequestVerificationToken", token)));
        Assert.True((int)corrigir.StatusCode is 200 or 302, corrigir.StatusCode.ToString());

        token = await GetVerificationTokenAsync(client, url);
        var apagar = await client.PostAsync("/Controlo/Peso?handler=DeleteMeasurement", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()), ("measurement_id", leituras[1].ToString()), ("__RequestVerificationToken", token)));
        Assert.True((int)apagar.StatusCode is 200 or 302, apagar.StatusCode.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var ficam = await db.PesoMeasurements.AsNoTracking().Where(m => m.PesoId == pesoId).OrderBy(m => m.RowPosition).ToListAsync();
        Assert.Equal(new[] { (1, "21", 199.90m), (2, "14", 198.80m) }, ficam.Select(m => (m.RowPosition, m.CmSubject, m.WaterWeightGrams)).ToArray());
        var rasto = await db.PesoMeasurementHistorico.AsNoTracking().Where(h => h.PesoId == pesoId).OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(new[] { false, true }, rasto.Select(h => h.Apagada).ToArray());
        Assert.All(rasto, h => Assert.False(string.IsNullOrEmpty(h.Por)));
    }

    [Fact]
    public async Task PesoLeitura_CorrecaoComMassaIlegivel_ERecusada_EOValorEscritoFica()
    {
        var (pesoId, leituras) = await SeedPesoComLeiturasAsync();
        SeedOperacional("4793", "provider-peso-corrigir-3", "PesoCorrigir3!");
        using var client = await LoginClientAsync("4793", "PesoCorrigir3!");
        var url = $"/Controlo/Peso?peso_id={pesoId}&corrigir_leitura={leituras[0]}";

        var token = await GetVerificationTokenAsync(client, url);
        var resposta = await client.PostAsync("/Controlo/Peso?handler=EditMeasurement", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()), ("measurement_id", leituras[0].ToString()),
            ("cm_subject", "12"), ("water_weight", "19x"), ("__RequestVerificationToken", token)));
        var html = await SeguirAsync(client, resposta);

        Assert.Contains("data-peso-correcao", html);
        Assert.Contains("value=\"19x\"", html);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(199.64m, (await db.PesoMeasurements.AsNoTracking().SingleAsync(m => m.Id == leituras[0])).WaterWeightGrams);
    }

    private async Task<string> SeguirAsync(HttpClient client, HttpResponseMessage resposta) =>
        resposta.Headers.Location is { } destino
            ? await GetPageHtmlAsync(client, destino.ToString())
            : await resposta.Content.ReadAsStringAsync();
}
