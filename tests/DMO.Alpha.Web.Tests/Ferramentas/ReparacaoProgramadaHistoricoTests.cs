using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Ferramentas;

/// <summary>
/// (Histórico e impressão, Diogo 22/10/2026) A Reparação Programada, separador das Ferramentas (Diogo, 22/10/2026):
/// só quem tem as Ferramentas entra; a procura abre vazia; associar cria a
/// lista do reparador; a Entrada no Armazém de uma ferramenta da lista vem
/// com «Reparado» e o aviso de mudar; depois da Entrada a linha fica cinza
/// com o dia, a posição e quem deu entrada; «Imprimir» abre a vista do
/// browser.
/// </summary>
public sealed class ReparacaoProgramadaHistoricoTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "rp-cm-5447-3";
    private const string Pagina = "/Ferramentas/ReparacaoProgramada";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoProgramadaHistoricoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ReparacaoProgramadaItens.RemoveRange(db.ReparacaoProgramadaItens);
        db.ReparacaoProgramadaListas.RemoveRange(db.ReparacaoProgramadaListas);
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados);
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Reparadores.RemoveRange(db.Reparadores);
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm));
        db.SaveChanges();
        db.Tools.Add(new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447RP", Lot = "3" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Codigo = "1033", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Codigo = "4152", CriadaEmUtc = DateTime.UtcNow });
        db.Reparadores.Add(new Reparador { Nome = "Moldin" });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Programada{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-rp-{operatorId}-{Guid.NewGuid():N}", Name = $"Programada {operatorId}", Capabilities = capabilities };
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

    private T NaBase<T>(Func<DmoDbContext, T> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    private async Task NoArmazemAsync(Func<IArmazemMovimentos, Task<ArmazemMovimentoSaida>> movimento)
    {
        using var scope = _factory.Services.CreateScope();
        var saida = await movimento(scope.ServiceProvider.GetRequiredService<IArmazemMovimentos>());
        Assert.True(saida.Gravou, saida.Resultado.ToString());
    }

    private int ReparadorId() => NaBase(db => db.Reparadores.Single().Id);

    [Fact]
    public async Task Imprimir_SoMostraAsPorRetirar_EOHistoricoAbreVazio_EListaAsCriadas()
    {
        await NoArmazemAsync(m => m.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "1033", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.PorReparar, null, null)));
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IReparacaoProgramada>().AssociarAsync([Cm], null, ReparadorId())).Associou);
        }

        using var chefia = await LoginAsync("7411", DmoCapabilities.Ferramentas);
        var listaId = NaBase(db => db.ReparacaoProgramadaListas.Single().Id);
        var antes = await chefia.GetStringAsync($"/Ferramentas/ReparacaoProgramada/Imprimir?lista={listaId}&sem_imprimir=1");
        Assert.Contains("5447RP", antes);
        Assert.DoesNotContain("id=\"rpImpNada\"", antes);

        await NoArmazemAsync(m => m.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Cm, ArmazemDestino.Reparacao, ReparadorId(), null, null, null, null)));
        var depois = await chefia.GetStringAsync($"/Ferramentas/ReparacaoProgramada/Imprimir?lista={listaId}&sem_imprimir=1");
        Assert.DoesNotContain("5447RP", depois);
        Assert.Contains("id=\"rpImpNada\"", depois);

        var vazio = await chefia.GetStringAsync(Pagina);
        Assert.Contains("id=\"rpHistVazio\"", vazio);
        Assert.DoesNotContain("data-dmo-historico=", vazio);

        var historico = await chefia.GetStringAsync($"{Pagina}?hist_reparador=Moldin");
        Assert.Contains($"data-dmo-historico=\"{listaId}\"", historico);
        Assert.Contains($"data-dmo-open=\"/Ferramentas/ReparacaoProgramada?lista={listaId}\"", historico);

        var outroDia = await chefia.GetStringAsync($"{Pagina}?hist_ate=2000-01-01");
        Assert.Contains("id=\"rpHistNada\"", outroDia);
    }
}
