using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Ferramentas;

/// <summary>
/// A lista das Ferramentas mostra o % de uso e onde está cada uma (Diogo,
/// 26/10/2026: «deve apresentar percentagem e o estado: a reparar, reparador
/// em frente, a produzir, sucatado, ou armazém posição»).
/// </summary>
public sealed class FerramentasListaEstadoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FerramentasListaEstadoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.RemoveRange(db.Tools.Where(t => t.Reference == "4321E100"));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = "est-arm", Type = ToolType.Cm, Reference = "4321E100", Lot = "1" },
            new Tool { ToolId = "est-rep", Type = ToolType.Cm, Reference = "4321E100", Lot = "2" },
            new Tool { ToolId = "est-fab", Type = ToolType.Cm, Reference = "4321E100", Lot = "3" },
            new Tool { ToolId = "est-suc", Type = ToolType.Cm, Reference = "4321E100", Lot = "4" },
            new Tool { ToolId = "est-nada", Type = ToolType.Cm, Reference = "4321E100", Lot = "5" });
        var posicao = new ArmazemPosicao { Codigo = "7712", CriadaEmUtc = DateTime.UtcNow };
        db.ArmazemPosicoes.Add(posicao);
        db.SaveChanges();
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = "est-arm", PosicaoId = posicao.Id, DesdeUtc = DateTime.UtcNow });
        db.FerramentaLocalizacoes.AddRange(
            new FerramentaLocalizacao { ToolId = "est-rep", Localizacao = FerramentaLocalizacaoTipo.AReparar, ReparadorNome = "Moldin", DesdeUtc = DateTime.UtcNow },
            new FerramentaLocalizacao { ToolId = "est-fab", Localizacao = FerramentaLocalizacaoTipo.Fabrico, DesdeUtc = DateTime.UtcNow },
            new FerramentaLocalizacao { ToolId = "est-suc", Localizacao = FerramentaLocalizacaoTipo.Sucata, DesdeUtc = DateTime.UtcNow });
        db.FerramentaUtilizacoes.AddRange(
            new FerramentaUtilizacao { ToolId = "est-arm", Percentagem = 42m, RegistadaEmUtc = DateTime.UtcNow },
            new FerramentaUtilizacao { ToolId = "est-rep", Percentagem = 87.5m, RegistadaEmUtc = DateTime.UtcNow });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"ListaEstado{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-est-{operatorId}-{Guid.NewGuid():N}", Name = $"Estado {operatorId}", Capabilities = [DmoCapabilities.Ferramentas] };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task Lista_MostraOUsoEOndeEstaCadaFerramenta()
    {
        using var client = await LoginAsync("7431");

        var html = await client.GetStringAsync("/Ferramentas?reference=4321E100&por_pagina=10");

        Assert.Contains("<th>Uso</th>", html);
        Assert.Contains("<th>Estado</th>", html);
        Assert.Contains("<th>Linhas</th>", html);
        Assert.DoesNotContain("<th>Máquinas</th>", html);
        string Linha(string toolId)
        {
            var inicio = html.IndexOf($"data-id=\"{toolId}\"", StringComparison.Ordinal);
            Assert.True(inicio >= 0, toolId);
            return html[inicio..html.IndexOf("</tr>", inicio, StringComparison.Ordinal)];
        }
        // Números: inteiros sem casas, os outros com 2 casas e ponto.
        Assert.Contains("42 %", Linha("est-arm"));
        Assert.Contains("Armazém · 7712", Linha("est-arm"));
        Assert.Contains("87.50 %", Linha("est-rep"));
        Assert.Contains("A reparar · Moldin", Linha("est-rep"));
        Assert.Contains("A produzir", Linha("est-fab"));
        Assert.Contains("Sucatado", Linha("est-suc"));
        Assert.Contains("<td>—</td>", Linha("est-nada"));
    }
}
