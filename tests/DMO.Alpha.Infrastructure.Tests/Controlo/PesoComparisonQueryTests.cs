using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// A4 — O DROPDOWN DA COMPARAÇÃO FILTRADO PELA FOLHA DAS FERRAMENTAS (o
/// Owner 2026-10-12: «se a tool aceita B1 e C3 e o ultimo for C3 pode
/// ser comparado, se a tool trabalha com B1 apenas, e o ultimo for B3 ai
/// ja nao pode ser... Usar a informaçao que esta na folha das
/// ferramentas para decidir o que pode ser comparado»): os candidatos
/// da Comparação_B ficam restritos às produções da MESMA referência
/// cujas MÁQUINAS estejam no ToolMachineLink da CM corrente da produção
/// (a cruzada PERMITIDA: a tool [B1, C3] também mostra as da C3); a
/// máquina fora da ficha simplesmente NÃO APARECE (a opção não existe —
/// nunca um bloqueio em runtime). Sem CM no contexto ou com a ficha
/// ainda SEM máquinas ligadas, não há informação de compatibilidade: o
/// conjunto completo da referência continua (função antes de proteção).
/// O RANKING MANTÉM-SE: a última da MESMA MÁQUINA primeiro (o desgaste).
/// </summary>
public sealed class PesoComparisonQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoComparisonQuery _query;

    public PesoComparisonQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new PesoComparisonQuery(_db, new PesoQuery(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Machine> SeedMachineAsync(string code)
    {
        var machine = new Machine { Code = code };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task<Tool> SeedToolAsync(string toolId, ToolType type, string reference, string lot)
    {
        var tool = new Tool { ToolId = toolId, Type = type, Reference = reference, Lot = lot };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    /// <summary>
    /// Uma produção completa (Job On + CM do contexto + controlo + Peso
    /// com UMA leitura) devolve o controlo_id dela para o dropdown da
    /// comparação.
    /// </summary>
    private async Task<int> SeedProductionWithPesoAsync(
        Machine machine,
        string productionNumber,
        string reference,
        string? cmToolId,
        decimal waterWeight = 200m,
        DateOnly? inicioEm = null)
    {
        var jobOn = new JobOn
        {
            Reference = reference,
            ProductionNumber = productionNumber,
            MachineId = machine.Id,
            InicioEm = inicioEm
        };
        _db.JobOns.Add(jobOn);
        if (cmToolId is not null)
        {
            _db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = cmToolId });
        }

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();

        var peso = new Peso
        {
            CmToolId = cmToolId ?? "T-ST100",
            BqToolId = "BQ-1",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.5m,
            ControloId = controlo.Id
        };
        _db.Pesos.Add(peso);
        _db.PesoMeasurements.Add(new PesoMeasurement
        {
            PesoId = peso.Id,
            RowPosition = 1,
            CmSubject = "1",
            WaterWeightGrams = waterWeight
        });
        await _db.SaveChangesAsync();
        return controlo.Id;
    }

    [Fact]
    public async Task FindCandidates_ToolLinkedToB1AndC3_TheDropdownHasC3()
    {
        // A FOLHA DECIDE: a CM corrente (ST100, lote 8) trabalha em
        // [B1, C3] — as produções da MESMA referência na B1 E na C3
        // aparecem (a cruzada PERMITIDA); a da B2 (fora da ficha) não.
        var b1 = await SeedMachineAsync("B1");
        var c3 = await SeedMachineAsync("C3");
        var b2 = await SeedMachineAsync("B2");
        await SeedToolAsync("T-ST100", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T-ST100", MachineId = b1.Id });
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T-ST100", MachineId = c3.Id });
        await _db.SaveChangesAsync();

        var controloOnB1 = await SeedProductionWithPesoAsync(b1, "P-202601", "1075C142", "T-ST100");
        var controloOnC3 = await SeedProductionWithPesoAsync(c3, "P-202602", "1075C142", "T-ST100");
        var controloOnB2 = await SeedProductionWithPesoAsync(b2, "P-202603", "1075C142", "T-ST100");
        var current = await SeedProductionWithPesoAsync(b1, "P-202604", "1075C142", "T-ST100");

        var candidates = await _query.FindCandidatesAsync(current);

        // O DROPDOWN TEM a C3 (e a B1): a cruzada B1×C3 da MESMA tool é
        // permitida pela ficha.
        Assert.Contains(candidates, c => c.ProductionNumber == "P-202601" && c.Machine == "B1");
        Assert.Contains(candidates, c => c.ProductionNumber == "P-202602" && c.Machine == "C3");
        // A B2 (fora da folha da tool) NÃO aparece — a opção não existe.
        Assert.DoesNotContain(candidates, c => c.ProductionNumber == "P-202603");

        // Só a recência (o Owner 2026-10-17: «basta apenas produção»): a
        // mais recente primeiro, seja qual for a máquina.
        Assert.Equal("P-202602", candidates[0].ProductionNumber);
        Assert.Equal("P-202601", candidates[1].ProductionNumber);

        Assert.Equal(2, candidates.Count);
        Assert.NotEqual(controloOnB1, current);
        Assert.NotEqual(controloOnC3, current);
        Assert.NotEqual(controloOnB2, current);
    }

    [Fact]
    public async Task FindCandidates_ToolLinkedToB1Only_TheDropdownHasNoB3()
    {
        // A CM corrente trabalha APENAS na B1: as produções da mesma
        // referência noutras máquinas (B3) NÃO aparecem no dropdown — o
        // que a ficha não aceita, não pode ser comparado.
        var b1 = await SeedMachineAsync("B1");
        var b3 = await SeedMachineAsync("B3");
        await SeedToolAsync("T-ST100", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T-ST100", MachineId = b1.Id });
        await _db.SaveChangesAsync();

        await SeedProductionWithPesoAsync(b1, "P-202601", "1075C142", "T-ST100");
        var onB3 = await SeedProductionWithPesoAsync(b3, "P-202602", "1075C142", "T-ST100");
        var current = await SeedProductionWithPesoAsync(b1, "P-202603", "1075C142", "T-ST100");

        var candidates = await _query.FindCandidatesAsync(current);

        // O DROPDOWN NÃO TEM a B3 (a produção dela existe, o Peso dela
        // existe — a opção é que não existe: a ficha decide).
        Assert.DoesNotContain(candidates, c => c.Machine == "B3");
        Assert.DoesNotContain(candidates, c => c.ProductionNumber == "P-202602");
        Assert.Single(candidates);
        Assert.Equal("P-202601", candidates[0].ProductionNumber);
        Assert.NotEqual(onB3, current);
    }

    [Fact]
    public async Task FindCandidates_ToolWithoutMachineLinks_AllMachinesOfTheReference()
    {
        // A ficha da CM ainda SEM máquinas ligadas (0 é permitido —
        // completa depois): não há informação de compatibilidade, o
        // conjunto COMPLETO da referência continua (função antes de
        // proteção).
        var b1 = await SeedMachineAsync("B1");
        var b3 = await SeedMachineAsync("B3");
        await SeedToolAsync("T-ST100", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        await _db.SaveChangesAsync();

        await SeedProductionWithPesoAsync(b1, "P-202601", "1075C142", "T-ST100");
        await SeedProductionWithPesoAsync(b3, "P-202602", "1075C142", "T-ST100");
        var current = await SeedProductionWithPesoAsync(b1, "P-202603", "1075C142", "T-ST100");

        var candidates = await _query.FindCandidatesAsync(current);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.Machine == "B1");
        Assert.Contains(candidates, c => c.Machine == "B3");
    }

    [Fact]
    public async Task FindCandidates_SoAsUltimas5_EOPedidoMaisAntigoFica()
    {
        // Deep sweep 2 (18/10/2026, F-12): o dropdown tinha todas as
        // produções da referência. Ficam as últimas 5 das máquinas
        // permitidas; o Peso pedido (o «comparar») fica mesmo mais antigo,
        // mas só se for um candidato (a mesma referência, uma máquina da ficha).
        var b1 = await SeedMachineAsync("B1");
        var b3 = await SeedMachineAsync("B3");
        await SeedToolAsync("T-ST100", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T-ST100", MachineId = b1.Id });
        await _db.SaveChangesAsync();

        var controlos = new List<int>();
        foreach (var n in Enumerable.Range(1, 7))
        {
            controlos.Add(await SeedProductionWithPesoAsync(b1, $"P-20260{n}", "1075C142", "T-ST100"));
        }
        // Mais recentes, mas fora das máquinas permitidas e de outra
        // referência: não tiram lugar às 5.
        var naB3 = await SeedProductionWithPesoAsync(b3, "P-202608", "1075C142", "T-ST100");
        var outraReferencia = await SeedProductionWithPesoAsync(b1, "P-202609", "9999X001", "T-ST100");
        var current = await SeedProductionWithPesoAsync(b1, "P-202610", "1075C142", "T-ST100");
        int PesoDe(int controloId) => _db.Pesos.Single(p => p.ControloId == controloId).Id;

        var candidates = await _query.FindCandidatesAsync(current);
        Assert.Equal(
            new[] { "P-202607", "P-202606", "P-202605", "P-202604", "P-202603" },
            candidates.Select(c => c.ProductionNumber));
        // As médias continuam lidas (as leituras dos 5).
        Assert.All(candidates, c => Assert.Equal(1, c.ReadingCount));

        // O pedido mais antigo que as 5 fica, no fim.
        var antiga = await _query.FindCandidatesAsync(current, PesoDe(controlos[0]));
        Assert.Equal(
            new[] { "P-202607", "P-202606", "P-202605", "P-202604", "P-202603", "P-202601" },
            antiga.Select(c => c.ProductionNumber));

        // Um pedido que não é candidato não entra.
        foreach (var fora in new[] { naB3, outraReferencia, current })
        {
            Assert.Equal(5, (await _query.FindCandidatesAsync(current, PesoDe(fora))).Count);
        }
    }

    /// <summary>
    /// Uma referência com a CM T-ST100 a trabalhar na B1 (a ficha), para os
    /// testes das produções anteriores.
    /// </summary>
    private async Task<Machine> SeedB1ComFichaAsync()
    {
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-ST100", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T-ST100", MachineId = b1.Id });
        await _db.SaveChangesAsync();
        return b1;
    }

    [Fact]
    public async Task FindCandidates_SoAsAnteriores_APosteriorNaoAparece_EAPedidaPosteriorFicaNoFim()
    {
        // Deep sweep 2 (18/10/2026, F-12, a resposta R18): a Comparação_B
        // sai no PDF como «Comparação com o controlo anterior»; uma produção
        // posterior não pode aparecer. O critério é o da «Produção
        // anterior»: começou antes da atual ou, no mesmo dia, foi registada
        // antes (Job On com id menor). As sem início ficam de fora.
        var b1 = await SeedB1ComFichaAsync();
        var dia = new DateOnly(2026, 3, 10);
        await SeedProductionWithPesoAsync(b1, "P-ANTES", "1075C142", "T-ST100", inicioEm: dia.AddDays(-9));
        await SeedProductionWithPesoAsync(b1, "P-MESMO-DIA-ANTES", "1075C142", "T-ST100", inicioEm: dia);
        await SeedProductionWithPesoAsync(b1, "P-SEM-INICIO-ANTES", "1075C142", "T-ST100");
        var current = await SeedProductionWithPesoAsync(b1, "P-ATUAL", "1075C142", "T-ST100", inicioEm: dia);
        var mesmoDiaDepois = await SeedProductionWithPesoAsync(b1, "P-MESMO-DIA-DEPOIS", "1075C142", "T-ST100", inicioEm: dia);
        var depois = await SeedProductionWithPesoAsync(b1, "P-DEPOIS", "1075C142", "T-ST100", inicioEm: dia.AddDays(20));
        // Mais antiga pelo início, mas registada depois da atual: conta o início.
        await SeedProductionWithPesoAsync(b1, "P-ANTES-REGISTADA-DEPOIS", "1075C142", "T-ST100", inicioEm: dia.AddDays(-30));
        await SeedProductionWithPesoAsync(b1, "P-SEM-INICIO-DEPOIS", "1075C142", "T-ST100");

        var candidates = await _query.FindCandidatesAsync(current);
        Assert.Equal(
            new[] { "P-MESMO-DIA-ANTES", "P-ANTES", "P-ANTES-REGISTADA-DEPOIS" },
            candidates.Select(c => c.ProductionNumber));

        // A pedida posterior (uma comparação já guardada) fica, no fim.
        foreach (var (controlo, numero) in new[] { (depois, "P-DEPOIS"), (mesmoDiaDepois, "P-MESMO-DIA-DEPOIS") })
        {
            Assert.Equal(
                new[] { "P-MESMO-DIA-ANTES", "P-ANTES", "P-ANTES-REGISTADA-DEPOIS", numero },
                (await _query.FindCandidatesAsync(current, await PesoDoControloAsync(controlo))).Select(c => c.ProductionNumber));
        }
    }

    [Fact]
    public async Task FindCandidates_SemInicioNaAtual_SoAsRegistadasAntes_AsSemInicioNoFim()
    {
        // Sem início na produção atual, as anteriores são as registadas antes
        // dela (Job On com id menor): primeiro as que têm início, a mais
        // recente primeiro, depois as sem início.
        var b1 = await SeedB1ComFichaAsync();
        await SeedProductionWithPesoAsync(b1, "P-SEM-1", "1075C142", "T-ST100");
        await SeedProductionWithPesoAsync(b1, "P-2026-01", "1075C142", "T-ST100", inicioEm: new DateOnly(2026, 1, 5));
        await SeedProductionWithPesoAsync(b1, "P-SEM-2", "1075C142", "T-ST100");
        await SeedProductionWithPesoAsync(b1, "P-2026-02", "1075C142", "T-ST100", inicioEm: new DateOnly(2026, 2, 5));
        var current = await SeedProductionWithPesoAsync(b1, "P-ATUAL", "1075C142", "T-ST100");
        var depois = await SeedProductionWithPesoAsync(b1, "P-DEPOIS", "1075C142", "T-ST100", inicioEm: new DateOnly(2025, 12, 1));
        await SeedProductionWithPesoAsync(b1, "P-SEM-DEPOIS", "1075C142", "T-ST100");

        var candidates = await _query.FindCandidatesAsync(current);
        Assert.Equal(
            new[] { "P-2026-02", "P-2026-01", "P-SEM-2", "P-SEM-1" },
            candidates.Select(c => c.ProductionNumber));

        Assert.Equal(
            new[] { "P-2026-02", "P-2026-01", "P-SEM-2", "P-SEM-1", "P-DEPOIS" },
            (await _query.FindCandidatesAsync(current, await PesoDoControloAsync(depois))).Select(c => c.ProductionNumber));
    }

    [Fact]
    public async Task FindCandidates_NuncaMaisDe5Anteriores_MaisAPedida()
    {
        // As últimas 5 anteriores, e a pedida (mais antiga ou posterior) a
        // mais, no fim; uma pedida que já é uma das 5 fica no seu lugar.
        var b1 = await SeedB1ComFichaAsync();
        var controlos = new List<int>();
        foreach (var n in Enumerable.Range(1, 7))
        {
            controlos.Add(await SeedProductionWithPesoAsync(b1, $"P-20260{n}", "1075C142", "T-ST100", inicioEm: new DateOnly(2026, n, 1)));
        }
        var current = await SeedProductionWithPesoAsync(b1, "P-202608", "1075C142", "T-ST100", inicioEm: new DateOnly(2026, 8, 1));
        var posteriores = new List<int>();
        foreach (var n in Enumerable.Range(9, 3))
        {
            posteriores.Add(await SeedProductionWithPesoAsync(b1, $"P-2026{n:00}", "1075C142", "T-ST100", inicioEm: new DateOnly(2026, n, 1)));
        }

        var ultimas = new[] { "P-202607", "P-202606", "P-202605", "P-202604", "P-202603" };
        Assert.Equal(ultimas, (await _query.FindCandidatesAsync(current)).Select(c => c.ProductionNumber));
        Assert.Equal(
            ultimas.Append("P-202601"),
            (await _query.FindCandidatesAsync(current, await PesoDoControloAsync(controlos[0]))).Select(c => c.ProductionNumber));
        Assert.Equal(
            ultimas.Append("P-202610"),
            (await _query.FindCandidatesAsync(current, await PesoDoControloAsync(posteriores[1]))).Select(c => c.ProductionNumber));
        Assert.Equal(
            ultimas,
            (await _query.FindCandidatesAsync(current, await PesoDoControloAsync(controlos[4]))).Select(c => c.ProductionNumber));
    }

    /// <summary>
    /// Um Peso vazio (sem leituras) da MESMA combinação (CM + BQ) de uma
    /// produção já seedada — simula uma pesagem reaberta/criada depois, sem
    /// leituras ainda (PESO-B05).
    /// </summary>
    private async Task<int> SeedPesoVazioAsync(int controloId, string cmToolId, string bqToolId)
    {
        var peso = new Peso
        {
            CmToolId = cmToolId,
            BqToolId = bqToolId,
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.5m,
            ControloId = controloId
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        return peso.Id;
    }

    [Fact]
    public async Task FindCandidates_PesoVazioCriadoDepois_NaoEscondeAProducaoComLeituras()
    {
        // PESO-B05 (sweep 2): o corte das últimas 5 contava Pesos, não
        // produções distintas com leituras — um Peso vazio da MESMA
        // combinação (p.ex. uma pesagem reaberta) criado depois do que já
        // tinha leituras empurrava a leitura real para fora das 5, mesmo
        // havendo só 5 produções no total (nenhuma devia ficar de fora, e a
        // que sobrava mostrava 0 leituras em vez das reais).
        var b1 = await SeedB1ComFichaAsync();
        var maisAntiga = await SeedProductionWithPesoAsync(
            b1, "P-202601", "1075C142", "T-ST100", waterWeight: 199m, inicioEm: new DateOnly(2026, 1, 1));
        // O Peso vazio, da mesma combinação, criado DEPOIS (Id maior).
        await SeedPesoVazioAsync(maisAntiga, "T-ST100", "BQ-1");
        foreach (var mes in Enumerable.Range(2, 4))
        {
            await SeedProductionWithPesoAsync(b1, $"P-20260{mes}", "1075C142", "T-ST100", inicioEm: new DateOnly(2026, mes, 1));
        }
        var current = await SeedProductionWithPesoAsync(b1, "P-202606", "1075C142", "T-ST100", inicioEm: new DateOnly(2026, 6, 1));

        var candidates = await _query.FindCandidatesAsync(current);

        // As 5 produções continuam todas (nenhuma perdida pelo Peso vazio).
        Assert.Equal(5, candidates.Count);
        var daMaisAntiga = Assert.Single(candidates, c => c.ProductionNumber == "P-202601");
        // A entrada é a da leitura REAL, nunca a do Peso vazio criado depois.
        Assert.Equal(1, daMaisAntiga.ReadingCount);
        Assert.NotNull(daMaisAntiga.AverageWaterWeightGrams);
    }

    /// <summary>Outro Peso da mesma produção (outra combinação, a BQ-2), com uma leitura.</summary>
    private async Task<int> SeedOutroPesoAsync(int controloId)
    {
        var peso = new Peso
        {
            CmToolId = "T-ST100",
            BqToolId = "BQ-2",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.5m,
            ControloId = controloId
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "1", WaterWeightGrams = 201m });
        await _db.SaveChangesAsync();
        return peso.Id;
    }

    [Fact]
    public async Task FindCandidates_VariosPesosPorProducao_OsDaAtualFicamDeFora()
    {
        // Depois de uma troca da CM ou da BQ (beta.243) uma produção tem um
        // Peso por combinação. Os da produção atual nunca entram, nem
        // pedidos; os de uma anterior entram todos (o mais recente
        // primeiro); os de uma posterior não (deep sweep 2, F-12).
        var b1 = await SeedB1ComFichaAsync();
        await SeedToolAsync("BQ-2", ToolType.Bq, "T194", "13");
        var anterior = await SeedProductionWithPesoAsync(b1, "P-ANTERIOR", "1075C142", "T-ST100");
        var primeiroAnterior = await _db.Pesos.Where(p => p.ControloId == anterior).MinAsync(p => p.Id);
        var segundoAnterior = await SeedOutroPesoAsync(anterior);
        var current = await SeedProductionWithPesoAsync(b1, "P-ATUAL", "1075C142", "T-ST100");
        var primeiroAtual = await _db.Pesos.Where(p => p.ControloId == current).MinAsync(p => p.Id);
        var segundoAtual = await SeedOutroPesoAsync(current);
        var posterior = await SeedProductionWithPesoAsync(b1, "P-POSTERIOR", "1075C142", "T-ST100");
        await SeedOutroPesoAsync(posterior);
        var pesoAtual = (await new PesoQuery(_db).FindPesoAsync(segundoAtual))!;

        foreach (var candidates in new[]
                 {
                     await _query.FindCandidatesAsync(current),
                     await _query.FindCandidatesAsync(pesoAtual),
                     await _query.FindCandidatesAsync(current, primeiroAtual),
                     await _query.FindCandidatesAsync(pesoAtual, primeiroAtual)
                 })
        {
            Assert.Equal(new[] { segundoAnterior, primeiroAnterior }, candidates.Select(c => c.PesoId));
        }
    }

    [Fact]
    public async Task FindCandidates_ProductionWithoutCmContext_AllMachinesOfTheReference()
    {
        // Sem CM no contexto do Job On (o slot não aplicável): nenhuma
        // ficha decide — o conjunto completo continua.
        var b1 = await SeedMachineAsync("B1");
        var b3 = await SeedMachineAsync("B3");
        await SeedToolAsync("T-ST100", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T-ST100", MachineId = b1.Id });
        await _db.SaveChangesAsync();

        await SeedProductionWithPesoAsync(b1, "P-202601", "1075C142", "T-ST100");
        await SeedProductionWithPesoAsync(b3, "P-202602", "1075C142", "T-ST100");
        var current = await SeedProductionWithPesoAsync(b1, "P-202603", "1075C142", cmToolId: null);

        var candidates = await _query.FindCandidatesAsync(current);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.Machine == "B3");
    }

    [Fact]
    public async Task CompararPesosGuardados_AReferenciaPeloInicio_NuncaAMeio()
    {
        // «Comparar Pesos guardados» (deep sweep 2): a referência da produção
        // ou da CM pelo início dela ou da parte BQ (ReferenceMatch), nunca a
        // meio: «075» trazia a 1075C142 e «447» a CM 5447T173. O nº, a
        // máquina e o lote continuam por parte do texto.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-5447", ToolType.Cm, "5447T173", "8");
        await SeedToolAsync("T-3540", ToolType.Cm, "3540Y079", "4");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        var daReferencia = await PesoDoControloAsync(await SeedProductionWithPesoAsync(b1, "202601", "1075C142", "T-5447"));
        var daOutra = await PesoDoControloAsync(await SeedProductionWithPesoAsync(b1, "202699", "9999X001", "T-5447"));
        var semProducao = new Peso
        {
            CmToolId = "T-3540", BqToolId = "BQ-1", Process = "NNPB", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.5m
        };
        _db.Pesos.Add(semProducao);
        await _db.SaveChangesAsync();

        async Task<int[]> ListarAsync(string referencia) =>
            (await _query.ListarPesosPorReferenciaAsync(referencia)).Select(o => o.PesoId).ToArray();
        async Task<int[]> ProcurarAsync(string texto, string? referencia = null) =>
            (await _query.ProcurarPesosAsync(texto, referencia, 10)).Select(o => o.PesoId).ToArray();

        // O segundo passo pela referência: a da produção ou, sem ela, a da CM.
        foreach (var referencia in new[] { "1075", "c14", " 1075c142 " })
        {
            Assert.Equal(new[] { daReferencia }, await ListarAsync(referencia));
        }
        Assert.Empty(await ListarAsync("075"));
        Assert.Empty(await ListarAsync("42"));
        Assert.Equal(new[] { semProducao.Id }, await ListarAsync("3540"));
        Assert.Equal(new[] { semProducao.Id }, await ListarAsync("y07"));
        Assert.Empty(await ListarAsync("540"));
        // A CM de um Peso com produção não conta para a referência dele.
        Assert.Empty(await ListarAsync("5447"));

        // A procura ao escrever: as referências pelo início.
        Assert.Equal(new[] { daReferencia }, await ProcurarAsync("1075"));
        Assert.Equal(new[] { daReferencia }, await ProcurarAsync("c14"));
        Assert.Empty(await ProcurarAsync("075"));
        Assert.Equal(new[] { daOutra, daReferencia }, await ProcurarAsync("5447"));
        Assert.Equal(new[] { daOutra, daReferencia }, await ProcurarAsync("t17"));
        Assert.Empty(await ProcurarAsync("447"));
        Assert.Equal(new[] { semProducao.Id }, await ProcurarAsync("354"));
        Assert.Empty(await ProcurarAsync("540"));
        // O nº, a máquina e o lote por parte do texto.
        Assert.Equal(new[] { daOutra }, await ProcurarAsync("0269"));
        Assert.Equal(new[] { daOutra, daReferencia }, await ProcurarAsync("b1"));
        Assert.Equal(new[] { semProducao.Id }, await ProcurarAsync("4"));

        // Com a referência indicada, pelo início dela.
        Assert.Equal(new[] { daReferencia }, await ProcurarAsync("2026", "1075"));
        Assert.Equal(new[] { daReferencia }, await ProcurarAsync("2026", "c14"));
        Assert.Empty(await ProcurarAsync("2026", "075"));
        Assert.Equal(new[] { semProducao.Id }, await ProcurarAsync("y07", "3540"));
        Assert.Empty(await ProcurarAsync("y07", "540"));
    }

    private async Task<int> PesoDoControloAsync(int controloId) =>
        (await _db.Pesos.SingleAsync(p => p.ControloId == controloId)).Id;

    [Fact]
    public async Task FindCandidates_DoPesoAnterior_UsaAsMaquinasDaCmDele()
    {
        // Depois de trocar a CM a meio da produção (18/10/2026), o Peso
        // anterior compara-se com as máquinas da CM dele; o atual, com as
        // da CM nova.
        var b1 = await SeedMachineAsync("B1");
        var b3 = await SeedMachineAsync("B3");
        await SeedToolAsync("T-OLD", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("T-NEW", ToolType.Cm, "ST100", "9");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = "T-OLD", MachineId = b1.Id },
            new ToolMachineLink { ToolId = "T-OLD", MachineId = b3.Id },
            new ToolMachineLink { ToolId = "T-NEW", MachineId = b1.Id });
        await _db.SaveChangesAsync();
        await SeedProductionWithPesoAsync(b1, "P-202601", "1075C142", "T-OLD");
        await SeedProductionWithPesoAsync(b3, "P-202602", "1075C142", "T-OLD");
        var current = await SeedProductionWithPesoAsync(b1, "P-202603", "1075C142", "T-OLD");
        var jobOnId = (await _db.Controlos.SingleAsync(c => c.Id == current)).JobOnId;
        var context = await _db.CmContexts.SingleAsync(c => c.JobOnId == jobOnId);
        context.JobOnId = null;
        context.ReplacedJobOnId = jobOnId;
        _db.CmContexts.Add(new CmContext { JobOnId = jobOnId, ToolId = "T-NEW" });
        await _db.SaveChangesAsync();
        var anterior = (await new PesoQuery(_db).FindByControloAsync(current))!;

        var daProducao = await _query.FindCandidatesAsync(current);
        var doPesoAnterior = await _query.FindCandidatesAsync(anterior);

        Assert.Equal(new[] { "B1" }, daProducao.Select(c => c.Machine).ToArray());
        Assert.Equal(new[] { "B1", "B3" }, doPesoAnterior.Select(c => c.Machine).OrderBy(m => m).ToArray());
        Assert.False(anterior.IsCurrent);
    }

    [Fact]
    public async Task ListarIniciais_DoisPesos_MarcaOAtualEDizABq()
    {
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-ST100", ToolType.Cm, "ST100", "8");
        await SeedToolAsync("BQ-1", ToolType.Bq, "T194", "12");
        await SeedToolAsync("BQ-2", ToolType.Bq, "T194", "13");
        var controloId = await SeedProductionWithPesoAsync(b1, "P-202601", "1075C142", "T-ST100");
        var controlo = await _db.Controlos.SingleAsync(c => c.Id == controloId);
        _db.BqContexts.Add(new BqContext { ReplacedJobOnId = controlo.JobOnId, ToolId = "BQ-1" });
        _db.BqContexts.Add(new BqContext { JobOnId = controlo.JobOnId, ToolId = "BQ-2" });
        _db.Pesos.Add(new Peso
        {
            CmToolId = "T-ST100",
            BqToolId = "BQ-2",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.5m,
            ControloId = controloId
        });
        await _db.SaveChangesAsync();

        var opcoes = await _query.ListarIniciaisAsync(controlo.JobOnId);

        Assert.Equal(2, opcoes.Count);
        Assert.Equal(new[] { false, true }, opcoes.Select(o => o.IsCurrent).ToArray());
        Assert.Equal(new[] { "BQ T194 · Lote 12", "BQ T194 · Lote 13" }, opcoes.Select(o => o.BqLabel).ToArray());
    }
}
