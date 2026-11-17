using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Audit externo, 16/11/2026 (G4): um POST que não chega a nenhum handler
/// (a página não tem OnPost, ou o handler pedido não existe) desenhava a
/// página sem o shell preenchido e rebentava com erro 500. Agora a página
/// sem OnPost responde 405 e o handler que não existe 404; o GET continua a
/// abrir a página.
/// </summary>
public sealed class PostSemHandlerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PostSemHandler_NaoDa500()
    {
        factory.ResetState();
        factory.AuthProvider.AddAccount("7492@dmo.local", "provider-post-sem-handler", "PostSemHandler1!", "7492@dmo.local");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = "tpl-post-sem-handler", Name = "Ferramentas", Capabilities = [DmoCapabilities.Ferramentas] };
            var user = new User { Id = Guid.NewGuid(), OperatorId = "7492", Name = "Sem handler", ProviderUserId = "provider-post-sem-handler" };
            db.Templates.Add(template);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.TemplateAssignments.Add(new TemplateAssignment { TemplateId = template.Id, UserId = user.Id });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await client.GetStringAsync("/login");
        using var entrada = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "7492@dmo.local"), ("password", "PostSemHandler1!"), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(login)!)));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        using var aberta = await client.GetAsync("/Ferramentas/ReparacaoProgramada");
        Assert.Equal(HttpStatusCode.OK, aberta.StatusCode);
        var pagina = await aberta.Content.ReadAsStringAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(pagina)!;

        using var semOnPost = await client.PostAsync("/Ferramentas/Historico", HtmlFormHelpers.BuildFormPost(("__RequestVerificationToken", token)));
        using var inventado = await client.PostAsync("/Ferramentas/ReparacaoProgramada?handler=Inventado", HtmlFormHelpers.BuildFormPost(("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, semOnPost.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inventado.StatusCode);
    }
}
