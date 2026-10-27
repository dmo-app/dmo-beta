using System.Reflection;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Pages.Planeamento;

namespace DMO.Alpha.Web.Tests;

public sealed partial class PlaneamentoSumarioPageTests
{
    // Quem só tem o Job On ver também recebe os Job On criados, as mudanças e as
    // alterações (blueprint, Sumário §2: «toda a gente»; Diogo, 27/10/2026: «so ve os
    // detalhes do plnaneamento e os jobons»); o link abre o Planeamento, que lhe
    // mostra a folha em leitura. O Controlo, as Boquilhas e as Ferramentas não.
    [Fact]
    public async Task JobOnVer_RecebeOsJobOnEAsMudancas_ComLinkQueAbre()
    {
        string[] soVer = [DmoCapabilities.JobOnView];
        foreach (var tipo in new[] { SumarioTipo.JobOnCriado, SumarioTipo.ProducaoComeca, SumarioTipo.JobOnAlterado })
        {
            Assert.True(SumarioTipos.PodeVer(tipo, soVer), $"{tipo} não chega a quem só vê");
        }
        foreach (var tipo in new[] { SumarioTipo.JobOnPorVer, SumarioTipo.ControloPorAprovar, SumarioTipo.ControloNaoAprovado,
                     SumarioTipo.BqMovimentoApagado, SumarioTipo.FerramentaDadosEmFalta, SumarioTipo.UsoAtualizado, SumarioTipo.LoteNovoPorCriar })
        {
            Assert.False(SumarioTipos.PodeVer(tipo, soVer), $"{tipo} chega a quem só vê");
        }
        foreach (var tipo in SumarioTipos.Visiveis(soVer).Where(SumarioTipos.DoPeriodo))
        {
            var item = new SumarioItem(tipo, Hoje, null, "T", null, null) { JobOnId = 7, ToolId = "CM-1", BqContextId = 3 };
            var href = SumarioModel.Href(item, soVer);
            Assert.True(href is not null, $"{tipo} sem link para quem só vê");
            var caminho = href!.Split('?')[0];
            if (caminho == "/JobOn/Folha")
            {
                continue;
            }
            var entrada = Destinos[caminho].GetCustomAttribute<ModuleEntryAttribute>()!;
            Assert.True(entrada.Module == DmoCapabilities.JobOnView || entrada.AlsoModules.Contains(DmoCapabilities.JobOnView),
                $"{tipo}: {href} não abre para quem só vê");
        }

        var hoje = await JobOnAsync("S9V001", Hoje);
        using var client = await LoginAsync("5606", DmoCapabilities.JobOnView);
        var html = await GetAsync(client);
        Assert.Contains($"<a class=\"sumario-titulo\" href=\"/JobOn?jobon_id={hoje}\">S9V001 7</a>", html);
    }
}
