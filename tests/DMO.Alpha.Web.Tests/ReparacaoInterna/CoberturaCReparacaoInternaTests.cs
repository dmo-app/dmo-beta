using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests.ReparacaoInterna;

/// <summary>
/// Os buracos que a auditoria de cobertura (C) achou na Reparação Interna:
/// a lista «Trabalho» (Reparação, Soldar ou Polir) no registo, na tabela e no
/// filtro da Consulta (DMO-415) e a ordem dos filtros da Consulta e do
/// Histórico, com a dica do «Sem Job On» (DMO-428). A base em memória é
/// partilhada entre classes: cada teste usa as suas máquinas (com a marca).
/// </summary>
public sealed class CoberturaCReparacaoInternaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RICOB";

    private readonly CustomWebApplicationFactory _factory;

    public CoberturaCReparacaoInternaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var maquinas = db.Machines.Where(m => m.Code.StartsWith(Marca)).Select(m => m.Id).ToList();
        var jobOns = db.JobOns.Where(j => maquinas.Contains(j.MachineId)).Select(j => j.Id).ToList();
        var reparacoes = db.ReparacoesInternas.Where(r => maquinas.Contains(r.MachineId)).ToList();
        var ids = reparacoes.Select(r => r.Id).ToList();
        var resultados = ids.Select(id => (int?)id).ToList();
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas.Where(o => o.ResultadoTipo == "Reparacao" && resultados.Contains(o.ResultadoId)));
        db.ReparacoesInternasRasto.RemoveRange(db.ReparacoesInternasRasto.Where(r => ids.Contains(r.ReparacaoId)));
        db.ReparacoesInternas.RemoveRange(reparacoes);
        db.CmContexts.RemoveRange(db.CmContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.MfContexts.RemoveRange(db.MfContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.JobOns.RemoveRange(db.JobOns.Where(j => jobOns.Contains(j.Id)));
        db.Machines.RemoveRange(db.Machines.Where(m => maquinas.Contains(m.Id)));
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId.StartsWith(Marca)));
        db.SaveChanges();
    }

    private T NaBase<T>(Func<DmoDbContext, T> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Uma máquina desta classe com uma produção que cobre hoje, com a CM e o MF.</summary>
    private (string Codigo, int JobOnId, int CmId, int MfId) Maquina(string sufixo) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var referencia = $"77{sufixo}0T324";
        var maquina = new Machine { Code = codigo };
        db.Machines.Add(maquina);
        db.Tools.AddRange(
            new Tool { ToolId = $"{codigo}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "2" },
            new Tool { ToolId = $"{codigo}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "3" });
        var jobOn = new JobOn { Machine = maquina, Reference = referencia, ProductionNumber = $"{sufixo}01", InicioEm = Hoje.AddDays(-2), FimEm = Hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        db.SaveChanges();
        var cm = new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm" };
        var mf = new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf" };
        db.CmContexts.Add(cm);
        db.MfContexts.Add(mf);
        db.SaveChanges();
        return (codigo, jobOn.Id, cm.Id, mf.Id);
    });

    private static string Elemento(string html, string atributo)
    {
        var inicio = html.IndexOf(atributo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"a página não tem {atributo}");
        inicio = html.LastIndexOf('<', inicio);
        var tag = Regex.Match(html[(inicio + 1)..], "^[a-z][a-z0-9]*").Value;
        var nivel = 0;
        foreach (Match m in Regex.Matches(html[inicio..], $"<{tag}\\b|</{tag}>"))
        {
            nivel += m.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (nivel == 0)
            {
                return html.Substring(inicio, m.Index + m.Length);
            }
        }

        throw new InvalidOperationException($"{atributo} ficou por fechar");
    }

    /// <summary>As opções de um select, pela ordem em que o operador as lê: valor e texto.</summary>
    private static (string Valor, string Texto)[] Opcoes(string select) =>
        Regex.Matches(select, "<option value=\"([^\"]*)\"[^>]*>([^<]*)</option>")
            .Select(m => (m.Groups[1].Value, System.Net.WebUtility.HtmlDecode(m.Groups[2].Value))).ToArray();

    private static string? Escolhida(string select)
    {
        var m = Regex.Match(select, "<option value=\"([^\"]*)\" selected");
        return m.Success ? m.Groups[1].Value : null;
    }

    private async Task<(HttpResponseMessage Resposta, string Pagina)> RegistarAsync(
        HttpClient cliente, string codigo, string numero, string? trabalho)
    {
        var pagina = await cliente.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var preenchidos = trabalho is null
            ? new[] { ($"riNumero-{codigo}", numero) }
            : new[] { ($"riNumero-{codigo}", numero), ($"riTrabalho-{codigo}", trabalho) };
        var (acao, campos) = EnvioDoFormulario.Preencher(pagina, preenchidos);
        var resposta = await cliente.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        return (resposta, pagina);
    }

    private ReparacaoTrabalho? TrabalhoGravado(int jobOnId, string numero) => NaBase(db =>
        db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOnId && r.Numero == numero)
            .Select(r => (ReparacaoTrabalho?)r.Trabalho).SingleOrDefault());

    [Fact]
    public async Task OTrabalhoEscolhidoNoRegisto_FicaGravado_ESemEscolhaOuComUmValorDesconhecidoEReparacao()
    {
        var (codigo, jobOnId, _, _) = Maquina("A");
        var (cliente, _) = await CoberturaCApoio.OperadorAsync(_factory, "7901", "Reparador Trabalho", DmoCapabilities.ReparacaoInterna);

        // A lista tem as três opções pela ordem do operador e abre em «Reparação».
        var aberta = await cliente.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var lista = Elemento(aberta, $"id=\"riTrabalho-{codigo}\"");
        Assert.Equal(new[] { ("reparacao", "Reparação"), ("soldar", "Soldar"), ("polir", "Polir") }, Opcoes(lista));
        Assert.Equal("reparacao", Escolhida(lista));

        var (soldar, _) = await RegistarAsync(cliente, codigo, "31", EnvioDoFormulario.Opcao(aberta, $"riTrabalho-{codigo}", "Soldar"));
        Assert.Equal(ReparacaoTrabalho.Soldar, TrabalhoGravado(jobOnId, "31"));
        // Volta com a lista no trabalho que a pessoa estava a fazer.
        Assert.Contains("trabalho=soldar", soldar.Headers.Location!.OriginalString);
        var depoisDeSoldar = await cliente.GetStringAsync(soldar.Headers.Location!.OriginalString);
        Assert.Equal("soldar", Escolhida(Elemento(depoisDeSoldar, $"id=\"riTrabalho-{codigo}\"")));

        await RegistarAsync(cliente, codigo, "32", EnvioDoFormulario.Opcao(aberta, $"riTrabalho-{codigo}", "Polir"));
        Assert.Equal(ReparacaoTrabalho.Polir, TrabalhoGravado(jobOnId, "32"));

        await RegistarAsync(cliente, codigo, "33", null);
        Assert.Equal(ReparacaoTrabalho.Reparacao, TrabalhoGravado(jobOnId, "33"));

        await RegistarAsync(cliente, codigo, "34", "um-trabalho-que-nao-existe");
        Assert.Equal(ReparacaoTrabalho.Reparacao, TrabalhoGravado(jobOnId, "34"));

        // O registo do dia mostra o trabalho de cada uma na coluna «Trabalho».
        var registo = System.Net.WebUtility.HtmlDecode(await cliente.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm"));
        var tabela = Elemento(registo, $"id=\"riLog-{codigo}\"");
        Assert.Contains("<th>Trabalho</th>", tabela);
        var ids = NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOnId).ToDictionary(r => r.Numero, r => r.Id));
        Assert.Contains("<td>Soldar</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["31"]}\""));
        Assert.Contains("<td>Polir</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["32"]}\""));
        Assert.Contains("<td>Reparação</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["33"]}\""));
        Assert.Contains("<td>Reparação</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["34"]}\""));
    }

    [Fact]
    public async Task ANaoSerReparacao_AConsultaMostraOTrabalhoNaLinha_EFiltraPeloTipoDeTrabalho()
    {
        var (codigo, jobOnId, cmId, mfId) = Maquina("B");
        var quem = Guid.NewGuid();
        var ids = NaBase(db =>
        {
            db.Users.Add(new DMO.Alpha.Core.Identity.User { Id = quem, OperatorId = "ricob-b", Name = "Ana B", ProviderUserId = "ricob-b" });
            db.SaveChanges();
            Reparacao Rep(ReparacaoTipo tipo, string numero, ReparacaoTrabalho trabalho) => new()
            {
                JobOnId = jobOnId, MachineId = db.Machines.Single(m => m.Code == codigo).Id, Tipo = tipo, Trabalho = trabalho,
                CmContextId = tipo == ReparacaoTipo.Cm ? cmId : null, MfContextId = tipo == ReparacaoTipo.Mf ? mfId : null,
                Numero = numero, RegistadoPorUserId = quem, RegistadoEmUtc = DateTime.UtcNow, DiaOperacional = Hoje
            };
            db.ReparacoesInternas.AddRange(
                Rep(ReparacaoTipo.Cm, "101", ReparacaoTrabalho.Soldar),
                Rep(ReparacaoTipo.Mf, "102", ReparacaoTrabalho.Polir),
                Rep(ReparacaoTipo.Cm, "103", ReparacaoTrabalho.Reparacao));
            db.SaveChanges();
            return db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOnId).ToDictionary(r => r.Numero, r => r.Id);
        });
        var (cliente, _) = await CoberturaCApoio.OperadorAsync(_factory, "7902", "Chefe Trabalho", DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView);

        var todas = System.Net.WebUtility.HtmlDecode(await cliente.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}"));
        var tabela = Elemento(todas, "id=\"riConsultaLista\"");
        // A Reparação (o trabalho normal) não leva nome; Soldar e Polir vão depois do CM/MF.
        Assert.Contains("<td>CM · Soldar</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["101"]}\""));
        Assert.Contains("<td>MF · Polir</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["102"]}\""));
        Assert.Contains("<td>CM</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["103"]}\""));
        Assert.Equal(new[] { ("", "Todos"), ("reparacao", "Reparação"), ("soldar", "Soldar"), ("polir", "Polir") },
            Opcoes(Elemento(todas, "id=\"riConsultaTrabalho\"")));

        foreach (var (valor, esperada, fora) in new[]
        {
            ("soldar", "101", new[] { "102", "103" }),
            ("polir", "102", new[] { "101", "103" }),
            ("reparacao", "103", new[] { "101", "102" })
        })
        {
            var filtrada = System.Net.WebUtility.HtmlDecode(await cliente.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}&trabalho={valor}"));
            Assert.Equal(valor, Escolhida(Elemento(filtrada, "id=\"riConsultaTrabalho\"")));
            var lista = Elemento(filtrada, "id=\"riConsultaLista\"");
            Assert.Contains($"data-dmo-reparacao=\"{ids[esperada]}\"", lista);
            foreach (var outra in fora)
            {
                Assert.DoesNotContain($"data-dmo-reparacao=\"{ids[outra]}\"", lista);
            }
        }
    }

    [Fact]
    public async Task OsFiltrosDaConsulta_VemPelaOrdemPedida_ESemJobOnTemUmaDica()
    {
        Maquina("C");
        var (cliente, _) = await CoberturaCApoio.OperadorAsync(_factory, "7903", "Chefe Filtros", DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView);

        var html = await cliente.GetStringAsync("/ReparacaoInterna/Consultar");

        // Referência, Produção, Tipo de trabalho, Linha, Quem, CM/MF e as datas por último; depois «Sem Job On» e «Limpar filtros».
        var ordem = new[]
        {
            "id=\"riConsultaReferencia\"", "id=\"riConsultaProducao\"", "id=\"riConsultaTrabalho\"", "id=\"riConsultaMaquina\"",
            "id=\"riConsultaPessoa\"", "id=\"riConsultaTipo\"", "id=\"riConsultaDe\"", "id=\"riConsultaAte\"",
            "id=\"riConsultaPorAssociar\"", "id=\"riConsultaLimpar\""
        };
        var posicoes = ordem.Select(marca => Posicao(html, marca)).ToArray();
        Assert.Equal(posicoes.OrderBy(p => p).ToArray(), posicoes);
        Assert.Equal(new[] { "Referência", "Produção", "Tipo de trabalho", "Linha", "Quem", "CM ou MF", "Dia de", "Até" },
            Regex.Matches(Elemento(html, "id=\"riConsultaFiltros\""), "<label for=\"riConsulta[A-Za-z]+\">([^<]+)").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToArray());

        // «Sem Job On» (já não «Só por associar») traz a dica que explica o que filtra.
        var semJobOn = System.Net.WebUtility.HtmlDecode(Elemento(html, "class=\"ri-so-por-associar\""));
        Assert.Contains("Sem Job On", semJobOn);
        Assert.DoesNotContain("Só por associar", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("Reparações sem Job On, com a ferramenta escrita à mão e ainda não ligada a uma ferramenta da lista.", semJobOn);
    }

    [Fact]
    public async Task OsFiltrosDoHistorico_VemPelaMesmaOrdem_SemQuemNemCmMf()
    {
        Maquina("D");
        var (cliente, _) = await CoberturaCApoio.OperadorAsync(_factory, "7904", "Reparador Historico", DmoCapabilities.ReparacaoInterna);

        var html = await cliente.GetStringAsync("/ReparacaoInterna?historico=1");

        var ordem = new[] { "id=\"riHistReferencia\"", "id=\"riHistProducao\"", "id=\"riHistTrabalho\"", "id=\"riHistLinha\"", "id=\"riHistDe\"", "id=\"riHistAte\"", "id=\"riHistLimpar\"" };
        var posicoes = ordem.Select(marca => Posicao(html, marca)).ToArray();
        Assert.Equal(posicoes.OrderBy(p => p).ToArray(), posicoes);
        var filtros = Elemento(html, "id=\"riHistFiltros\"");
        Assert.Equal(new[] { ("", "Todos"), ("reparacao", "Reparação"), ("soldar", "Soldar"), ("polir", "Polir") },
            Opcoes(Elemento(filtros, "id=\"riHistTrabalho\"")));
        // O histórico é desta pessoa: não há filtro de quem nem do CM/MF.
        Assert.DoesNotContain("name=\"pessoa\"", filtros);
        Assert.DoesNotContain("name=\"tipo\"", filtros);
    }
}
