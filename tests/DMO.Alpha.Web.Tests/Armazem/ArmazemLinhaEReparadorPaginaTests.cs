using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// A Linha e o reparador no Registo do Armazém: a Saída para Produção pede a
/// Linha (a única da ficha já escolhida) logo a seguir ao Destino; a Entrada
/// mostra a Linha e o reparador com que a ferramenta vem (os da última
/// Saída), já preenchidos e editáveis, e grava o que lá ficou. A ficha da CM
/// mostra as RI e os Pesos dela, com o Job On e a data.
/// </summary>
public sealed class ArmazemLinhaEReparadorPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-lr-cm";
    private const string Referencia = "8801T001";
    private const string Linha = "LR1";
    private const string Reparador = "Moldes LR";

    private readonly CustomWebApplicationFactory _factory;
    private readonly int _linhaId;
    private readonly int _jobOnId;

    public ArmazemLinhaEReparadorPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.FerramentaLembretesUtilizacao.RemoveRange(db.FerramentaLembretesUtilizacao.Where(l => l.ToolId == Cm));
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados.Where(e => e.ToolId == Cm));
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes.Where(l => l.ToolId == Cm));
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes.Where(o => o.ToolId == Cm));
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos.Where(m => m.ToolId == Cm));
        db.Pesos.RemoveRange(db.Pesos.Where(p => p.CmToolId == Cm));
        db.ReparacoesInternas.RemoveRange(db.ReparacoesInternas.Where(r => r.AssociadaToolId == Cm));
        db.CmContexts.RemoveRange(db.CmContexts.Where(c => c.ToolId == Cm));
        db.ToolMachineLinks.RemoveRange(db.ToolMachineLinks.Where(l => l.ToolId == Cm));
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == "arm-lr-bq"));
        db.SaveChanges();

        var linha = db.Machines.FirstOrDefault(m => m.Code == Linha) ?? new Machine { Code = Linha };
        if (linha.Id == 0)
        {
            db.Machines.Add(linha);
        }

        if (!db.ArmazemPosicoes.Any(p => p.Codigo == "0077"))
        {
            db.ArmazemPosicoes.Add(new ArmazemPosicao { Codigo = "0077", CriadaEmUtc = DateTime.UtcNow });
        }

        if (!db.Reparadores.Any(r => r.Nome == Reparador))
        {
            db.Reparadores.Add(new Reparador { Nome = Reparador });
        }

        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = Referencia, Lot = "4" },
            new Tool { ToolId = "arm-lr-bq", Type = ToolType.Bq, Reference = "T001", Lot = "01" });
        db.SaveChanges();
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = Cm, MachineId = linha.Id });
        // O Job On a correr na Linha, com esta CM.
        var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
        var job = new JobOn { Reference = Referencia, ProductionNumber = "LR2601", MachineId = linha.Id, InicioEm = hoje.AddDays(-2), FimEm = hoje.AddDays(5) };
        db.JobOns.Add(job);
        db.SaveChanges();
        db.CmContexts.Add(new CmContext { JobOnId = job.Id, ToolId = Cm });
        db.SaveChanges();
        _linhaId = linha.Id;
        _jobOnId = job.Id;
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Armazem{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armlr-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
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

    private static async Task<string> GravarAsync(HttpClient client, string html, string botao, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        var (acao, campos) = EnvioDoFormulario.Enviar(html, preenchidos, idDoBotao: botao);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await client.GetStringAsync(response.Headers.Location!.OriginalString);
    }

    private static string Select(string html, string id)
    {
        var m = Regex.Match(html, $"<select\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[\\s\\S]*?</select>");
        Assert.True(m.Success, $"a página não tem o select #{id}");
        return m.Value;
    }

    private static string? Escolhida(string html, string id)
    {
        var opcao = Regex.Match(Select(html, id), "<option(?<a>[^>]*)selected=\"selected\"[^>]*>(?<t>[^<]*)</option>");
        return opcao.Success && !opcao.Groups["a"].Value.Contains("value=\"\"", StringComparison.Ordinal) ? opcao.Groups["t"].Value : null;
    }

    private static string Campo(string html, string id)
    {
        var campo = Regex.Match(html, $"<input\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>");
        Assert.True(campo.Success, $"a página não tem o campo #{id}");
        return campo.Value;
    }

    private async Task EntrarNaPosicaoAsync(HttpClient client)
    {
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}&acao=entrada");
        await GravarAsync(client, pagina, "armEntRegistar",
            ("armEntPosicao", "0077"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Já estava no armazém")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Reparado")));
    }

    [Fact]
    public async Task SaidaParaProducao_PedeALinhaDepoisDoDestino_EGravaOJobOn()
    {
        using var client = await LoginOperatorAsync("7901", DmoCapabilities.ArmazemGestao);
        await EntrarNaPosicaoAsync(client);

        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}&acao=saida&destino={(int)ArmazemDestino.Producao}");

        // A Linha vem logo a seguir ao Destino, com a única da ficha escolhida.
        var destino = pagina.IndexOf("id=\"armSaiDestino\"", StringComparison.Ordinal);
        var linha = pagina.IndexOf("id=\"armSaiLinha\"", StringComparison.Ordinal);
        Assert.True(destino >= 0 && linha > destino, "a Linha vem depois do Destino");
        Assert.Equal(Linha, Escolhida(pagina, "armSaiLinha"));
        Assert.DoesNotContain("id=\"armSaiLinha\"", await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}&acao=saida&destino={(int)ArmazemDestino.Sucata}"));

        await GravarAsync(client, pagina, "armSaiRegistar");

        var saida = NaBase(db => db.ArmazemMovimentos.AsNoTracking().Where(m => m.ToolId == Cm).OrderByDescending(m => m.Id).First());
        Assert.Equal((ArmazemDestino.Producao, (int?)_linhaId, (int?)_jobOnId), (saida.Destino!.Value, saida.MachineId, saida.JobOnId));
    }

    [Fact]
    public async Task Entrada_DaReparacao_MostraOReparadorEALinhaComQueVem_EGravaOQueAPessoaMudou()
    {
        using var client = await LoginOperatorAsync("7902", DmoCapabilities.ArmazemGestao);
        await EntrarNaPosicaoAsync(client);
        var producao = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}&acao=saida&destino={(int)ArmazemDestino.Producao}");
        await GravarAsync(client, producao, "armSaiRegistar");
        // Volta da Produção (a Linha vem preenchida) e vai à Reparação.
        var volta = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        Assert.Equal(Linha, Escolhida(volta, "armEntLinha"));
        await GravarAsync(client, volta, "armEntRegistar", ("armEntPosicao", "0077"));
        var reparacao = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}&acao=saida&destino={(int)ArmazemDestino.Reparacao}");
        await GravarAsync(client, reparacao, "armSaiRegistar", ("armSaiReparador", EnvioDoFormulario.Opcao(reparacao, "armSaiReparador", Reparador)));

        var entrada = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");

        // O reparador e a Linha vêm preenchidos e nunca bloqueados.
        var campo = Campo(entrada, "armEntReparador");
        Assert.Contains($"value=\"{Reparador}\"", campo);
        Assert.DoesNotContain("readonly", campo);
        Assert.DoesNotContain("disabled", campo);
        Assert.Equal(Linha, Escolhida(entrada, "armEntLinha"));
        Assert.DoesNotContain("disabled", Select(entrada, "armEntLinha"));

        await GravarAsync(client, entrada, "armEntRegistar", ("armEntPosicao", "0077"), ("armEntReparador", "Outro reparador"));

        var gravada = NaBase(db => db.ArmazemMovimentos.AsNoTracking().Where(m => m.ToolId == Cm).OrderByDescending(m => m.Id).First());
        Assert.Equal((ArmazemMovimentoTipo.Entrada, "Outro reparador", (int?)_linhaId, (int?)_jobOnId),
            (gravada.Tipo, gravada.EntradaReparadorNome, gravada.MachineId, gravada.JobOnId));
    }

    [Fact]
    public async Task FichaDeUmaBq_NaoTemASeccaoDaCm()
    {
        using var client = await LoginOperatorAsync("7904", DmoCapabilities.Ferramentas);

        var ficha = await client.GetStringAsync("/Ferramentas/Ficha?tool_id=arm-lr-bq");

        Assert.DoesNotContain("id=\"ferrRiEPesos\"", ficha);
    }
}
