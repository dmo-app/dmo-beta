using System.Net;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoComparacaoPageTests
{
    [Fact]
    public async Task GuardarComparacao_SameEnvioAfterTheRepeatWindow_SavesOnceAndAnswersWithTheSameRedirect()
    {
        // Deep sweep, 18/10/2026, B-peso-4: a ligação caía depois de guardar e
        // o operador carregava outra vez em «Guardar comparação» quando ela
        // voltava. Os repetidos só abriam a primeira durante a janela dos
        // repetidos (2 min); depois, guardavam outra. O mesmo envio (a mesma
        // chave X-Dmo-Envio) guarda uma vez, a qualquer altura.
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4641", "Peso Operador", "provider-cmp-envio", "PesoCmpEnvio1!");
        using var client = await LoginClientAsync("4641", "PesoCmpEnvio1!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");
        var chave = EnvioUnicoPedidos.NovaChave();
        (string, string)[] campos =
        [
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "22"),
            ("cm_subject", "76"), ("water_weight", "131,7"),
            ("cm_subject", "33"), ("water_weight", "127,7"),
            ("comparacao_note", "teste rede"),
            ("__RequestVerificationToken", token)
        ];

        var primeiro = await client.PostComChaveAsync($"{PagePath}?handler=GuardarComparacao", chave, campos);
        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Contains("comparacao_id=", primeiro.Headers.Location!.ToString(), StringComparison.Ordinal);

        // A ligação volta passada a janela dos repetidos.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var guardada = await db.PesoComparacoes.SingleAsync();
            guardada.CreatedAtUtc -= CreatePesoComparacaoHandler.RepeatedSubmitWindow + TimeSpan.FromMinutes(1);
            await db.SaveChangesAsync();
        }

        var repetido = await client.PostComChaveAsync($"{PagePath}?handler=GuardarComparacao", chave, campos);

        Assert.Equal(HttpStatusCode.Redirect, repetido.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        using var verificar = _factory.Services.CreateScope();
        var lidas = verificar.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await lidas.PesoComparacoes.AsNoTracking().CountAsync());
        Assert.Equal(2, await lidas.PesoComparacaoMeasurements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task ComparacaoTab_CompararEGuardarTrocamSoAsZonas()
    {
        // Deep sweep, 18/10/2026, B-peso-4: «Comparar pesos» e «Guardar
        // comparação» vão por fetch (DmoTroca): o formulário está numa zona
        // data-dmo-parcial e, fora das zonas, a página desenhada pelo POST
        // (o resultado, uma recusa) e a do PRG da guardada são iguais à do
        // GET; senão o componente abria a página inteira.
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4642", "Peso Operador", "provider-cmp-zonas", "PesoCmpZonas1!");
        using var client = await LoginClientAsync("4642", "PesoCmpZonas1!");

        var inicio = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");
        var leituras = inicio.IndexOf("<div id=\"pesoComparacaoLeituras\" class=\"dmo-zona\" data-dmo-regiao data-dmo-parcial>", StringComparison.Ordinal);
        Assert.True(leituras >= 0, "a zona das leituras");
        Assert.True(inicio.IndexOf("id=\"cmpForm\"", StringComparison.Ordinal) > leituras, "o formulário dentro da zona");
        var fora = PesoZonas.ForaDasZonas(inicio);
        foreach (var id in new[] { "pesoComparacaoContexto", "pesoComparacaoResultado", "pesoComparacaoGuardada" })
        {
            Assert.Contains($"<div id=\"{id}\" class=\"dmo-zona\" data-dmo-regiao></div>", fora);
        }

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(inicio)!;
        (string, string)[] Campos(string agua) =>
        [
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", agua),
            ("comparacao_note", "nota do operador"),
            ("__RequestVerificationToken", token)
        ];
        var comparar = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(Campos("199.64")));
        Assert.Equal(HttpStatusCode.OK, comparar.StatusCode);
        var resultado = await comparar.Content.ReadAsStringAsync();
        Assert.Contains("Peso do vidro · controlo inicial / novas leituras", resultado);
        // O formulário que volta traz o que o operador escreveu, também a nota.
        Assert.Contains("value=\"199.64\"", resultado);
        Assert.Contains("value=\"nota do operador\"", resultado);
        Assert.Equal(fora, PesoZonas.ForaDasZonas(resultado));

        var recusa = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(Campos("abc")));
        Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
        Assert.Equal(fora, PesoZonas.ForaDasZonas(await recusa.Content.ReadAsStringAsync()));

        var guardar = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(Campos("199.64")));
        Assert.Equal(HttpStatusCode.Redirect, guardar.StatusCode);
        var guardada = await GetPageHtmlAsync(client, guardar.Headers.Location!.ToString());
        Assert.Contains("Comparação guardada · 1075C142 · 202601", guardada);
        Assert.Equal(fora, PesoZonas.ForaDasZonas(guardada));
    }
}
