using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Audit externo, 16/11/2026 (DMO-755): o registo de várias linhas e a
/// janela «Registar sem Job On» eram formulários nativos, sem a chave
/// X-Dmo-Envio, e o «Pôr a zero», o «Editar» e o «Apagar» não tinham
/// [EnvioUnico]. Agora gravam pela fila da página (data-dmo-parcial) e o
/// mesmo envio outra vez (a ligação caiu e carregou-se de novo) não grava
/// duas vezes.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task VariasLinhasESemJobOn_GravamPelaFilaDaPagina()
    {
        SeedUser("4611", "Fila da página", "provider-fila-4611", "FilaPass11!");
        using var client = await LoginClientAsync("4611", "FilaPass11!");

        var registo = await GetPageHtmlAsync(client, PagePath);
        Assert.Matches("<form[^>]*id=\"bqVarias\"[^>]*data-dmo-parcial", registo);

        var ficheiros = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1");
        Assert.Contains("<div id=\"bqSemJobOn\" class=\"dmo-zona\" data-dmo-regiao data-dmo-parcial>", ficheiros);
    }

    [Fact]
    public async Task RegisterMany_MesmoEnvioDuasVezes_GravaUmaVez()
    {
        await SeedToolAsync("BQ-VAR-ENV", "RVE001", "1");
        var b1 = await SeedProductionAsync("BQ-VAR-ENV", "8011T001", "202711", "B1");
        SeedUser("4612", "Varias Envio", "provider-varias-4612", "VariasPass12!");
        using var client = await LoginClientAsync("4612", "VariasPass12!");
        var token = await GetVerificationTokenAsync(client, PagePath);
        var chave = EnvioUnicoPedidos.NovaChave();
        Task<HttpResponseMessage> Enviar(string envio) => client.PostComChaveAsync($"{PagePath}?handler=RegisterMany", envio,
            ("movement_type", "saida"), ("linha_bq_id", b1.Id.ToString()), ("linha_qtd", "7"), ("linha_qtd_an", ""),
            ("__RequestVerificationToken", token));

        using var primeiro = await Enviar(chave);
        using var repetido = await Enviar(chave);

        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var movimentos = await db.BqMovements.AsNoTracking().Where(m => m.Trace.BqContextId == b1.Id).ToListAsync();
        Assert.Equal(7, Assert.Single(movimentos).Quantity);

        // Outro clique (outra chave) é outro registo.
        using var outro = await Enviar(EnvioUnicoPedidos.NovaChave());
        Assert.Equal(2, await db.BqMovements.AsNoTracking().CountAsync(m => m.Trace.BqContextId == b1.Id));
    }

    [Fact]
    public async Task ResetDiscrepancy_MesmoEnvioDuasVezes_NaoMudaOMarco()
    {
        var (client, traceId, token) = await PrepararEnvioUnicoAsync("4613");
        using var _ = client;
        var chave = EnvioUnicoPedidos.NovaChave();
        Task<HttpResponseMessage> PorAZero() => client.PostComChaveAsync($"{PagePath}?handler=ResetDiscrepancy", chave,
            ("trace_id", traceId.ToString()), ("__RequestVerificationToken", token));

        using var primeiro = await PorAZero();
        var marco = await MarcoAsync(traceId);
        await Task.Delay(50);
        using var repetido = await PorAZero();

        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        Assert.NotNull(marco);
        Assert.Equal(marco, await MarcoAsync(traceId));
    }

    private async Task<DateTime?> MarcoAsync(int traceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return (await db.BqRepairTraces.AsNoTracking().SingleAsync(t => t.Id == traceId)).DiscrepancyResetAtUtc;
    }
}
