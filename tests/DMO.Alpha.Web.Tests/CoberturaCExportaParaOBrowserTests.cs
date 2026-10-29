using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Guarda o HTML das páginas que tests/visual/cobertura_c_browser.py mede no
/// Chromium (DMO_UI_EVIDENCE_DIR): o login, as Definições, a ficha do
/// utilizador, a Reparação Interna e a Consulta. Sem a variável não guarda
/// nada; o que cada página tem de ter para a medição verifica-se aqui.
/// </summary>
public sealed class CoberturaCExportaParaOBrowserTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RICOX";

    private readonly CustomWebApplicationFactory _factory;

    public CoberturaCExportaParaOBrowserTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Uma máquina com uma produção que cobre hoje, com a CM e o MF registados.</summary>
    private (string Codigo, int JobOnId, int CmId, int MfId, int MaquinaId) Maquina()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var codigo = Marca + "A";
        var referencia = "77X0T324";
        var maquina = new Machine { Code = codigo };
        db.Machines.Add(maquina);
        db.Tools.AddRange(
            new Tool { ToolId = $"{codigo}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "2" },
            new Tool { ToolId = $"{codigo}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "3" });
        var jobOn = new JobOn { Machine = maquina, Reference = referencia, ProductionNumber = "X01", InicioEm = Hoje.AddDays(-2), FimEm = Hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        db.SaveChanges();
        var cm = new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm" };
        var mf = new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf" };
        db.CmContexts.Add(cm);
        db.MfContexts.Add(mf);
        db.SaveChanges();
        return (codigo, jobOn.Id, cm.Id, mf.Id, maquina.Id);
    }

    [Fact]
    public async Task ExportaOLoginAsDefinicoesEAFichaDoUtilizador()
    {
        using var anonimo = CoberturaCApoio.Cliente(_factory);
        var login = await CoberturaCApoio.PaginaAsync(anonimo, "/login");
        Assert.Contains("id=\"login-posto\"", login);
        ExportarParaOBrowser.Gravar("cobertura-c-login.html", login);

        var (operador, _) = await CoberturaCApoio.OperadorAsync(_factory, "7991", "Ana Ecrã", DmoCapabilities.Boquilhas);
        var definicoes = await CoberturaCApoio.PaginaAsync(operador, "/Definicoes");
        Assert.Contains("data-user-profile-name", definicoes);
        ExportarParaOBrowser.Gravar("cobertura-c-definicoes.html", definicoes);

        var admin = await CoberturaCApoio.AdminAsync(_factory, "cobc-exporta-admin@example.test", "CobCAdmin7!");
        var ficha = await CoberturaCApoio.PaginaAsync(admin, "/Admin/Users/Ficha?operator_id=7991");
        Assert.Contains("id=\"dfuVoltar\"", ficha);
        ExportarParaOBrowser.Gravar("cobertura-c-ficha.html", ficha);
    }

    [Fact]
    public async Task ExportaARegistarEAConsultaDaReparacaoInterna()
    {
        var (codigo, jobOnId, cmId, mfId, maquinaId) = Maquina();
        var (cliente, userId) = await CoberturaCApoio.OperadorAsync(_factory, "7992", "Chefe Ecrã",
            DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView, DmoCapabilities.Ferramentas, DmoCapabilities.JobOnCreate);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ReparacoesInternas.AddRange(
                new Reparacao { JobOnId = jobOnId, MachineId = maquinaId, Tipo = ReparacaoTipo.Cm, Trabalho = ReparacaoTrabalho.Soldar, CmContextId = cmId, Numero = "101", RegistadoPorUserId = userId, RegistadoEmUtc = DateTime.UtcNow, DiaOperacional = Hoje },
                new Reparacao { JobOnId = jobOnId, MachineId = maquinaId, Tipo = ReparacaoTipo.Mf, Trabalho = ReparacaoTrabalho.Reparacao, MfContextId = mfId, Numero = "102", RegistadoPorUserId = userId, RegistadoEmUtc = DateTime.UtcNow, DiaOperacional = Hoje });
            db.SaveChanges();
        }

        var registar = await CoberturaCApoio.PaginaAsync(cliente, $"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        Assert.Contains("class=\"ri-ficha\"", registar);
        ExportarParaOBrowser.Gravar("cobertura-c-ri.html", registar);

        var consultar = await CoberturaCApoio.PaginaAsync(cliente, $"/ReparacaoInterna/Consultar?maquina={codigo}");
        Assert.Contains("id=\"riConsultaLista\"", consultar);
        Assert.Contains("href=\"/JobOn?jobon_id=", consultar);
        ExportarParaOBrowser.Gravar("cobertura-c-consultar.html", consultar);
    }
}
