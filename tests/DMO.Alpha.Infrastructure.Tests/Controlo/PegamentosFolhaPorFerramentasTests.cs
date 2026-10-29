using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Uma folha de Pegamentos por combinação de ferramentas (
/// 18/10/2026). Antes: depois de trocar a CM, a BQ ou o MF a meio da
/// produção, as leituras novas iam para a folha das ferramentas antigas,
/// medidas contra a ferramenta nova mas mostradas com a antiga. Agora a
/// primeira leitura depois da troca começa uma folha nova com as
/// ferramentas novas e a tolerância corrente; a anterior fica como estava
/// (sem leituras novas, mas as dela ainda se corrigem e apagam); voltar a
/// uma combinação já usada retoma a folha dela; um MF acrescentado depois
/// também começa uma folha.
/// </summary>
public sealed class PegamentosFolhaPorFerramentasTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly RecordPegamentosMeasurementHandler _handler;
    private readonly PegamentosQuery _query;

    public PegamentosFolhaPorFerramentasTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new RecordPegamentosMeasurementHandler(_db, new PegamentosToleranceConfiguration(_db));
        _query = new PegamentosQuery(_db, new ProductionContextQuery(_db), new PegamentosToleranceConfiguration(_db));
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Uma produção com a CM-A, a BQ-A e o MF-A (com MF) ou só a CM-A e a
    /// BQ-A (a produção antiga sem MF); as ferramentas B ficam no registo
    /// para as trocas. Todas têm gargalo.
    /// </summary>
    private async Task<(int ControloId, int JobOnId, int MachineId)> SeedAsync(bool withMf = true)
    {
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-A", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "CM-B", Type = ToolType.Cm, Reference = "5447", Lot = "2" },
            new Tool { ToolId = "BQ-A", Type = ToolType.Bq, Reference = "T194", Lot = "12" },
            new Tool { ToolId = "BQ-B", Type = ToolType.Bq, Reference = "T195", Lot = "3" },
            new Tool { ToolId = "MF-A", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = "MF-B", Type = ToolType.Mf, Reference = "5811", Lot = "4" });
        foreach (var (toolId, gargalo) in new[]
                 {
                     ("CM-A", 70.00m), ("CM-B", 70.50m), ("BQ-A", 30.00m),
                     ("BQ-B", 30.50m), ("MF-A", 60.00m), ("MF-B", 60.50m)
                 })
        {
            _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = toolId, DiametroGargaloMm = gargalo });
        }

        await _db.SaveChangesAsync();
        var machineId = (await _db.Machines.AsNoTracking().SingleAsync()).Id;
        if (withMf)
        {
            var created = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
                "PN-1", "5810", machineId, "CM-A", "MF-A", "BQ-A"));
            Assert.Equal(CreateJobOnResult.Success, created.Result);
        }
        else
        {
            // A produção antiga sem MF (antes da regra de 10/10/2026).
            var jobOn = new JobOn { Reference = "5810", ProductionNumber = "PN-1", MachineId = machineId };
            _db.JobOns.Add(jobOn);
            _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-A" });
            _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-A" });
            _db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn });
            await _db.SaveChangesAsync();
        }

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        return (controlo.Id, controlo.JobOnId, machineId);
    }

    /// <summary>A troca no Editar do Job On (a mesma porta da ficha).</summary>
    private async Task ChangeToolsAsync(int jobOnId, int machineId, string cm, string mf, string bq)
    {
        var outcome = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(jobOnId, machineId, cm, mf, bq));
        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        _db.ChangeTracker.Clear();
    }

    /// <summary>
    /// A hora de gravação de cada folha (na app, o carimbo do registo; aqui
    /// a base em memória não o põe).
    /// </summary>
    private async Task StampSheetsAsync()
    {
        foreach (var folha in await _db.Pegamentos.ToListAsync())
        {
            folha.RegistadoEmUtc ??= new DateTime(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);
        }

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<RecordedPegamentosMeasurement> RecordAsync(
        int controloId, string component, decimal costura, int? pegamentosId = null)
    {
        var outcome = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, component, costura, PegamentosId: pegamentosId));
        Assert.Equal(RecordPegamentosMeasurementResult.Success, outcome.Result);
        return outcome.Measurement!;
    }

    [Theory]
    [InlineData("CM")]
    [InlineData("BQ")]
    [InlineData("MF")]
    public async Task ToolChangedMidProduction_FirstReadingStartsANewSheet_TheOldOneStaysAsItWas(string changed)
    {
        var (controloId, jobOnId, machineId) = await SeedAsync();
        var first = await RecordAsync(controloId, "CM", 70.05m);
        Assert.False(first.StartedNewSheet);

        await ChangeToolsAsync(jobOnId, machineId,
            changed == "CM" ? "CM-B" : "CM-A", changed == "MF" ? "MF-B" : "MF-A", changed == "BQ" ? "BQ-B" : "BQ-A");

        var after = await RecordAsync(controloId, changed, changed switch { "CM" => 70.55m, "BQ" => 30.55m, _ => 60.55m });
        Assert.True(after.StartedNewSheet);
        Assert.NotEqual(first.PegamentosId, after.PegamentosId);
        // Medida contra o nominal da ferramenta nova, na posição 1 da folha nova.
        Assert.Equal(changed switch { "CM" => 70.50m, "BQ" => 30.50m, _ => 60.50m }, after.NominalGargaloMm);
        Assert.Equal(1, after.RowPosition);

        // A leitura seguinte, de outro componente, fica na mesma folha nova.
        var next = await RecordAsync(controloId, changed == "CM" ? "BQ" : "CM", changed == "CM" ? 30.00m : 70.00m);
        Assert.False(next.StartedNewSheet);
        Assert.Equal(after.PegamentosId, next.PegamentosId);
        Assert.Equal(2, next.RowPosition);

        var old = await _db.Pegamentos.AsNoTracking().Include(p => p.Measurements).SingleAsync(p => p.Id == first.PegamentosId);
        Assert.Equal(("CM-A", "BQ-A", "MF-A"), (old.CmToolId, old.BqToolId, old.MfToolId));
        Assert.Equal(70.05m, Assert.Single(old.Measurements).CosturaMm);
        var nova = await _db.Pegamentos.AsNoTracking().SingleAsync(p => p.Id == after.PegamentosId);
        Assert.Equal(changed == "CM" ? "CM-B" : "CM-A", nova.CmToolId);
        Assert.Equal(changed == "BQ" ? "BQ-B" : "BQ-A", nova.BqToolId);
        Assert.Equal(changed == "MF" ? "MF-B" : "MF-A", nova.MfToolId);
        Assert.Equal(changed switch { "CM" => "5447", "BQ" => "T195", _ => "5811" }, changed switch
        {
            "CM" => nova.CmToolReferenceFrozen,
            "BQ" => nova.BqToolReferenceFrozen,
            _ => nova.MfToolReferenceFrozen
        });

        // A folha aberta é a das ferramentas atuais; a anterior continua na lista.
        var sheet = (await _query.FindSheetByControloAsync(controloId))!;
        Assert.Equal(after.PegamentosId, sheet.Pegamentos!.PegamentosId);
        Assert.Equal(new[] { first.PegamentosId, after.PegamentosId }, sheet.Sheets.Select(s => s.PegamentosId));
        Assert.Equal(new[] { false, true }, sheet.Sheets.Select(s => s.IsCurrent));
        Assert.Equal(2, sheet.SheetNumber(sheet.Pegamentos));
    }

    [Fact]
    public async Task BackToAnEarlierCombination_ReusesItsSheet_NoThirdSheet()
    {
        var (controloId, jobOnId, machineId) = await SeedAsync();
        var a = await RecordAsync(controloId, "CM", 70.00m);
        await ChangeToolsAsync(jobOnId, machineId, "CM-B", "MF-A", "BQ-A");
        var b = await RecordAsync(controloId, "CM", 70.50m);
        await ChangeToolsAsync(jobOnId, machineId, "CM-A", "MF-A", "BQ-A");

        var back = await RecordAsync(controloId, "CM", 70.10m);

        Assert.False(back.StartedNewSheet);
        Assert.Equal(a.PegamentosId, back.PegamentosId);
        Assert.Equal(2, back.RowPosition);
        Assert.Equal(2, await _db.Pegamentos.CountAsync());
        Assert.Single(await _db.PegamentosMeasurements.Where(m => m.PegamentosId == b.PegamentosId).ToListAsync());
        var sheet = (await _query.FindSheetByControloAsync(controloId))!;
        Assert.Equal(a.PegamentosId, sheet.Pegamentos!.PegamentosId);
        Assert.Equal(1, sheet.SheetNumber(sheet.Pegamentos));
    }

    [Fact]
    public async Task OldSheet_RefusesNewReadings_ButItsReadingsAreStillCorrectedAndDeleted()
    {
        var (controloId, jobOnId, machineId) = await SeedAsync();
        var first = await RecordAsync(controloId, "CM", 70.05m);
        // Enquanto as ferramentas são as da folha, gravar pelo pegamentos_id serve.
        var second = await RecordAsync(controloId, "CM", 70.10m, first.PegamentosId);
        Assert.Equal(first.PegamentosId, second.PegamentosId);
        await ChangeToolsAsync(jobOnId, machineId, "CM-B", "MF-A", "BQ-A");
        var nova = await RecordAsync(controloId, "CM", 70.55m);

        var refused = await _handler.HandleAsync(new RecordPegamentosMeasurement(
            controloId, "CM", 70.20m, PegamentosId: first.PegamentosId));

        Assert.Equal(RecordPegamentosMeasurementResult.PegamentosNotCurrent, refused.Result);
        Assert.Null(refused.Measurement);
        Assert.Equal(3, await _db.PegamentosMeasurements.CountAsync());

        var correct = new CorrectPegamentosMeasurementHandler(_db);
        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await correct.EditAsync(new EditPegamentosMeasurement(
            new PegamentosMeasurementAnchor(second.MeasurementId, controloId, first.PegamentosId), 70.15m, null)));
        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await correct.DeleteAsync(
            new PegamentosMeasurementAnchor(first.MeasurementId, controloId, first.PegamentosId)));
        _db.ChangeTracker.Clear();

        // Só a folha antiga é renumerada; a nova fica como estava.
        var oldRow = await _db.PegamentosMeasurements.AsNoTracking().SingleAsync(m => m.PegamentosId == first.PegamentosId);
        Assert.Equal((second.MeasurementId, 1, 70.15m), (oldRow.Id, oldRow.RowPosition, oldRow.CosturaMm));
        var newRow = await _db.PegamentosMeasurements.AsNoTracking().SingleAsync(m => m.PegamentosId == nova.PegamentosId);
        Assert.Equal((1, 70.55m), (newRow.RowPosition, newRow.CosturaMm));
    }

    [Fact]
    public async Task WithoutMf_ReadingsShareOneSheet_AndAnMfAddedLaterStartsANewSheet()
    {
        var (controloId, jobOnId, machineId) = await SeedAsync(withMf: false);
        var first = await RecordAsync(controloId, "CM", 70.00m);
        var second = await RecordAsync(controloId, "BQ", 30.00m);
        Assert.Equal(first.PegamentosId, second.PegamentosId);
        Assert.False(second.StartedNewSheet);
        Assert.Null((await _db.Pegamentos.AsNoTracking().SingleAsync()).MfToolId);

        await ChangeToolsAsync(jobOnId, machineId, "CM-A", "MF-A", "BQ-A");
        var mf = await RecordAsync(controloId, "MF", 60.00m);
        var cm = await RecordAsync(controloId, "CM", 70.05m);

        Assert.True(mf.StartedNewSheet);
        Assert.NotEqual(first.PegamentosId, mf.PegamentosId);
        Assert.Equal(mf.PegamentosId, cm.PegamentosId);
        var nova = await _db.Pegamentos.AsNoTracking().SingleAsync(p => p.Id == mf.PegamentosId);
        Assert.Equal(("CM-A", "BQ-A", "MF-A"), (nova.CmToolId, nova.BqToolId, nova.MfToolId));
    }

    [Fact]
    public async Task NewSheet_FreezesTheCurrentTolerance_TheOldOneKeepsItsOwn()
    {
        var (controloId, jobOnId, machineId) = await SeedAsync();
        var first = await RecordAsync(controloId, "CM", 70.00m);
        _db.PegamentosTolerances.Add(new PegamentosTolerance { ToleranceMm = 0.30m });
        await _db.SaveChangesAsync();
        await ChangeToolsAsync(jobOnId, machineId, "CM-B", "MF-A", "BQ-A");

        var nova = await RecordAsync(controloId, "CM", 70.75m);

        Assert.Equal(PegamentosCalculation.DefaultToleranceMm,
            (await _db.Pegamentos.AsNoTracking().SingleAsync(p => p.Id == first.PegamentosId)).ToleranceMm);
        Assert.Equal(0.30m, (await _db.Pegamentos.AsNoTracking().SingleAsync(p => p.Id == nova.PegamentosId)).ToleranceMm);
        // 70.75 contra 70.50 ±0,30: dentro (com ±0,20 estaria fora).
        Assert.False(nova.Evaluation.HasAlert);
    }

    [Fact]
    public async Task AfterTheChange_BeforeTheFirstReading_TheSheetIsReadyWithTheNewTools_AndTheOldOneOpensOnRequest()
    {
        var (controloId, jobOnId, machineId) = await SeedAsync();
        var first = await RecordAsync(controloId, "CM", 70.05m);
        await StampSheetsAsync();
        await ChangeToolsAsync(jobOnId, machineId, "CM-B", "MF-A", "BQ-A");

        var pending = (await _query.FindSheetByControloAsync(controloId))!;
        Assert.Null(pending.Pegamentos);
        Assert.Equal("CM-B", pending.CmToolId);
        Assert.Equal("CM-B", pending.Components.Single(c => c.Component == PegamentosComponent.Cm).ToolId);
        Assert.False(Assert.Single(pending.Sheets).IsCurrent);

        var old = (await _query.FindSheetByControloAsync(controloId, first.PegamentosId))!;
        Assert.Equal(first.PegamentosId, old.Pegamentos!.PegamentosId);
        Assert.False(old.Pegamentos.IsCurrent);
        Assert.Equal("CM-A", old.CmToolId);
        Assert.Equal("CM-A", old.Components.Single(c => c.Component == PegamentosComponent.Cm).ToolId);
        Assert.Equal("CM-B", old.CurrentComponents.Single(c => c.Component == PegamentosComponent.Cm).ToolId);
        Assert.Equal(1, old.SheetNumber(old.Pegamentos));

        // Pelo pegamentos_id abre a mesma folha, com as ferramentas dela.
        var byId = (await _query.FindSheetByPegamentosAsync(first.PegamentosId))!;
        Assert.Equal(first.PegamentosId, byId.Pegamentos!.PegamentosId);
        Assert.Equal("CM-A", byId.CmToolId);
        Assert.False(Assert.Single(await _query.ListSheetsByControloAsync(controloId)).IsCurrent);

        // O PDF de cada folha diz qual é; a folha antiga leva o nominal e a CM dela.
        var oldPdf = PegamentosPdfGenerator.FromSheet(old);
        Assert.Equal("Folha 1 de 2 · registada a 18/10/2026", oldPdf.SheetLabel);
        var cmCard = oldPdf.Components.Single(c => c.Component == "CM");
        Assert.Equal(("5447", "1", (decimal?)70.00m), (cmCard.Reference, cmCard.Lot, cmCard.NominalGargaloMm));
        Assert.Equal("Folha 2 de 2 · por começar", PegamentosPdfGenerator.FromSheet(pending).SheetLabel);
    }

    [Fact]
    public async Task ResumoPdf_TwoSheets_OneSectionPerSheet_TheAlertOfTheSecondIsNotHidden()
    {
        var (controloId, jobOnId, machineId) = await SeedAsync();
        await RecordAsync(controloId, "CM", 70.05m);
        await ChangeToolsAsync(jobOnId, machineId, "CM-A", "MF-A", "BQ-B");
        var fora = await RecordAsync(controloId, "BQ", 31.00m);
        Assert.True(fora.Evaluation.HasAlert);
        await StampSheetsAsync();
        var sheet = (await _query.FindSheetByControloAsync(controloId))!;
        var detalhes = sheet.Sheets.Select(folha => sheet with
        {
            CmToolId = folha.CmToolId,
            BqToolId = folha.BqToolId,
            MfToolId = folha.MfToolId,
            Components = folha.Components,
            Pegamentos = folha
        }).ToList();
        var summary = new ResumoPdfGenerator.PegamentosSummary(2, 1, 0.20m)
        {
            Sheets = sheet.Sheets.Select((f, i) => new ResumoPdfGenerator.PegamentosSheetLine(
                $"Folha {i + 1} · {ResumoPdfGenerator.ToolsOf(f)}", f.Measurements.Count,
                f.Measurements.Count(m => m.Evaluation.HasAlert), 0, f.ToleranceMm,
                ResumoPdfGenerator.PegamentosSection(i + 1))).ToArray()
        };

        var text = PdfText.Of(new ResumoPdfGenerator().GenerateBytes(new ResumoPdfGenerator.Input(
            sheet.ProductionNumber, sheet.Reference, sheet.Machine, null, null, [], null, summary, [], null,
            PegamentosDetalhe: detalhes[0], PegamentosDetalhes: detalhes)));

        Assert.Contains("Existem medições fora das tolerâncias", text);
        Assert.Contains("Folha 1 · CM 5447 · BQ T194 · MF 5810 Ver 1 medição(ões) · ± 0.20 mm", text);
        Assert.Contains("Folha 2 · CM 5447 · BQ T195 · MF 5810 Ver 1 medição(ões) · 1 em alerta · ± 0.20 mm", text);
        Assert.Contains("Folha 1 de 2 · registada a 18/10/2026", text);
        Assert.Contains("Folha 2 de 2 · registada a 18/10/2026", text);
        Assert.Equal("pegamentos-2", ResumoPdfGenerator.PegamentosSection(2));
        Assert.Equal("pegamentos", ResumoPdfGenerator.PegamentosSection(1));
    }

    [Fact]
    public async Task ResumoPdfCompleto_FolhaDeFora_SemLigacao_ACardLigaAFolhaQueEntra()
    {
        // O PDF completo (Diogo, 19/10/2026) deixa de fora as folhas sem
        // medições: a linha dela fica sem «Ver» e o cartão liga à folha
        // que entra, com a secção dela.
        var (controloId, jobOnId, machineId) = await SeedAsync();
        await RecordAsync(controloId, "CM", 70.05m);
        await ChangeToolsAsync(jobOnId, machineId, "CM-A", "MF-A", "BQ-B");
        await RecordAsync(controloId, "BQ", 31.00m);
        await StampSheetsAsync();
        var sheet = (await _query.FindSheetByControloAsync(controloId))!;
        var segunda = sheet with
        {
            CmToolId = sheet.Sheets[1].CmToolId,
            BqToolId = sheet.Sheets[1].BqToolId,
            MfToolId = sheet.Sheets[1].MfToolId,
            Components = sheet.Sheets[1].Components,
            Pegamentos = sheet.Sheets[1]
        };
        var summary = new ResumoPdfGenerator.PegamentosSummary(1, 1, 0.20m)
        {
            Sheets = sheet.Sheets.Select((f, i) => new ResumoPdfGenerator.PegamentosSheetLine(
                $"Folha {i + 1} · {ResumoPdfGenerator.ToolsOf(f)}", f.Measurements.Count,
                f.Measurements.Count(m => m.Evaluation.HasAlert), 0, f.ToleranceMm,
                i == 0 ? null : ResumoPdfGenerator.PegamentosSection(i + 1))).ToArray(),
            Section = ResumoPdfGenerator.PegamentosSection(2)
        };

        var text = PdfText.Of(new ResumoPdfGenerator().GenerateBytes(new ResumoPdfGenerator.Input(
            sheet.ProductionNumber, sheet.Reference, sheet.Machine, null, null, [], null, summary, [], null,
            PegamentosDetalhe: segunda, PegamentosDetalhes: [segunda], PegamentosSecoes: ["pegamentos-2"])));

        Assert.Contains("Controlo Ver detalhe", text);
        // Os títulos do PDF do Resumo (Diogo, 19/10/2026): «Controlo»,
        // «Ferramenta» e «Verificação».
        Assert.Contains("Ferramenta Sem ferramentas registadas", text);
        Assert.Contains("Verificação CM BQ MF PU CS", text);
        Assert.DoesNotContain("Controlos associados", text);
        Assert.DoesNotContain("A combinação da produção", text);
        Assert.DoesNotContain("Estado do documento", text);
        Assert.Contains("Folha 1 · CM 5447 · BQ T194 · MF 5810 1 medição(ões)", text);
        Assert.Contains("Folha 2 · CM 5447 · BQ T195 · MF 5810 Ver 1 medição(ões)", text);
        Assert.Contains("Folha 2 de 2 · registada a 18/10/2026", text);
        Assert.DoesNotContain("Folha 1 de 2", text);
        Assert.Contains("Voltar ao Resumo", text);
    }

    [Fact]
    public async Task SingleSheet_LooksAsBefore_NoSheetLabelOrSuffix()
    {
        var (controloId, _, _) = await SeedAsync();
        await RecordAsync(controloId, "CM", 70.05m);

        var sheet = (await _query.FindSheetByControloAsync(controloId))!;

        Assert.True(Assert.Single(sheet.Sheets).IsCurrent);
        Assert.Null(PegamentosPdfGenerator.FromSheet(sheet).SheetLabel);
        Assert.Equal(
            ControloDocumentPaths.GetPegamentosFileName(sheet.Reference, sheet.ProductionNumber),
            ControloDocumentPaths.GetPegamentosFileName(sheet.Reference, sheet.ProductionNumber, sheet.SheetNumber(sheet.Pegamentos)!.Value));
    }

    [Fact]
    public async Task Association_ProductionWithOnlyAnEarlierCombinationSheet_TakesTheSheetOfTheCurrentTools()
    {
        var (controloId, jobOnId, machineId) = await SeedAsync();
        var first = await RecordAsync(controloId, "CM", 70.05m);
        await ChangeToolsAsync(jobOnId, machineId, "CM-B", "MF-A", "BQ-A");
        var create = new CreateToolScopedPegamentosHandler(_db, new PegamentosToleranceConfiguration(_db));
        var semJobOn = await create.HandleAsync(new CreateToolScopedPegamentos("CM-B", "BQ-A", "MF-A"));
        var other = await create.HandleAsync(new CreateToolScopedPegamentos("CM-B", "BQ-A", "MF-B"));
        var associate = new AssociatePegamentosToControloHandler(_db);

        var outcome = await associate.HandleAsync(new AssociatePegamentosToControlo(controloId, semJobOn.PegamentosId!.Value));

        Assert.Equal(AssociatePegamentosToControloResult.Success, outcome.Result);
        var next = await RecordAsync(controloId, "CM", 70.55m);
        Assert.False(next.StartedNewSheet);
        Assert.Equal(semJobOn.PegamentosId, next.PegamentosId);
        Assert.Equal(new[] { first.PegamentosId, semJobOn.PegamentosId!.Value },
            (await _query.ListSheetsByControloAsync(controloId)).Select(s => s.PegamentosId));

        // Outra folha com as ferramentas atuais: a produção já tem a dela.
        Assert.Equal(AssociatePegamentosToControloResult.ToolMismatch,
            (await associate.HandleAsync(new AssociatePegamentosToControlo(controloId, other.PegamentosId!.Value))).Result);
        var duplicate = await create.HandleAsync(new CreateToolScopedPegamentos("CM-B", "BQ-A", "MF-A"));
        Assert.Equal(AssociatePegamentosToControloResult.ControloAlreadyHasPegamentos,
            (await associate.HandleAsync(new AssociatePegamentosToControlo(controloId, duplicate.PegamentosId!.Value))).Result);
    }
}
