using DMO.Alpha.Core.Controlo;
using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O que os testes «CoberturaC» repetem: um cliente com cookies, a entrada
/// pelo login (o formulário que a página desenha), um operador com as
/// chaves dadas e o ADMIN do email das Definições.
/// </summary>
internal static class CoberturaCApoio
{
    public static HttpClient Cliente(WebApplicationFactory<Program> fabrica) =>
        fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static async Task<HttpResponseMessage> EntrarAsync(HttpClient cliente, string identificador, string palavraPasse)
    {
        var login = await (await cliente.GetAsync("/login")).Content.ReadAsStringAsync();
        return await cliente.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identificador),
            ("password", palavraPasse),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(login)!)));
    }

    public static string PalavraPasseDe(string operatorId) => $"CobC{operatorId}!";

    /// <summary>Cria o operador (com um template com estas chaves) sem entrar.</summary>
    public static Guid CriarOperador(CustomWebApplicationFactory fabrica, string operatorId, string nome, params string[] capacidades)
    {
        var email = $"{operatorId}@dmo.local";
        fabrica.AuthProvider.AddAccount(email, $"provider-cobc-{operatorId}", PalavraPasseDe(operatorId), email);
        using var scope = fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = new Template { TemplateKey = $"tpl-cobc-{operatorId}", Name = $"Cobertura C {operatorId}", Capabilities = capacidades };
        db.Templates.Add(template);
        var utilizador = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = nome, ProviderUserId = $"provider-cobc-{operatorId}" };
        db.Users.Add(utilizador);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = utilizador.Id, TemplateId = template.Id });
        db.SaveChanges();
        return utilizador.Id;
    }

    public static async Task<(HttpClient Cliente, Guid UserId)> OperadorAsync(
        CustomWebApplicationFactory fabrica, string operatorId, string nome, params string[] capacidades)
    {
        var id = CriarOperador(fabrica, operatorId, nome, capacidades);
        var cliente = Cliente(fabrica);
        using var entrada = await EntrarAsync(cliente, $"{operatorId}@dmo.local", PalavraPasseDe(operatorId));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        return (cliente, id);
    }

    public static async Task<HttpClient> AdminAsync(CustomWebApplicationFactory fabrica, string email, string palavraPasse)
    {
        fabrica.AuthProvider.AddAccount(email, "provider-cobc-admin-" + email, palavraPasse, email);
        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            db.SaveChanges();
        }

        var cliente = Cliente(fabrica);
        using var entrada = await EntrarAsync(cliente, email, palavraPasse);
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        return cliente;
    }

    public static async Task<string> PaginaAsync(HttpClient cliente, string url)
    {
        using var resposta = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadAsStringAsync();
    }

    /// <summary>O envio do formulário POST que a página desenha: o token vem da própria página.</summary>
    public static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string paginaComToken, string url, params (string Nome, string Valor)[] campos)
    {
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(paginaComToken)
            ?? throw new InvalidOperationException("A página não tem o token do formulário.");
        return await cliente.PostAsync(url, HtmlFormHelpers.BuildFormPost(
            campos.Append(("__RequestVerificationToken", token)).ToArray()));
    }
}
