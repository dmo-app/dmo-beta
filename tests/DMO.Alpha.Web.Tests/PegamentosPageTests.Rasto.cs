using System.Text.RegularExpressions;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Pages.Controlo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    // Sweep de 19/10/2026, 4.6: corrigir ou apagar uma leitura dos Pegamentos
    // deixa rasto (o valor antigo, o novo, quem e quando), e a consulta dos
    // Pegamentos na Revisão mostra-o. Antes escrevia por cima sem rasto.
    [Fact]
    public async Task CorrigirEApagarUmaLeitura_AConsultaDaRevisaoMostraOAntesODepoisEQuem()
    {
        SeedUser("4441", "Pegamentos Rasto", "provider-peg-rasto", "PegRasto1!");
        using var client = await LoginClientAsync("4441", "PegRasto1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00")) { }
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.30", "69.80")) { }

        int[] ids;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            ids = await db.PegamentosMeasurements.OrderBy(m => m.RowPosition).Select(m => m.Id).ToArrayAsync();
        }

        var token = await GetVerificationTokenAsync(client, url);
        using (await client.PostAsync($"{PagePath}?handler=EditMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("measurement_id", ids[1].ToString()),
            ("costura", "70,20"), ("contra_costura", "70.00"), ("__RequestVerificationToken", token)))) { }
        token = await GetVerificationTokenAsync(client, url);
        using (await client.PostAsync($"{PagePath}?handler=DeleteMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("measurement_id", ids[0].ToString()),
            ("__RequestVerificationToken", token)))) { }

        SeedApprover("4442", "provider-peg-rasto-chefe", "PegRastoChefe1!");
        using var chefe = await LoginClientAsync("4442", "PegRastoChefe1!");
        var consulta = await GetPageHtmlAsync(chefe, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos");

        Assert.Contains("Correções às leituras", consulta);
        var corrigida = Regex.Match(consulta, "<tr data-peg-rasto=\"corrigida\">([\\s\\S]*?)</tr>").Groups[1].Value;
        Assert.Contains("Pegamentos Rasto", corrigida);
        Assert.Contains("CM · N.º 2", corrigida);
        Assert.Contains($"{PegamentosVista.Fmt(70.30m)} / {PegamentosVista.Fmt(69.80m)}", corrigida);
        Assert.Contains($"{PegamentosVista.Fmt(70.20m)} / {PegamentosVista.Fmt(70.00m)}", corrigida);
        var apagada = Regex.Match(consulta, "<tr data-peg-rasto=\"apagada\">([\\s\\S]*?)</tr>").Groups[1].Value;
        Assert.Contains("CM · N.º 1", apagada);
        Assert.Contains($"{PegamentosVista.Fmt(70.10m)} / {PegamentosVista.Fmt(69.90m)}", apagada);
        Assert.Contains("Apagada", apagada);
        // A mais recente primeiro.
        Assert.True(consulta.IndexOf("data-peg-rasto=\"apagada\"", StringComparison.Ordinal)
            < consulta.IndexOf("data-peg-rasto=\"corrigida\"", StringComparison.Ordinal));
    }
}
