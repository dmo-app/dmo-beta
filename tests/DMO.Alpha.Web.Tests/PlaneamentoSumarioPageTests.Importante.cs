using System.Reflection;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Pages.Planeamento;
using DMO.Alpha.Web.Pages.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// As regras do Sumário de Diogo, 23/10/2026: importante ou informação, os
/// dados técnicos para o Controlo Criar, o % de uso atualizado e os lotes
/// novos para as Ferramentas, os links para a página que resolve e o ponto
/// do separador.
/// </summary>
public sealed partial class PlaneamentoSumarioPageTests
{
    [Fact]
    public void Importante_SoOsQuePedemAcao()
    {
        SumarioTipo[] importantes =
        [
            SumarioTipo.ControloPorAprovar, SumarioTipo.ControloNaoAprovado, SumarioTipo.JobOnPorVer,
            SumarioTipo.FerramentaDadosEmFalta, SumarioTipo.BqMovimentoApagado, SumarioTipo.UsoAtualizado,
            SumarioTipo.LoteNovoPorCriar
        ];
        foreach (var tipo in SumarioTipos.Todos)
        {
            Assert.Equal(importantes.Contains(tipo), SumarioTipos.Importante(tipo));
        }

        Assert.False(SumarioTipos.Importante(SumarioTipo.JobOnCriado));
        Assert.False(SumarioTipos.Importante(SumarioTipo.ProducaoComeca));
        Assert.False(SumarioTipos.Importante(SumarioTipo.JobOnAlterado));
    }

    [Fact]
    public void QuemVe_DadosEmFaltaComOControloCriar_UsoELotesComAsFerramentas()
    {
        string[] controlo = [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate];
        string[] aprovar = [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove];
        string[] ferramentas = [DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas];

        Assert.True(SumarioTipos.PodeVer(SumarioTipo.FerramentaDadosEmFalta, controlo));
        Assert.False(SumarioTipos.PodeVer(SumarioTipo.FerramentaDadosEmFalta, aprovar));
        Assert.False(SumarioTipos.PodeVer(SumarioTipo.FerramentaDadosEmFalta, ferramentas));
        foreach (var tipo in new[] { SumarioTipo.UsoAtualizado, SumarioTipo.LoteNovoPorCriar })
        {
            Assert.True(SumarioTipos.PodeVer(tipo, ferramentas));
            Assert.False(SumarioTipos.PodeVer(tipo, controlo));
        }

        // Os lotes novos não são do período: mostram-se sempre, nos avisos do topo.
        Assert.False(SumarioTipos.DoPeriodo(SumarioTipo.LoteNovoPorCriar));
        Assert.Equal(DmoCapabilities.Ferramentas, DmoAvisoLink.CriarLoteNovo(1, "CM", "R", "1").Modulo);
    }

    /// <summary>As páginas onde as linhas se resolvem e o módulo que as abre.</summary>
    private static readonly IReadOnlyDictionary<string, Type> Destinos = new Dictionary<string, Type>
    {
        ["/JobOn"] = typeof(Pages.JobOn.IndexModel),
        ["/Controlo/Resumo"] = typeof(Pages.Controlo.ResumoModel),
        ["/Controlo/Approve"] = typeof(Pages.Controlo.ApproveModel),
        ["/Boquilhas"] = typeof(Pages.Boquilhas.IndexModel),
        ["/Ferramentas/Ficha"] = typeof(Pages.Ferramentas.FichaModel)
    };

    // «No sumário mostrar sempre os links que levam à página que resolve o
    // problema» (Diogo, 23/10/2026): cada tipo, para cada conta que o vê, tem
    // um link para uma página que essa conta abre.
    [Fact]
    public void CadaLinha_TemLink_ParaUmaPaginaQueAContaAbre()
    {
        string[][] contas =
        [
            [DmoCapabilities.JobOnCreate],
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate],
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove],
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove],
            [DmoCapabilities.JobOnCreate, DmoCapabilities.Boquilhas],
            [DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas],
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.Ferramentas]
        ];
        foreach (var modulos in contas)
        {
            foreach (var tipo in SumarioTipos.Visiveis(modulos).Where(SumarioTipos.DoPeriodo))
            {
                var item = new SumarioItem(tipo, Hoje, null, "T", null, null) { JobOnId = 7, ToolId = "CM-1", BqContextId = 3 };
                var href = SumarioModel.Href(item, modulos);
                Assert.True(href is not null, $"{tipo} sem link para {string.Join("+", modulos)}");
                var pagina = Destinos[href!.Split('?')[0]];
                var entrada = pagina.GetCustomAttribute<ModuleEntryAttribute>()!;
                Assert.True(
                    modulos.Contains(entrada.Module) || entrada.AlsoModules.Any(modulos.Contains),
                    $"{tipo}: {href} não abre para {string.Join("+", modulos)}");
            }
        }

        // Os por aprovar abrem a Revisão a quem aprova e o Resumo a quem só tem o Controlo Criar.
        var porAprovar = new SumarioItem(SumarioTipo.ControloPorAprovar, Hoje, null, "T", null, null) { JobOnId = 7 };
        Assert.StartsWith("/Controlo/Approve?jobon_id=7", SumarioModel.Href(porAprovar, [DmoCapabilities.ControloApprove]));
        Assert.Equal("/Controlo/Resumo?jobon_id=7", SumarioModel.Href(porAprovar, [DmoCapabilities.ControloCreate]));
    }

    // «Não tem os links para a página onde se pode corrigir» (Diogo,
    // 26/10/2026): a linha inteira abre a página que resolve, sem botões;
    // o link do título estica-se pela linha e a linha mostra que se clica.
    [Fact]
    public async Task UmImportante_ALinhaInteiraAbreAPaginaQueResolve()
    {
        var jobOnId = await JobOnAsync("S9L001", Hoje, DateTime.UtcNow);
        using var client = await LoginAsync("5902", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        var grupo = GrupoImportante(await GetAsync(client));
        Assert.Matches($"<li class=\"sumario-item mch-B1\" data-sumario-tipo=\"por-ver\">[\\s\\S]*?<a class=\"sumario-titulo\" href=\"/Controlo/Resumo\\?jobon_id={jobOnId}\">", grupo);
        Assert.DoesNotContain("<button", grupo);
        var css = await client.GetStringAsync("/css/dmo-sumario.css");
        Assert.Contains(".sumario-item{position:relative}", css);
        Assert.Contains(".sumario-item > a.sumario-titulo::after{content:\"\";position:absolute;inset:0;border-radius:8px}", css);
        Assert.Contains(".sumario-item:has(> a.sumario-titulo){cursor:pointer}", css);
        Assert.DoesNotContain("text-decoration:underline", css);
    }

    /// <summary>O grupo «Importante» do topo da lista.</summary>
    private static string GrupoImportante(string html) =>
        Regex.Match(html, "<div class=\"sumario-grupo sumario-importante sumario-topo\" data-sumario-tipo=\"importante\">[\\s\\S]*?</ul>").Value;

    // «Criar uma Informação "Importante" e ficam sempre no topo até resolver»
    // (Diogo, 23/10/2026): um grupo no topo, antes do dia escolhido; a
    // informação fica por baixo, por tipo, no período.
    [Fact]
    public async Task OsImportantes_FicamNumGrupoNoTopo_AInformacaoPorBaixo()
    {
        await JobOnAsync("S9I001", Hoje, DateTime.UtcNow);
        using var client = await LoginAsync("5901", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        var html = await GetAsync(client);

        var grupo = GrupoImportante(html);
        Assert.Contains("<h3>Importante</h3>", grupo);
        Assert.Matches("<li class=\"sumario-item mch-B1\" data-sumario-tipo=\"por-ver\">", grupo);
        Assert.Contains("<span class=\"sumario-etiqueta\">Job Ons por ver no Controlo</span>", grupo);
        Assert.True(html.IndexOf("data-sumario-tipo=\"importante\"", StringComparison.Ordinal) < html.IndexOf("id=\"sumario-lista-title\"", StringComparison.Ordinal));
        Assert.Contains("<div class=\"sumario-grupo\" data-sumario-tipo=\"criados\">", html);
        Assert.Contains("<div class=\"sumario-grupo\" data-sumario-tipo=\"comecam\">", html);
        Assert.DoesNotContain("<h3>Job Ons por ver no Controlo</h3>", html);
        var css = await client.GetStringAsync("/css/dmo-sumario.css");
        Assert.Contains(".sumario-grupo.sumario-importante .sumario-item{box-shadow:inset 3px 0 0 var(--dmo-warning)}", css);

        // O filtro «Importante» mostra só o grupo; um de informação, só a informação.
        var so = await GetAsync(client, "?tipo=importante");
        Assert.Contains("<option value=\"importante\" selected=\"selected\">Importante</option>", so);
        Assert.Contains("S9I001 7", GrupoImportante(so));
        Assert.DoesNotContain("data-sumario-tipo=\"criados\"", so);
        var criados = await GetAsync(client, "?tipo=criados");
        Assert.DoesNotContain("data-sumario-tipo=\"importante\"", criados);
        Assert.Contains("data-sumario-tipo=\"criados\"", criados);
        // Os tipos importantes não são opções à parte do filtro.
        Assert.DoesNotContain("<option value=\"por-ver\"", html);
    }

    // Um importante de outro dia continua no topo hoje e em qualquer dia
    // escolhido, até ser resolvido; resolvido, sai.
    [Fact]
    public async Task UmImportanteDeOutroDia_FicaAteResolver_EDepoisSai()
    {
        var jobOnId = await JobOnAsync("S9R001", Hoje.AddDays(-3), DateTime.UtcNow.AddDays(-3));
        using var client = await LoginAsync("5951", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        Assert.Contains("S9R001 7", GrupoImportante(await GetAsync(client)));
        var outro = Hoje.AddDays(-10);
        Assert.Contains("S9R001 7", GrupoImportante(await GetAsync(client, $"?ano={outro.Year}&mes={outro.Month}&dia={outro.Day}")));
        Assert.Contains("S9R001 7", GrupoImportante(await GetAsync(client, $"?ano={Hoje.Year}&mes={Hoje.Month}&dia=0")));

        // Resolvido: alguém abriu o Resumo.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            db.ControloViewings.Add(new DMO.Alpha.Core.Controlo.ControloViewing { ControloId = controlo.Id, OperatorId = "5951", ViewedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain("S9R001 7", GrupoImportante(await GetAsync(client)));
    }

    // Um controlo não aprovado há dias continua no topo até a decisão passar
    // a aprovado.
    [Fact]
    public async Task NaoAprovado_FicaAteSerAprovado()
    {
        var jobOnId = await JobOnAsync("S9N001", Hoje.AddDays(-4));
        int controloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            controloId = controlo.Id;
            db.ControloDecisoes.Add(new DMO.Alpha.Core.Controlo.ControloDecisao
            {
                ControloId = controlo.Id, Parte = DMO.Alpha.Core.Controlo.ControloParte.Peso, Aprovado = false,
                OperatorId = "5971", EmUtc = DateTime.UtcNow.AddDays(-4), Nota = "Refazer S9N"
            });
            db.ControloViewings.Add(new DMO.Alpha.Core.Controlo.ControloViewing { ControloId = controlo.Id, OperatorId = "5971", ViewedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var client = await LoginAsync("5971", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove);
        var grupo = GrupoImportante(await GetAsync(client));
        Assert.Contains("<span class=\"sumario-etiqueta\">Controlos não aprovados</span>", grupo);
        Assert.Contains("Refazer S9N", grupo);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var decisao = await db.ControloDecisoes.SingleAsync(d => d.ControloId == controloId);
            decisao.Aprovado = true;
            decisao.EmUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain("Refazer S9N", GrupoImportante(await GetAsync(client)));
    }

    // Os movimentos de boquilhas apagados não têm estado de «resolvido»:
    // ficam no topo 7 dias (Diogo, 23/10/2026).
    [Fact]
    public async Task BqApagados_FicamNoTopoSeteDias()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            foreach (var (dias, quem) in new[] { (5, "Apagou S9B5"), (8, "Apagou S9B8") })
            {
                db.BqMovementDeletions.Add(new DMO.Alpha.Core.Boquilhas.BqMovementDeletion
                {
                    BqMovementId = 900 + dias, BqRepairTraceId = 9900 + dias, Type = DMO.Alpha.Core.Boquilhas.BqMovementType.Saida, Quantity = 1,
                    OccurredAtUtc = DateTime.UtcNow.AddDays(-dias), MachineCodeFrozen = "C1", DeletedByNameFrozen = quem, DeletedAtUtc = DateTime.UtcNow.AddDays(-dias)
                });
            }

            await db.SaveChangesAsync();
        }

        using var client = await LoginAsync("5961", DmoCapabilities.JobOnCreate, DmoCapabilities.Boquilhas);
        var grupo = GrupoImportante(await GetAsync(client));
        Assert.Contains("Apagou S9B5", grupo);
        Assert.DoesNotContain("Apagou S9B8", grupo);
    }

    // «% de uso atualizada» (Diogo, 23/10/2026): quando um Job On acaba, o %
    // de uso das CM, MF e BQ dele muda; fica no topo até haver a leitura do
    // fim, só a quem tem o módulo Ferramentas, com o link para a ficha.
    [Fact]
    public async Task UsoAtualizado_FicaAteHaverALeituraDoFim_SoQuemTemFerramentas()
    {
        var jobOnId = await JobOnAsync("S9U001", Hoje.AddDays(-5));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            (await db.JobOns.SingleAsync(j => j.Id == jobOnId)).FimEm = Hoje.AddDays(-2);
            db.Tools.Add(new Tool { ToolId = "CM-S9U001", Type = ToolType.Cm, Reference = "S9U001", Lot = "4" });
            db.Tools.Add(new Tool { ToolId = "MF-S9U001", Type = ToolType.Mf, Reference = "S9U001", Lot = "5" });
            db.CmContexts.Add(new CmContext { JobOnId = jobOnId, ToolId = "CM-S9U001" });
            db.MfContexts.Add(new MfContext { JobOnId = jobOnId, ToolId = "MF-S9U001" });
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "CM-S9U001", Percentagem = 10m, RegistadaEmUtc = DateTime.UtcNow.AddDays(-6) });
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "CM-S9U001", Percentagem = 12.5m, RegistadaEmUtc = DateTime.UtcNow });
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "MF-S9U001", Percentagem = 30m, RegistadaEmUtc = DateTime.UtcNow.AddDays(-6) });
            await db.SaveChangesAsync();
        }

        using (var planeamento = await LoginAsync("5911", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate))
        {
            var html = await GetAsync(planeamento);
            Assert.DoesNotContain("% de uso atualizada", html);
            Assert.DoesNotContain("S9U001 · Lote", html);
        }

        using var ferramentas = await LoginAsync("5912", DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas);
        // O Job On acabou há dois dias e continua no topo hoje.
        var grupo = GrupoImportante(await GetAsync(ferramentas, "?tipo=uso-atualizado"));
        Assert.Contains("<span class=\"sumario-etiqueta\">% de uso atualizada</span>", grupo);
        Assert.Contains("href=\"/Ferramentas/Ficha?tool_id=MF-S9U001\">MF S9U001 · Lote 5</a>", grupo);
        Assert.Contains("Falta a leitura do % de uso depois do fim · S9U001 7", grupo);
        // A CM já tem a leitura do fim: está resolvida.
        Assert.DoesNotContain("CM-S9U001", grupo);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "MF-S9U001", Percentagem = 33m, RegistadaEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain("MF-S9U001", GrupoImportante(await GetAsync(ferramentas)));
    }

    private static readonly Regex PontoDoSumario = new("href=\"/Planeamento/Sumario\"\\s+class=\"dmo-tab-novo");

    // O ponto do separador (Diogo, 23/10/2026): pequeno, sem número, quando
    // há importantes que a conta ainda não viu; abrir o Sumário apaga-o e um
    // importante novo volta a pô-lo.
    [Fact]
    public async Task OPonto_ApareceComImportantesPorVer_EDesapareceAoAbrirOSumario()
    {
        await JobOnAsync("S9D001", Hoje, DateTime.UtcNow.AddMinutes(-5));
        using var client = await LoginAsync("5921", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        var jobOn = await (await client.GetAsync("/JobOn")).Content.ReadAsStringAsync();
        Assert.Matches(PontoDoSumario, jobOn);
        Assert.Contains(".dmo-tab-novo::after{", await client.GetStringAsync("/css/dmo-normalize.css"));

        // No Sumário não há ponto; depois de o abrir, também não no Job On.
        Assert.DoesNotMatch(PontoDoSumario, await GetAsync(client));
        Assert.DoesNotMatch(PontoDoSumario, await (await client.GetAsync("/JobOn")).Content.ReadAsStringAsync());

        // Um importante novo (um Job On criado depois, por ver no Controlo) volta a pôr o ponto.
        await JobOnAsync("S9D002", Hoje, DateTime.UtcNow.AddMinutes(1));
        ((MemoryCache)_factory.Services.GetRequiredService<IMemoryCache>()).Compact(1.0);
        Assert.Matches(PontoDoSumario, await (await client.GetAsync("/JobOn")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OPonto_NaoApareceSoComInformacao()
    {
        // Só com o Planeamento os Job Ons criados são informação: sem ponto.
        await JobOnAsync("S9D101", Hoje, DateTime.UtcNow);
        using var client = await LoginAsync("5931", DmoCapabilities.JobOnCreate);

        var jobOn = await (await client.GetAsync("/JobOn")).Content.ReadAsStringAsync();
        Assert.Contains(">Sumário</a>", jobOn);
        Assert.DoesNotMatch(PontoDoSumario, jobOn);
    }

    [Fact]
    public async Task OPonto_ContaOsLotesNovosPorCriar_ParaQuemTemFerramentas()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ArmazemLotesNovos.Add(new DMO.Alpha.Core.Armazem.ArmazemLoteNovo
            {
                Tipo = ToolType.Cm, Referencia = "S9L001", Lote = "1", Posicao = "1001", RegistadoEmUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var semFerramentas = await LoginAsync("5941", DmoCapabilities.JobOnCreate))
        {
            Assert.DoesNotMatch(PontoDoSumario, await (await semFerramentas.GetAsync("/JobOn")).Content.ReadAsStringAsync());
        }

        using var ferramentas = await LoginAsync("5942", DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas);
        Assert.Matches(PontoDoSumario, await (await ferramentas.GetAsync("/JobOn")).Content.ReadAsStringAsync());
        var sumario = await GetAsync(ferramentas);
        Assert.Contains(">CM S9L001 · Lote 1</a>", GrupoImportante(sumario));
        // Os lotes novos não são uma opção do filtro: estão no grupo «Importante».
        Assert.DoesNotContain("value=\"lotes-novos\"", sumario);
        Assert.DoesNotMatch(PontoDoSumario, await (await ferramentas.GetAsync("/JobOn")).Content.ReadAsStringAsync());
    }
}
