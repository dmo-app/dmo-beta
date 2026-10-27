using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Pages.Planeamento;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class PlaneamentoSumarioPageTests
{
    // Blueprint, Sumário §3 (Diogo, 19/10/2026): «quem tem Armazém recebe na
    // página as ferramentas que saíram ou entraram»; Diogo, 26/10/2026
    // (cmsg_01K75FTs9bQnbhhH7qttpgra5RmpCY7HiWL1Fi338S3eFf): quem tem o Job On
    // ver «vê alertas de mudanças do armazém de lotes que mudam».
    [Fact]
    public async Task ArmazemMovimentos_ParaQuemTemJobOnVer_JobOnCriar_OuArmazem()
    {
        var jobOnId = await JobOnAsync("S6A001", Hoje, DateTime.UtcNow);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "CM-S6A001", Type = ToolType.Cm, Reference = "S6A001", Lot = "07" });
            var posicao = new ArmazemPosicao { Codigo = "4048", CriadaEmUtc = DateTime.UtcNow };
            db.ArmazemPosicoes.Add(posicao);
            await db.SaveChangesAsync();
            db.ArmazemMovimentos.Add(new ArmazemMovimento
            {
                Tipo = ArmazemMovimentoTipo.Entrada, ToolId = "CM-S6A001", PosicaoId = posicao.Id, Origem = ArmazemOrigem.Producao,
                JobOnId = jobOnId, OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow,
                RegistadoPor = "Operador Armazém", OperacaoId = Guid.NewGuid()
            });
            db.ArmazemMovimentos.Add(new ArmazemMovimento
            {
                Tipo = ArmazemMovimentoTipo.Saida, ToolId = "CM-S6A001", PosicaoAnteriorId = posicao.Id, Destino = ArmazemDestino.Reparacao,
                ReparadorNome = "Moldes Lda", OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }

        using (var soVer = await LoginAsync("5604", DmoCapabilities.JobOnView))
        {
            var html = await GetAsync(soVer, "?tipo=armazem");
            Assert.Contains("<h3>Movimentos do Armazém</h3>", html);
            Assert.Contains("CM S6A001 · Lote 7", html);
            Assert.Contains("Entrada na posição 4048", html);
            Assert.Contains("Saída da posição 4048 para Reparação (Moldes Lda)", html);
            Assert.Contains("Operador Armazém", html);
            Assert.Contains($"href=\"/JobOn/Folha?jobon_id={jobOnId}\"", html);
        }

        using (var soBoquilhas = await LoginAsync("5605", DmoCapabilities.JobOnCreate, DmoCapabilities.Boquilhas))
        {
            Assert.Contains("Entrada na posição 4048", await GetAsync(soBoquilhas, "?tipo=armazem"));
        }

        // O Armazém sozinho não abre o Sumário (é preciso o Job On ver), mas o
        // tipo é dele e o link leva aos Movimentos do Armazém.
        Assert.True(SumarioTipos.PodeVer(SumarioTipo.ArmazemMovimento, [DmoCapabilities.ArmazemGestao]));
        Assert.False(SumarioTipos.PodeVer(SumarioTipo.ArmazemMovimento, [DmoCapabilities.Ferramentas]));
        Assert.False(SumarioTipos.Importante(SumarioTipo.ArmazemMovimento));
        var item = new SumarioItem(SumarioTipo.ArmazemMovimento, Hoje, null, "T", null, null) { ToolId = "CM-S6A001", JobOnId = jobOnId };
        Assert.Equal("/Armazem/Movimentos?ferramenta=CM-S6A001", SumarioModel.Href(item, [DmoCapabilities.JobOnView, DmoCapabilities.ArmazemGestao]));
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=CM-S6A001", SumarioModel.Href(item, [DmoCapabilities.JobOnView, DmoCapabilities.Ferramentas]));
        Assert.Equal($"/JobOn?jobon_id={jobOnId}", SumarioModel.Href(item, [DmoCapabilities.JobOnCreate]));
    }
}
