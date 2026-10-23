using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class BoquilhasRegistoPageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SemJobOn_OMesmoEnvioGravaUmaVez_OutraChavePermiteOutroMovimento(bool aoMesmoTempo)
    {
        const string ferramenta = "BQ-REPLAY-SEM-JOB";
        await SeedToolAsync(ferramenta, "QS175", "1");
        SeedUser("4294", "Registo sem repetição", "provider-bq-semjob-replay", "RegistoSemJob4!");
        using var client = await LoginClientAsync("4294", "RegistoSemJob4!");
        var html = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1");
        var (acao, campos) = EnvioDoFormulario.Enviar(html,
            [("bqSemJobOnBq", ferramenta), ("bqSemJobOnQtd", "4"), ("bqSemJobOnData", "2026-10-01T08:00")],
            radios: [("movement_type", "entrada")]);
        var chave = EnvioUnicoPedidos.NovaChave();
        async Task<HttpResponseMessage> Enviar(string envio)
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, acao) { Content = EnvioDoFormulario.Corpo(campos) };
            pedido.Headers.Add(EnvioUnicoPedidos.Cabecalho, envio);
            return await client.SendAsync(pedido);
        }

        using var primeira = await Enviar(chave);
        var repeticoes = aoMesmoTempo
            ? await Task.WhenAll(Enviar(chave), Enviar(chave))
            : [await Enviar(chave)];
        try
        {
            Assert.Equal(HttpStatusCode.Redirect, primeira.StatusCode);
            Assert.All(repeticoes, r =>
            {
                Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
                Assert.Equal(primeira.Headers.Location, r.Headers.Location);
            });
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Single(await db.BqMovements.AsNoTracking().Where(m => m.Trace.ToolId == ferramenta).ToListAsync());

            using var outra = await Enviar(EnvioUnicoPedidos.NovaChave());
            Assert.Equal(HttpStatusCode.Redirect, outra.StatusCode);
            var movimentos = await db.BqMovements.AsNoTracking().Where(m => m.Trace.ToolId == ferramenta).ToListAsync();
            Assert.Equal(2, movimentos.Count);
            Assert.All(movimentos, m => Assert.Equal(4, m.Quantity));
        }
        finally
        {
            foreach (var resposta in repeticoes) resposta.Dispose();
        }
    }
}
