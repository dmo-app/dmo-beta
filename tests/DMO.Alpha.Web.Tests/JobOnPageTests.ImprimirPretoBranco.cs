using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O IMPRIMIR A PRETO E BRANCO (Diogo, 27/10/2026): as 4 folhas como o PDF
/// «Job-On 9395T194 Redesign», com a CS em Abertos/Fechados em vez de «2X4» e a
/// referência do Tampão como «diâmetro x calote» («34.95 x 6.5mm»). Os valores
/// vêm do gerador (JobOnImpressao).
/// </summary>
public partial class JobOnPageTests
{
    private async Task<int> SeedJobOn9395T194Async()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = "P-CM", Type = ToolType.Cm, Reference = "9395", Lot = "34" });
        db.Tools.Add(new Tool { ToolId = "P-MF", Type = ToolType.Mf, Reference = "9395", Lot = "23" });
        db.Tools.Add(new Tool { ToolId = "P-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "198" });
        var jobOn = new JobOn
        {
            Reference = "9395T194",
            ProductionNumber = "202601",
            Machine = db.Machines.FirstOrDefault(m => m.Code == "B2") ?? new Machine { Code = "B2" },
            InicioEm = new DateOnly(2025, 10, 15),
            FimEm = new DateOnly(2025, 10, 26),
            CaloteMm = 6.5m,
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "P-CM" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "P-MF" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "P-BQ" });
        var folha = new JobOnFolha { JobOn = jobOn };
        foreach (var (chave, valor) in new[]
        {
            ("cab_seccoes", "12"), ("cab_gota", "3"), ("cab_peso", "161"), ("cab_processo", "PS"),
            ("cm_reparador", "NOVO"), ("cm_utilizacao", "0.00"), ("cm_adaptador", "N/A"), ("cm_inversao", "30"), ("cm_tipo", "4 1/4''"),
            ("cm_exterior", "117.2"), ("cm_pata", "35"), ("cm_gargalo", "58.3"), ("cm_folga1", "0.13"), ("cm_folga2", "0/+0.03"),
            ("cm_notas", "TRATAR · RODAR JUSTA\nProjeto novo com refrigeração à boquilha"),
            ("tp_diametro_ps", "34.95"), ("tp_notas", "Calote 6,5 mm mais altos\nTipo C3"),
            ("mf_fundo_final", "9395"), ("mf_reparador", "NOVO"), ("mf_utilizacao", "0.00"), ("mf_adaptador", "N/A"), ("mf_inversao", "30"), ("mf_tipo", "4T"),
            ("mf_exterior", "117.2"), ("mf_corpo", "71.7/71.2"), ("mf_gargalo", "58.7"), ("mf_folga1", "0.10"), ("mf_folga2", "0/+0.03"), ("mf_folga3", "0.12"),
            ("mf_notas", "EMPARELHAR MF-FF\nRebaixo 0,3 mm\nAuditar concordância e costura desencontrada"),
            ("bq_utilizacao", "0.00"), ("bq_notas", "NOVO · TRATAR\nProjeto novo com refrigeração do MP"),
            ("an_referencia", "T194"), ("pu_referencia", "9395"),
            ("arr_referencia", "9395"), ("arr_notas", "3 primeiras filas (topo) de 1.25 para 1.50"),
            ("cs_referencia", "T194"), ("cs_furos", "2X4"), ("cs_tubo", "À FACE"),
            ("pi_diametro", "59.3"), ("pi_pincas", "Latão"), ("pi_notas", "CHANFRO POR DENTRO"),
            ("notas_gerais", "2.º fabrico na linha B2 (mesma ferramenta da linha C3)\nRebaixo MF 0,3 mm - 100%\nAlterar calote TP para 6,5 mm\nFF tamponados\nAlargar 2 mm cilíndrico dos contramoldes"),
        })
            folha.DefinirValor(chave, valor);
        db.JobOnFolhas.Add(folha);
        var ordem = 0;
        foreach (var (designacao, valor) in new[] { ("Tampão", "34.95"), ("Inversão MF", "68.7 MM"), ("Inversão MP", "25.5 MM"), ("Cab.Sopro", "56.4 X 58.6"), ("Pinças", "P 59.2 / M 58.6") })
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha { JobOn = jobOn, Ordem = ordem++, Designacao = designacao, Valor = valor });
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    [Fact]
    public async Task Folha_ImprimirPretoBranco_CsAbertosFechados_TampaoDiametroXCalote_EPaginas()
    {
        SeedUser("4585", "Chefe preto e branco", "provider-folha-imprimir-5", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using var client = await LoginClientAsync("4585", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        if (Environment.GetEnvironmentVariable("DMO_IMPRIMIR_HTML") is { Length: > 0 } destino)
            await File.WriteAllTextAsync(destino, html);
        var impressao = Zona(html, "data-jof-impressao");

        // CS: «2X4» sai como 2 abertos e 2 fechados, nunca «2X4».
        Assert.Contains("<tr><th>Abertos</th><td>2</td></tr><tr><th>Fechados</th><td>2</td></tr>", impressao, StringComparison.Ordinal);
        Assert.Contains("Abertos 2 · Fechados 2 · Tubo À FACE", impressao, StringComparison.Ordinal);
        Assert.DoesNotContain("2X4", impressao, StringComparison.Ordinal);
        // TP: a referência é «diâmetro x calote».
        Assert.Contains("<strong>34.95 x 6.5mm</strong>", impressao, StringComparison.Ordinal);
        Assert.Contains("<b>34.95 x 6.5mm</b>", impressao, StringComparison.Ordinal);
        // Cabeçalho, rodapé e páginas como no PDF.
        Assert.Contains("<span>Entrada · quarta-feira</span><b>15/10/2025</b>", impressao, StringComparison.Ordinal);
        Assert.Contains("<span>FOR 05 MOL 001 (pt)   PS</span><span>9395T194 · 202601</span><span>01 / 04</span>", impressao, StringComparison.Ordinal);
        Assert.Contains("<span>FOR 05 MOL 004 (pt)</span><span>9395T194 · 202601</span><span>03 / 04</span>", impressao, StringComparison.Ordinal);
        Assert.Contains("<tr><th>Inversão / Tipo</th><td>30 / 4T</td></tr>", impressao, StringComparison.Ordinal);
        Assert.Contains("<li><span>05</span>Alargar 2 mm cilíndrico dos contramoldes</li>", impressao, StringComparison.Ordinal);
    }
}
