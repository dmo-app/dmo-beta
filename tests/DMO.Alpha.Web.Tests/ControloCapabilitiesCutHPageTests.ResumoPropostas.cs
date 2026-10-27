using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // As duas propostas de design do Resumo (Diogo, 27/10/2026: «quero
    // melhorar esse design do resumo»; 09:36: «Salientar os resultados do
    // peso comparando ao controlo anterior, produção anterior e nominal.
    // Salientar resultados dos pegamentos e registo das Punções caso NNPB»).
    // O ?proposta=a|b só marca o <main>; o markup e os resultados são os
    // mesmos nas duas e no aspeto de hoje.
    [Fact]
    public async Task Resumo_Propostas_MarcamOMain_EMostramOsResultados()
    {
        var (jobOnId, _) = await SeedResumoComResultadosAsync(aprovado: false, comPegamentos: true);
        try
        {
            await PropostasAsync(jobOnId);
        }
        finally
        {
            ApagarPuncoes();
        }
    }

    private async Task PropostasAsync(int jobOnId)
    {
        SeedUserWithTemplate("4721", "Operador Propostas", "provider-resumo-propostas", "Propostas1!",
            "tpl-teste-propostas", "Propostas", [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate]);
        using var client = await LoginClientAsync("4721", "Propostas1!");

        var atual = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.DoesNotContain("data-resumo-proposta", atual);
        Assert.DoesNotContain("data-resumo-proposta", await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}&proposta=x"));

        foreach (var proposta in new[] { "a", "b" })
        {
            var html = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}&proposta={proposta}");
            Assert.Contains($"data-dmo-troca data-resumo-proposta=\"{proposta}\">", html);
            Assert.Contains("class=\"resumo-coluna resumo-coluna-principal\"", html);
            Assert.Contains("class=\"resumo-coluna resumo-coluna-documento\"", html);
            Assert.Contains("&amp;comparacao=1\" aria-label=\"Comparar o Peso desta produção\">Comparar</a>", html);

            // O Peso: a média e as três comparações, com o valor e a diferença.
            var tabela = Regex.Match(html, "<table class=\"dmo-table resumo-peso-tabela\"[\\s\\S]*?</table>").Value;
            var linhas = Regex.Matches(tabela, "<tr>\\s*<th scope=\"row\">([\\s\\S]*?)</tr>")
                .Select(m => Regex.Replace(Regex.Replace(m.Groups[1].Value, "<[^>]+>", " "), "\\s+", " ").Trim())
                .ToArray();
            Assert.Equal(3, linhas.Length);
            Assert.StartsWith("Nominal", linhas[0]);
            Assert.Contains("225.00 g", linhas[0]);
            Assert.StartsWith("Controlo anterior 202571", linhas[1]);
            Assert.DoesNotContain("—", linhas[1]);
            Assert.StartsWith("Produção anterior 202571", linhas[2]);
            Assert.Contains("226.00 g", linhas[2]);
            Assert.DoesNotContain("—", linhas[2]);

            // Os Pegamentos e as PU NNPB: os totais, não só «Registado».
            Assert.Contains("<div><span>Medições</span><b>6</b></div>", html);
            Assert.Contains("<div><span>Em alerta</span><b>1</b></div>", html);
            Assert.Contains("<div><span>Punções</span><b>8</b></div>", html);
            Assert.Contains("<div><span>Arrefecedores</span><b>7</b></div>", html);
        }
    }

    /// <summary>As PU NNPB semeadas (o ResetDomainState não as apaga).</summary>
    private void ApagarPuncoes()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PuncaoInspecoes.RemoveRange(db.PuncaoInspecoes);
        db.Puncoes.RemoveRange(db.Puncoes);
        db.SaveChanges();
    }

    /// <summary>
    /// Uma produção NNPB com resultados: o Peso com leituras, o nominal, o
    /// controlo anterior (202571, com Peso) e a produção anterior escolhida
    /// com o peso do SAP; as PU NNPB (8 punções, 7 arrefecedores vistos);
    /// CM OK, BQ NOK com comentário; com ou sem os Pegamentos (6 medições,
    /// 1 em alerta) e com ou sem a aprovação.
    /// </summary>
    private async Task<(int JobOnId, int ControloId)> SeedResumoComResultadosAsync(bool aprovado, bool comPegamentos, string? operadorId = null)
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var agora = DateTime.UtcNow;

        var jobOn = await db.JobOns.SingleAsync(j => j.Id == jobOnId);
        jobOn.ProductionNumber = "202604";
        jobOn.InicioEm = new DateOnly(2026, 10, 20);
        jobOn.FimEm = new DateOnly(2026, 10, 31);
        var anterior = new JobOn { Reference = jobOn.Reference, ProductionNumber = "202571", MachineId = jobOn.MachineId,
            InicioEm = new DateOnly(2026, 9, 1), FimEm = new DateOnly(2026, 9, 12) };
        db.JobOns.Add(anterior);
        await db.SaveChangesAsync();
        var controloAnterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anterior.Id };
        db.Controlos.Add(controloAnterior);
        await db.SaveChangesAsync();

        var pesoAnterior = new Peso { ControloId = controloAnterior.Id, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
            WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, GlassDensityGramsPerCm3 = 2.5m, NominalWeightGrams = 225m };
        foreach (var (posicao, agua) in new[] { (1, 149.6m), (2, 149.8m), (3, 149.5m), (4, 149.7m) })
            pesoAnterior.Measurements.Add(new PesoMeasurement { RowPosition = posicao, CmSubject = posicao.ToString(), WaterWeightGrams = agua });
        db.Pesos.Add(pesoAnterior);

        var peso = new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
            WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, GlassDensityGramsPerCm3 = 2.5m, NominalWeightGrams = 225m,
            ProducaoAnteriorControloId = controloAnterior.Id, ProducaoAnteriorPesoGramas = 226m, RegistadoEmUtc = agora };
        foreach (var (posicao, agua) in new[] { (1, 150.0m), (2, 150.4m), (3, 149.8m), (4, 150.2m), (5, 150.1m), (6, 150.3m) })
            peso.Measurements.Add(new PesoMeasurement { RowPosition = posicao, CmSubject = posicao.ToString(), WaterWeightGrams = agua });
        db.Pesos.Add(peso);

        var puncoes = new Puncoes { ControloId = controloId, PunchReference = "5810", Lot = "L24", UpdatedAtUtc = agora };
        for (var posicao = 1; posicao <= 8; posicao++)
            puncoes.Inspecoes.Add(new PuncaoInspecao { Position = posicao, Number = $"P{posicao:00}", Lot = "L24",
                PunchSeen = true, CoolerSeen = aprovado || posicao != 5, RegisteredAtUtc = agora });
        db.Puncoes.Add(puncoes);

        if (comPegamentos)
        {
            var folha = new Pegamentos { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", MfToolId = "MF-TOOL-9",
                ToleranceMm = 0.10m, RegistadoEmUtc = agora };
            foreach (var (posicao, componente, costura) in new[]
                     {
                         (1, PegamentosComponent.Cm, 70.02m), (2, PegamentosComponent.Cm, 69.97m), (3, PegamentosComponent.Bq, 70.04m),
                         (4, PegamentosComponent.Bq, 70.24m), (5, PegamentosComponent.Mf, 69.99m), (6, PegamentosComponent.Mf, 70.03m)
                     })
                folha.Measurements.Add(new PegamentosMeasurement { RowPosition = posicao, Component = componente,
                    CosturaMm = costura, ContraCosturaMm = 70.00m, NominalGargaloMm = 70.00m, RegistadoEmUtc = agora });
            db.Pegamentos.Add(folha);
        }

        db.ControloAreaStates.Add(new ControloAreaState { ControloId = controloId, Area = "CM", Status = "OK", UpdatedBy = operadorId, UpdatedAtUtc = agora });
        db.ControloAreaStates.Add(new ControloAreaState { ControloId = controloId, Area = "BQ", Status = "NOK",
            Comment = "Marca de desgaste na boquilha 3: enviada para reparação.", UpdatedBy = operadorId, UpdatedAtUtc = agora });

        if (aprovado && operadorId is not null)
        {
            var controlo = await db.Controlos.SingleAsync(c => c.Id == controloId);
            controlo.ApprovedByOperatorId = operadorId;
            controlo.ApprovedAtUtc = agora;
        }

        await db.SaveChangesAsync();
        return (jobOnId, controloId);
    }
}
