using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Ferramentas;

public sealed class ReparacaoProgramadaEnvioUnicoTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CriarLista_OMesmoEnvioCriaUmaSo_OutraChaveCriaOutraLista(bool aoMesmoTempo)
    {
        factory.ResetState();
        factory.AuthProvider.AddAccount("7491@dmo.local", "provider-programada-replay", "ProgramadaReplay1!", "7491@dmo.local");
        int reparadorId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var reparador = new Reparador { Nome = "Reparador da repetição" };
            var template = new Template { TemplateKey = "tpl-programada-replay", Name = "Programada", Capabilities = [DmoCapabilities.Ferramentas] };
            var user = new User { Id = Guid.NewGuid(), OperatorId = "7491", Name = "Programada", ProviderUserId = "provider-programada-replay" };
            db.Reparadores.Add(reparador);
            db.Templates.Add(template);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.TemplateAssignments.Add(new TemplateAssignment { TemplateId = template.Id, UserId = user.Id });
            await db.SaveChangesAsync();
            reparadorId = reparador.Id;
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await client.GetStringAsync("/login");
        using var entrada = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "7491@dmo.local"), ("password", "ProgramadaReplay1!"), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(login)!)));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);

        var html = await client.GetStringAsync("/Ferramentas/ReparacaoProgramada?lista=0");
        var (acao, campos) = EnvioDoFormulario.Enviar(html, [("rpReparador", reparadorId.ToString())], idDoBotao: "rpCriarBotao");
        async Task<HttpResponseMessage> Enviar(string chave)
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, acao) { Content = EnvioDoFormulario.Corpo(campos) };
            pedido.Headers.Add(EnvioUnicoPedidos.Cabecalho, chave);
            return await client.SendAsync(pedido);
        }

        var envio = EnvioUnicoPedidos.NovaChave();
        var respostas = aoMesmoTempo
            ? await Task.WhenAll(Enviar(envio), Enviar(envio))
            : [await Enviar(envio), await Enviar(envio)];
        try
        {
            Assert.All(respostas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Single(await db.ReparacaoProgramadaListas.AsNoTracking().Where(l => l.ReparadorId == reparadorId).ToListAsync());
            Assert.Equal(respostas[0].Headers.Location, respostas[1].Headers.Location);
            using var outra = await Enviar(EnvioUnicoPedidos.NovaChave());
            Assert.Equal(HttpStatusCode.Redirect, outra.StatusCode);
            Assert.Equal(2, await db.ReparacaoProgramadaListas.AsNoTracking().CountAsync(l => l.ReparadorId == reparadorId));
        }
        finally
        {
            foreach (var resposta in respostas) resposta.Dispose();
        }
    }
}
