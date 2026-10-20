using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PuncoesPageTests
{
    [Fact]
    public async Task Add_SameEnvioAfterTheRepeatWindow_SavesOnceWithoutAFalseAlteracao()
    {
        // Deep sweep, 18/10/2026, B-pegamentos-3: a ligação caía depois de o
        // servidor gravar a punção «20», o número ficava no campo e um Enter
        // 18 s depois (já fora dos 10 s da proteção do duplo clique) gravava
        // outra «20», mostrada como «Alteração» falsa. O mesmo envio (a mesma
        // chave X-Dmo-Envio) grava uma vez, a qualquer altura.
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4631", "Punções Envio", "provider-puncoes-envio", "PuEnvio1!");
        using var client = await LoginClientAsync("4631", "PuEnvio1!");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"))!;
        var chave = EnvioUnicoPedidos.NovaChave();
        (string, string)[] campos =
        [
            ("controlo_id", controloId.ToString()), ("numero", "20"), ("referencia", "1075"), ("lote", "1"),
            ("__RequestVerificationToken", token)
        ];

        var primeiro = await client.PostComChaveAsync($"{PagePath}?handler=Add", chave, campos);
        // 18 s depois: a proteção do duplo clique (10 s) já passou.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var gravada = await db.PuncaoInspecoes.SingleAsync();
            gravada.RegisteredAtUtc = DateTime.UtcNow.AddSeconds(-18);
            await db.SaveChangesAsync();
        }
        var repetido = await client.PostComChaveAsync($"{PagePath}?handler=Add", chave, campos);

        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        using var leitura = _factory.Services.CreateScope();
        var numeros = await leitura.ServiceProvider.GetRequiredService<DmoDbContext>().PuncaoInspecoes.AsNoTracking()
            .Select(i => i.Number).ToListAsync();
        Assert.Equal(new[] { "20" }, numeros);
    }
}
