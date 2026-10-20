using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova o write path da criação de um Peso tool-scoped: criação com sucesso
/// (âncoras canónicas às Tools reais, valores consumidos congelados como
/// evidência histórica, temperatura arredondada ao grau inteiro), a
/// completion dos valores técnicos Tool-owned EM FALTA (grava no tool_id
/// owner, nunca como facto do Peso; valores já registados nunca são
/// sobrescritos) e as recusas tipadas (tools não registadas, valores
/// estruturais em falta, token de processo desconhecido, densidade do vidro
/// não configurada, temperatura fora do intervalo). Nenhuma identidade de
/// produção é criada nem exigida.
/// </summary>
public sealed class CreateToolScopedPesoHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateToolScopedPesoHandler _handler;

    public CreateToolScopedPesoHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new CreateToolScopedPesoHandler(_db, new GlassDensityConfiguration(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Tool> SeedToolAsync(string toolId, ToolType type, string reference, string lot)
    {
        var tool = new Tool { ToolId = toolId, Type = type, Reference = reference, Lot = lot };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    private async Task<ToolTechnicalContext> SeedTechnicalAsync(
        string toolId,
        CmProcess? process = null,
        decimal? volumePuncao = null,
        decimal? nominal = null,
        decimal? volumeMarisa = null)
    {
        var technical = new ToolTechnicalContext
        {
            ToolId = toolId,
            Process = process,
            VolumePuncaoCm3 = volumePuncao,
            NominalWeightGrams = nominal,
            VolumeMarisaCm3 = volumeMarisa
        };
        _db.ToolTechnicalContexts.Add(technical);
        await _db.SaveChangesAsync();
        return technical;
    }

    private async Task SeedGlassDensityAsync(CmProcess process, decimal density)
    {
        _db.GlassDensities.Add(new GlassDensity
        {
            Process = process,
            DensityGramsPerCm3 = density
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_CongezesConsumedInputsAndAnchorsTools()
    {
        // Ferramentas reais: CM T3 (5447, lote 3) e a BQ da pesagem, com o
        // contexto técnico já registado — o cenário mais simples.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m, nominal: 200m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20.4m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        Assert.NotNull(outcome.Peso);

        // A temperatura medida 20.4 foi arredondada ao grau 20 para o
        // lookup; o grau inteiro usado é o persistido. A densidade resolvida
        // é a linha exata da tabela real da fábrica (decisão Owner 1b).
        Assert.Equal(20, outcome.Peso!.WaterTemperatureC);
        Assert.Equal(0.99717m, outcome.Peso.WaterDensityGramsPerCm3);
        Assert.Equal("NNPB", outcome.Peso.Process);
        Assert.Equal(2.50m, outcome.Peso.GlassDensityGramsPerCm3);
        Assert.Equal(10m, outcome.Peso.VolumeMarisaCm3);
        Assert.Equal(30m, outcome.Peso.VolumePuncaoCm3);
        Assert.Equal(200m, outcome.Peso.NominalWeightGrams);

        // O Peso persistido congela exatamente os inputs consumidos.
        var persisted = await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == outcome.Peso.PesoId);
        Assert.Equal("T3", persisted.CmToolId);
        Assert.Equal("BQ-TOOL-9", persisted.BqToolId);
        Assert.Equal("NNPB", persisted.Process);
        Assert.Equal(20, persisted.WaterTemperatureC);
        Assert.Equal(10m, persisted.VolumeMarisaCm3);
        Assert.Equal(30m, persisted.VolumePuncaoCm3);
        Assert.Equal(200m, persisted.NominalWeightGrams);
        Assert.Equal(2.50m, persisted.GlassDensityGramsPerCm3);

        // Nenhuma identidade de produção é criada ou exigida: o Peso
        // tool-scoped existe sem Job On, controlo ou contexto CM.
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_MissingToolValues_CompletedOnOwningToolId()
    {
        // A regra Ferramentas aplicada pelo Peso: valores Tool-owned EM
        // FALTA coletados no fluxo são gravados no tool_id owner (contexto
        // técnico da própria Tool), nunca como facto privado do Peso.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        // T3 tem apenas o processo registado; falta volume_puncao e nominal.
        await SeedTechnicalAsync("T3", CmProcess.Nnpb);
        // A BQ ainda não tem linha de contexto técnico (volume_marisa em falta).
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3",
            "BQ-TOOL-9",
            20m,
            CmVolumePuncaoCm3: 30m,
            CmNominalWeightGrams: 200m,
            BqVolumeMarisaCm3: 10m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);

        // Os valores completados ficam no tool_id owner e reutilizáveis por
        // futuros Pesos — não é criada nenhuma cópia privada do Peso.
        var cmTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "T3");
        Assert.Equal(CmProcess.Nnpb, cmTechnical.Process);
        Assert.Equal(30m, cmTechnical.VolumePuncaoCm3);
        Assert.Equal(200m, cmTechnical.NominalWeightGrams);

        var bqTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "BQ-TOOL-9");
        Assert.Equal(10m, bqTechnical.VolumeMarisaCm3);

        // O Peso consumiu exatamente os valores completados.
        Assert.Equal(30m, outcome.Peso!.VolumePuncaoCm3);
        Assert.Equal(10m, outcome.Peso.VolumeMarisaCm3);
        Assert.Equal(200m, outcome.Peso.NominalWeightGrams);
    }

    [Fact]
    public async Task Create_RegistadosValuesAreNeverOverwrittenByCompletion()
    {
        // Completion, não correção silenciosa: um valor que a Tool já tem
        // registado não é substituído por um valor fornecido no fluxo do
        // Peso. O Peso consome o valor registado.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 70m, nominal: 200m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10.10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3",
            "BQ-TOOL-9",
            20m,
            CmProcessToken: "PS",
            CmVolumePuncaoCm3: 30m,
            CmNominalWeightGrams: 999m,
            BqVolumeMarisaCm3: 99m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);

        // Os valores registados permanecem; o Peso consumiu-os.
        var cmTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "T3");
        Assert.Equal(CmProcess.Nnpb, cmTechnical.Process);
        Assert.Equal(70m, cmTechnical.VolumePuncaoCm3);
        Assert.Equal(200m, cmTechnical.NominalWeightGrams);

        var bqTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "BQ-TOOL-9");
        Assert.Equal(10.10m, bqTechnical.VolumeMarisaCm3);

        Assert.Equal(70m, outcome.Peso!.VolumePuncaoCm3);
        Assert.Equal(10.10m, outcome.Peso.VolumeMarisaCm3);
        Assert.Equal(200m, outcome.Peso.NominalWeightGrams);
        Assert.Equal("NNPB", outcome.Peso.Process);
    }

    [Fact]
    public async Task Create_SameTool_ManyPesosOverTime()
    {
        // Cada pesagem real é um peso_id novo: a mesma Tool pode ter vários
        // Pesos ao longo do tempo sem unicidade inventada sobre
        // (CmToolId, BqToolId).
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var first = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ-TOOL-9", 20m));
        var second = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ-TOOL-9", 21m));

        Assert.Equal(CreateToolScopedPesoResult.Success, first.Result);
        Assert.Equal(CreateToolScopedPesoResult.Success, second.Result);
        Assert.NotEqual(first.Peso!.PesoId, second.Peso!.PesoId);
        Assert.Equal(2, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_UsesGlassDensityConfiguredForConsumedProcess()
    {
        // A densidade do vidro é a configurada para o processo CONSUMIDO da
        // CM (não para outro processo configurado).
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Ps, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);
        await SeedGlassDensityAsync(CmProcess.Ps, 2.52m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        Assert.Equal("PS", outcome.Peso!.Process);
        Assert.Equal(2.52m, outcome.Peso.GlassDensityGramsPerCm3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyCmToolId_RefusedCmToolNotFound(string? cmToolId)
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            cmToolId!, "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.CmToolNotFound, outcome.Result);
        Assert.Null(outcome.Peso);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_UnknownCmTool_Refused()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.CmToolNotFound, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_UnknownBqTool_Refused()
    {
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.BqToolNotFound, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_MissingCmProcess_AfterCompletionAttempt_Refused()
    {
        // Nem o contexto técnico da Tool tem processo registado nem o
        // comando o forneceu: o cálculo principal não pode resolver a
        // densidade do vidro.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.MissingCmProcess, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("nnpb")]
    [InlineData("NNPB ")]
    [InlineData("ps")]
    [InlineData("blow")]
    public async Task Create_UnknownProcessToken_Refused(string token)
    {
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m, CmProcessToken: token));

        Assert.Equal(CreateToolScopedPesoResult.UnknownProcessToken, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_MissingCmVolumePuncao_Refused()
    {
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.MissingCmVolumePuncao, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_MissingBqVolumeMarisa_Refused()
    {
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.MissingBqVolumeMarisa, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_GlassDensityNotConfiguredForProcess_Refused()
    {
        // A configuração do Controlo tem o processo PS mas não o NNPB
        // consumido pela T3: nenhum valor de densidade é inventado.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Ps, 2.52m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.GlassDensityNotConfigured, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(4.4)]   // arredonda a 4: fora do intervalo suportado
    [InlineData(-2.0)]
    [InlineData(35.5)]  // arredonda a 36: fora do intervalo suportado
    [InlineData(100.0)]
    public async Task Create_WaterTemperatureOutsideSupportedRange_Refused(decimal temperature)
    {
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", temperature));

        Assert.Equal(CreateToolScopedPesoResult.InvalidWaterTemperature, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(9.9E+19)]    // O P1 do stresstest: o overflow
    [InlineData(-9.9E+19)]   // rebentava o cast a int (500)
    [InlineData(1E+28)]
    public async Task Create_WaterTemperatureAbsurdOverflow_RefusedTyped_Never500(double overflow)
    {
        var temperature = (decimal)overflow;
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", temperature));

        Assert.Equal(CreateToolScopedPesoResult.InvalidWaterTemperature, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(4.9)]    // arredonda a 5: DENTRO (a margem 0,5 da guarda)
    [InlineData(5.0)]
    [InlineData(20.4)]   // a vírgula da oficina, no ponto do teste
    [InlineData(35.4)]
    public async Task Create_WaterTemperatureRoundsInToSupportedRange_Accepted(decimal temperature)
    {
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", temperature));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        Assert.Equal(1, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(-100)]    // O P2 do stresstest: os volumes negativos
    [InlineData(0)]       // passavam calados e eram até gravados
    public async Task Create_VolumesOrNominalNonPositive_RefusedTyped(decimal garbage)
    {
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20.5m,
            CmVolumePuncaoCm3: garbage,
            BqVolumeMarisaCm3: garbage,
            CmNominalWeightGrams: garbage));

        Assert.Equal(CreateToolScopedPesoResult.InvalidTechnicalValue, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("puncao", 0)]       // no limite
    [InlineData("puncao", -0.01)]   // logo abaixo
    [InlineData("marisa", 0)]
    [InlineData("marisa", -0.01)]
    [InlineData("nominal", 0)]
    [InlineData("nominal", -0.01)]
    public async Task UmVolumeOuONominalZeroOuNegativoSozinho_ERecusado_NadaFicaGravado(string campo, decimal invalido)
    {
        // Cada campo inválido sozinho, com os outros válidos: as três guardas
        // valem cada uma por si. As ferramentas ainda não têm valores
        // técnicos, por isso aceitar o valor gravá-lo-ia na ficha da Tool.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(EnvioComUmValor(campo, invalido));

        Assert.Equal(CreateToolScopedPesoResult.InvalidTechnicalValue, outcome.Result);
        Assert.Null(outcome.Peso);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("puncao")]
    [InlineData("marisa")]
    [InlineData("nominal")]
    public async Task UmVolumeOuONominalDe001_EAceite_FicaNoPesoENaFerramenta(string campo)
    {
        // Logo acima do limite: 0,01 é positivo e passa.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(EnvioComUmValor(campo, 0.01m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        var peso = await _db.Pesos.AsNoTracking().SingleAsync();
        var cm = await _db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "T3");
        var bq = await _db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "BQ-TOOL-9");
        Assert.Equal(campo == "puncao" ? 0.01m : 30m, peso.VolumePuncaoCm3);
        Assert.Equal(campo == "puncao" ? 0.01m : 30m, cm.VolumePuncaoCm3);
        Assert.Equal(campo == "marisa" ? 0.01m : 10m, peso.VolumeMarisaCm3);
        Assert.Equal(campo == "marisa" ? 0.01m : 10m, bq.VolumeMarisaCm3);
        Assert.Equal(campo == "nominal" ? 0.01m : 200m, peso.NominalWeightGrams);
        Assert.Equal(campo == "nominal" ? 0.01m : 200m, cm.NominalWeightGrams);
    }

    // O envio completo e válido (NNPB, punção 30, nominal 200, marisa 10)
    // com um só campo trocado.
    private static CreateToolScopedPeso EnvioComUmValor(string campo, decimal valor) => new(
        "T3",
        "BQ-TOOL-9",
        20m,
        CmProcessToken: "NNPB",
        CmVolumePuncaoCm3: campo == "puncao" ? valor : 30m,
        CmNominalWeightGrams: campo == "nominal" ? valor : 200m,
        BqVolumeMarisaCm3: campo == "marisa" ? valor : 10m);

    [Fact]
    public async Task Create_RefusalLeavesNoPartialCompletionBehind()
    {
        // Todas as recusas são verificadas antes de qualquer escrita: uma
        // recusa (aqui: densidade não configurada) não deixa valores
        // técnicos completados nem Pesos parcialmente criados.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        // Sem GlassDensities configuradas.

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3",
            "BQ-TOOL-9",
            20m,
            CmProcessToken: "NNPB",
            CmVolumePuncaoCm3: 30m,
            BqVolumeMarisaCm3: 10m));

        Assert.Equal(CreateToolScopedPesoResult.GlassDensityNotConfigured, outcome.Result);
        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_NominalWeightIsOptional()
    {
        // O peso nominal é contexto consumido OPCIONAL: a ausência não
        // bloqueia a criação e o Peso preserva o null.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso(
            "T3", "BQ-TOOL-9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        Assert.Null(outcome.Peso!.NominalWeightGrams);

        var persisted = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Null(persisted.NominalWeightGrams);
    }

    [Fact]
    public async Task RepeatedClicks_OpenTheSameEmptyPeso_NewOneOnlyAfterReadings()
    {
        // Varredura de 18/10/2026, B5: o mesmo envio repetido abre o Peso
        // vazio que o primeiro criou, sem mostrar nada.
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m, nominal: 200m);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m);
        await SeedGlassDensityAsync(CmProcess.Nnpb, 2.50m);

        var first = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ-TOOL-9", 20m));
        var again = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ-TOOL-9", 20m));
        Assert.Equal(CreateToolScopedPesoResult.Success, again.Result);
        Assert.Equal(first.Peso!.PesoId, again.Peso!.PesoId);
        Assert.Equal(first.Peso, again.Peso);

        // Outra temperatura é outro Peso.
        var warmer = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ-TOOL-9", 25m));
        Assert.NotEqual(first.Peso.PesoId, warmer.Peso!.PesoId);

        // Com leituras gravadas, o mesmo envio começa um Peso novo.
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = first.Peso.PesoId, RowPosition = 1, CmSubject = "1", WaterWeightGrams = 300m });
        await _db.SaveChangesAsync();
        var next = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ-TOOL-9", 20m));
        Assert.NotEqual(first.Peso.PesoId, next.Peso!.PesoId);
        Assert.Equal(3, await _db.Pesos.CountAsync());
    }
}
