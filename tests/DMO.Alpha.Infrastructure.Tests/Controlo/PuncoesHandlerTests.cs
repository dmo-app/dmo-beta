using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O registo das punções NNPB (o Owner 17/10/2026, a partir da folha que
/// criou depois de um problema): só uma produção cuja CM é NNPB começa um
/// registo; cada punção nasce com a punção e o arrefecedor vistos e com o
/// lote em vigor; um número repetido é uma «Alteração»; os três totais.
/// </summary>
public sealed class PuncoesHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PuncoesHandler _handler;

    public PuncoesHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new PuncoesHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> SeedProductionAsync(CmProcess? process, string? technicalName = null, string reference = "1075C142")
    {
        _db.Machines.Add(new Machine { Code = "C2" });
        _db.Tools.Add(new Tool { ToolId = "CM-PU", Type = ToolType.Cm, Reference = reference, Lot = "66", TechnicalName = technicalName });
        _db.Tools.Add(new Tool { ToolId = "MF-PU", Type = ToolType.Mf, Reference = "1075", Lot = "83" });
        _db.Tools.Add(new Tool { ToolId = "BQ-PU", Type = ToolType.Bq, Reference = "C142", Lot = "165" });
        if (process is not null)
        {
            _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "CM-PU", Process = process });
        }
        await _db.SaveChangesAsync();

        var machine = await _db.Machines.AsNoTracking().SingleAsync();
        var created = await new CreateJobOnHandler(_db).HandleAsync(
            new CreateJobOn("202604", reference, machine.Id, "CM-PU", "MF-PU", "BQ-PU"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        return (await _db.Controlos.AsNoTracking().SingleAsync()).Id;
    }

    [Fact]
    public async Task NnpbProduction_RegistersPunches_SeenByDefault_WithLotAndAlteracao()
    {
        var controloId = await SeedProductionAsync(CmProcess.Nnpb);

        var sheet = await _handler.GetSheetAsync(controloId);
        Assert.NotNull(sheet);
        Assert.True(sheet.IsNnpb);
        Assert.Equal(PuncoesNnpbSource.Ficha, sheet.NnpbSource);
        Assert.Equal("1075", sheet.DefaultPunchReference);
        Assert.Equal("1075C142", sheet.CmReference);
        Assert.Null(sheet.Registo);

        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), " 41 ", "1075", "1", "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "42", "1075", "1", "4301"));
        // O lote muda: as punções anteriores mantêm o seu.
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "41", "1075", "02", "4301"));

        _db.ChangeTracker.Clear();
        var registo = (await _handler.GetSheetAsync(controloId))!.Registo!;
        Assert.Equal("1075", registo.PunchReference);
        Assert.Equal("2", registo.Lot);
        Assert.Equal(new[] { "41", "42", "41" }, registo.Rows.Select(r => r.Number));
        Assert.Equal(new[] { "1", "1", "2" }, registo.Rows.Select(r => r.Lot));
        Assert.Equal(new[] { false, false, true }, registo.Rows.Select(r => r.IsAlteracao));
        Assert.All(registo.Rows, r => Assert.True(r.PunchSeen && r.CoolerSeen));
        Assert.All(registo.Rows, r => Assert.Equal("4301", r.RegisteredBy));
        Assert.Equal((3, 3, 3), (registo.Total, registo.PunchSeenCount, registo.CoolerSeenCount));
    }

    [Fact]
    public async Task NumeroRepetido_ComOLoteDessaPuncao_RegistaComEsseLote_EOLoteDeCimaFica()
    {
        // Diogo, 21/10/2026: «quando o numero de um puncao é diferente deveria
        // pedir o lote». A 41 repetida, logo a seguir e pela mesma pessoa,
        // com o lote 3 é outra punção (não um duplo clique); o lote de cima
        // continua 1 para as próximas.
        var controloId = await SeedProductionAsync(CmProcess.Nnpb);
        var alvo = PuncoesTarget.ForControlo(controloId);
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(alvo, "41", "1075", "1", "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(alvo, "41", "1075", "1", "4301", punchLot: "3"));
        Assert.Equal(PuncoesResult.InvalidLot, await _handler.AddAsync(alvo, "41", "1075", "1", "4301", punchLot: "x"));
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(alvo, "42", "1075", "1", "4301", punchLot: "9"));

        _db.ChangeTracker.Clear();
        var registo = (await _handler.GetSheetAsync(controloId))!.Registo!;
        Assert.Equal("1", registo.Lot);
        Assert.Equal(new[] { "41", "41", "42" }, registo.Rows.Select(r => r.Number));
        // O 42 é novo: o lote da punção não conta, fica o de cima.
        Assert.Equal(new[] { "1", "3", "1" }, registo.Rows.Select(r => r.Lot));
        Assert.Equal(new[] { false, true, false }, registo.Rows.Select(r => r.IsAlteracao));
    }

    // Sem botões nas linhas (Diogo, 27/10/2026): os botões do canto, sem
    // script, não dizem o estado novo; sem valor, troca o que lá está.
    [Fact]
    public async Task Seen_SemValor_TrocaOEstadoQueLaEsta()
    {
        var controloId = await SeedProductionAsync(CmProcess.Nnpb);
        var alvo = PuncoesTarget.ForControlo(controloId);
        await _handler.AddAsync(alvo, "41", "1075", "1", "4301");
        var id = (await _handler.GetSheetAsync(controloId))!.Registo!.Rows.Single().Id;

        Assert.Equal(PuncoesResult.Success, await _handler.SetSeenAsync(alvo, id, PuncaoSeenField.Puncao, null, "4301"));
        _db.ChangeTracker.Clear();
        var linha = (await _handler.GetSheetAsync(controloId))!.Registo!.Rows.Single();
        Assert.False(linha.PunchSeen);
        Assert.True(linha.CoolerSeen);

        Assert.Equal(PuncoesResult.Success, await _handler.SetSeenAsync(alvo, id, PuncaoSeenField.Puncao, null, "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.SetSeenAsync(alvo, id, PuncaoSeenField.Arrefecedor, null, "4301"));
        _db.ChangeTracker.Clear();
        linha = (await _handler.GetSheetAsync(controloId))!.Registo!.Rows.Single();
        Assert.True(linha.PunchSeen);
        Assert.False(linha.CoolerSeen);
    }

    [Fact]
    public async Task SeenMarks_AndRemove_UpdateTheTotals()
    {
        var controloId = await SeedProductionAsync(CmProcess.Nnpb);
        await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "41", "1075", "1", "4301");
        await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "42", "1075", "1", "4301");
        var rows = (await _handler.GetSheetAsync(controloId))!.Registo!.Rows;

        Assert.Equal(PuncoesResult.Success, await _handler.SetSeenAsync(PuncoesTarget.ForControlo(controloId), rows[0].Id, PuncaoSeenField.Arrefecedor, false, "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.SetSeenAsync(PuncoesTarget.ForControlo(controloId), rows[1].Id, PuncaoSeenField.Puncao, false, "4301"));
        _db.ChangeTracker.Clear();
        var registo = (await _handler.GetSheetAsync(controloId))!.Registo!;
        Assert.Equal((2, 1, 1), (registo.Total, registo.PunchSeenCount, registo.CoolerSeenCount));
        Assert.False(registo.Rows[0].CoolerSeen);
        Assert.True(registo.Rows[0].PunchSeen);

        Assert.Equal(PuncoesResult.Success, await _handler.RemoveAsync(PuncoesTarget.ForControlo(controloId), rows[0].Id, "4301"));
        _db.ChangeTracker.Clear();
        registo = (await _handler.GetSheetAsync(controloId))!.Registo!;
        Assert.Equal("42", Assert.Single(registo.Rows).Number);

        // Uma punção de outro controlo, ou já removida, não é tocada.
        Assert.Equal(PuncoesResult.InspecaoNotFound, await _handler.RemoveAsync(PuncoesTarget.ForControlo(controloId), rows[0].Id, "4301"));
        Assert.Equal(PuncoesResult.InspecaoNotFound, await _handler.SetSeenAsync(PuncoesTarget.ForControlo(controloId + 1), rows[1].Id, PuncaoSeenField.Puncao, true, "4301"));

        // A próxima punção fica depois da última, sem reutilizar posições.
        await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "43", "1075", "1", "4301");
        _db.ChangeTracker.Clear();
        registo = (await _handler.GetSheetAsync(controloId))!.Registo!;
        Assert.Equal(new[] { 2, 3 }, registo.Rows.Select(r => r.Position));
    }

    [Theory]
    [InlineData(null, "1075", "1", PuncoesResult.MissingNumber)]
    [InlineData("41", " ", "1", PuncoesResult.MissingPunchReference)]
    [InlineData("41", "1075", "", PuncoesResult.InvalidLot)]
    [InlineData("41", "1075", "A", PuncoesResult.InvalidLot)]
    [InlineData("41", "1075", "0", PuncoesResult.InvalidLot)]
    public async Task InvalidInput_IsRefused_AndNothingIsWritten(string? number, string reference, string lot, PuncoesResult expected)
    {
        var controloId = await SeedProductionAsync(CmProcess.Nnpb);

        Assert.Equal(expected, await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), number, reference, lot, "4301"));
        Assert.Empty(_db.Puncoes);
        Assert.Empty(_db.PuncaoInspecoes);
    }

    [Fact]
    public async Task PsOrUnknownProcess_IsNotNnpb_AndCannotStartARegisto()
    {
        var controloId = await SeedProductionAsync(CmProcess.Ps);
        var sheet = await _handler.GetSheetAsync(controloId);
        Assert.False(sheet!.IsNnpb);
        Assert.Equal("PS", sheet.CmProcess);
        Assert.Equal(PuncoesResult.NotNnpb, await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "41", "1075", "1", "4301"));
        Assert.Equal(PuncoesResult.NotNnpb, await _handler.SaveIdentificationAsync(PuncoesTarget.ForControlo(controloId), "1075", "1", "4301"));
        Assert.Empty(_db.Puncoes);

        Assert.Null(await _handler.GetSheetAsync(controloId + 99));
        Assert.Equal(PuncoesResult.ControloNotFound, await _handler.AddAsync(PuncoesTarget.ForControlo(controloId + 99), "41", "1075", "1", "4301"));
    }

    [Fact]
    public async Task EmptyProcess_FallsBackToTheNnOfTheCmTechnicalName()
    {
        var controloId = await SeedProductionAsync(process: null, technicalName: "M107564MPNNV");

        var sheet = await _handler.GetSheetAsync(controloId);
        Assert.True(sheet!.IsNnpb);
        Assert.Equal(PuncoesNnpbSource.NomeTecnico, sheet.NnpbSource);
        Assert.Null(sheet.CmProcess);
        Assert.Equal(PuncoesResult.Success, await _handler.SaveIdentificationAsync(PuncoesTarget.ForControlo(controloId), "1075", "3", "4301"));
        Assert.Equal("3", (await _handler.GetSheetAsync(controloId))!.Registo!.Lot);
    }

    [Theory]
    [InlineData(CmProcess.Ps, "M107564MPNNV", false)]
    [InlineData(null, "M002044MPSSX", false)]
    [InlineData(null, null, false)]
    [InlineData(CmProcess.Nnpb, null, true)]
    public void ResolveNnpb_FichaWinsOverTheTechnicalName(CmProcess? process, string? technicalName, bool expected) =>
        Assert.Equal(expected, PuncoesHandler.ResolveNnpb(process, technicalName).IsNnpb);

    [Theory]
    [InlineData("1075C142", "1075")]
    [InlineData("6018Y232", "6018")]
    [InlineData("ST12", null)]
    public void DefaultPunchReference_IsTheModelOfTheReference(string reference, string? expected) =>
        Assert.Equal(expected, PuncoesHandler.DefaultPunchReference(reference));

    [Fact]
    public async Task Pdf_PosicoesDaLinha_EOResumoLigaOFicheiroDasPuncoes()
    {
        // Diogo, 22/10/2026: o PDF das punções é a tabela das posições da
        // linha, gerado em memória; o PDF do Controlo não leva a tabela, só a
        // ligação ao ficheiro das punções, gravado à parte na mesma pasta.
        var controloId = await SeedProductionAsync(CmProcess.Nnpb);
        await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "41", "1075", "1", "4301");
        await _handler.AddAsync(PuncoesTarget.ForControlo(controloId), "41", "1075", "2", "4301");
        var sheet = (await _handler.GetSheetAsync(controloId))!;
        var input = new PuncoesPosicoesPdfGenerator.Input(sheet.Reference, sheet.Machine, sheet.ProductionNumber,
            new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 11), "Diogo",
            PuncoesPosicoes.Distribuir(sheet.Registo!.Rows, 12));

        var bytes = new PuncoesPosicoesPdfGenerator().GenerateBytes(input);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        var sheetText = PdfText.Of(bytes);
        Assert.Contains("Registo de punções PU · NNPB", sheetText);
        Assert.Contains("Referência 1075C142 · Linha C2 · Produção 202604 · 04/08/2026 a 11/08/2026 · Registado por Diogo", sheetText);
        // A1 e B1 com o 41 a negrito e o lote por baixo (as colunas lidas lado a lado).
        Assert.Contains("41 A Lote 1 41 B Lote 2", sheetText);
        Assert.Contains("Punções verificados 2", sheetText);
        Assert.Contains("Linha C2 2", sheetText);
        Assert.DoesNotContain("A1 41 1", sheetText);
        Assert.Contains("B1 41 2", sheetText);
        Assert.DoesNotContain("APROVADO", sheetText);
        Assert.DoesNotContain("Aprovação", sheetText);

        var ficheiro = ControloDocumentPaths.GetPuncoesFileName("1075C142", "202604");
        Assert.Equal("PU-NNPB-1075C142-202604.pdf", ficheiro);
        var resumo = new ResumoPdfGenerator.Input("202604", "1075C142", "C2", null, null,
            Array.Empty<ResumoPdfGenerator.ToolCard>(), null, null, Array.Empty<ResumoPdfGenerator.Viewing>(), null, "");
        var with = new ResumoPdfGenerator().GenerateBytes(resumo with
        {
            Puncoes = new ResumoPdfGenerator.PuncoesSummary(2, 2, 2),
            PuncoesFicheiro = ficheiro
        });
        var withText = PdfText.Of(with);
        Assert.Contains("TOTAL PU VISTAS ARREF. VISTOS 2 2 2", withText);
        Assert.Contains("Abrir PDF das punções →", withText);
        Assert.DoesNotContain("Registo de punções", withText);
        Assert.Contains("1 / 1", withText);
        using var document = UglyToad.PdfPig.PdfDocument.Open(with);
        Assert.Contains(ficheiro, document.GetPage(1).GetHyperlinks().Select(l => l.Uri));

        var semFicheiro = PdfText.Of(new ResumoPdfGenerator().GenerateBytes(resumo with { Puncoes = new ResumoPdfGenerator.PuncoesSummary(2, 2, 2) }));
        Assert.DoesNotContain("Abrir PDF das punções", semFicheiro);
    }

    [Fact]
    public async Task WithoutJobOn_RegisterStandsAlone_ListedByReference()
    {
        // O Owner 17/10/2026: o registo pode nascer antes do Job On, como o Peso.
        var (created, puncoesId) = await _handler.CreateStandaloneAsync(" 1075c142 ", "1075", "01", "4301");
        Assert.Equal(PuncoesResult.Success, created);
        var target = PuncoesTarget.ForRegisto(puncoesId!.Value);

        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(target, "41", "1075", "1", "4301"));
        // O mesmo número com o mesmo lote é a mesma punção: recusada (Diogo,
        // 24/10/2026). Com outro lote é uma «Alteração».
        await AgeInspecoesAsync(puncoesId.Value);
        Assert.Equal(PuncoesResult.DuplicatePunch, await _handler.AddAsync(target, "41", "1075", "1", "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(target, "41", "1075", "1", "4301", punchLot: "2"));
        _db.ChangeTracker.Clear();

        var sheet = (await _handler.GetStandaloneAsync(puncoesId.Value))!;
        Assert.Equal("1075C142", sheet.Reference);
        Assert.Null(sheet.ControloId);
        Assert.Equal("1", sheet.Registo.Lot);
        Assert.Equal(new[] { false, true }, sheet.Registo.Rows.Select(r => r.IsAlteracao));

        var rows = sheet.Registo.Rows;
        Assert.Equal(PuncoesResult.Success, await _handler.SetSeenAsync(target, rows[0].Id, PuncaoSeenField.Puncao, false, "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.RemoveAsync(target, rows[1].Id, "4301"));
        // A referência total corrige-se na identificação do registo sem Job On.
        Assert.Equal(PuncoesResult.Success, await _handler.SaveIdentificationAsync(target, "1075", "2", "4301", reference: "1075c143"));
        Assert.Equal(PuncoesResult.MissingReference, await _handler.SaveIdentificationAsync(target, "1075", "2", "4301", reference: " "));
        _db.ChangeTracker.Clear();

        sheet = (await _handler.GetStandaloneAsync(puncoesId.Value))!;
        Assert.Equal("1075C143", sheet.Reference);
        Assert.Equal((1, 0, 1), (sheet.Registo.Total, sheet.Registo.PunchSeenCount, sheet.Registo.CoolerSeenCount));

        Assert.Single(await _handler.ListStandaloneAsync("1075c143"));
        Assert.Empty(await _handler.ListStandaloneAsync("1075C142"));
        Assert.Empty(await _handler.ListStandaloneAsync(" "));

        Assert.Equal(PuncoesResult.MissingReference, (await _handler.CreateStandaloneAsync(" ", "1075", "1", "4301")).Result);
        Assert.Equal(PuncoesResult.InvalidLot, (await _handler.CreateStandaloneAsync("1075C142", "1075", "A", "4301")).Result);
        Assert.Equal(PuncoesResult.RegistoNotFound, await _handler.AddAsync(PuncoesTarget.ForRegisto(puncoesId.Value + 99), "41", "1075", "1", "4301"));
        Assert.Equal(PuncoesResult.InspecaoNotFound, await _handler.RemoveAsync(PuncoesTarget.ForRegisto(puncoesId.Value + 99), rows[0].Id, "4301"));
    }

    private async Task AgeInspecoesAsync(int puncoesId)
    {
        foreach (var inspecao in _db.PuncaoInspecoes.Where(i => i.PuncoesId == puncoesId))
        {
            inspecao.RegisteredAtUtc = DateTime.UtcNow.AddMinutes(-1);
        }

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task RepeatedClicks_StartTheRegisterAndRegisterThePunchOnlyOnce()
    {
        // Varredura de 18/10/2026, B5: carregar várias vezes seguidas em
        // «Começar registo» ou em «Registar» não cria registos nem punções
        // repetidos, e não mostra nada; segue como um só clique.
        var first = await _handler.CreateStandaloneAsync("1075C150", "1075", "1", "4301");
        var again = await _handler.CreateStandaloneAsync("1075c150 ", "1075", "1", "4301");
        Assert.Equal(PuncoesResult.Success, again.Result);
        Assert.Equal(first.PuncoesId, again.PuncoesId);

        var target = PuncoesTarget.ForRegisto(first.PuncoesId!.Value);
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(target, "41", "1075", "1", "4301"));
        // A segunda vez é a mesma punção: não se grava e diz-se (Diogo, 24/10/2026).
        Assert.Equal(PuncoesResult.DuplicatePunch, await _handler.AddAsync(target, "41", "1075", "1", "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.AddAsync(target, "42", "1075", "1", "4301"));
        _db.ChangeTracker.Clear();
        var sheet = (await _handler.GetStandaloneAsync(first.PuncoesId.Value))!;
        Assert.Equal(new[] { "41", "42" }, sheet.Registo.Rows.Select(r => r.Number));

        // Com punções já registadas, «Começar registo» igual começa outro.
        var other = await _handler.CreateStandaloneAsync("1075C150", "1075", "1", "4301");
        Assert.NotEqual(first.PuncoesId, other.PuncoesId);
    }

    [Fact]
    public async Task StandaloneRegister_IsAssociatedByHand_ToAnNnpbProductionWithoutRegister()
    {
        var controloId = await SeedProductionAsync(CmProcess.Nnpb);
        var (_, puncoesId) = await _handler.CreateStandaloneAsync("1075C142", "1075", "1", "4301");
        await _handler.AddAsync(PuncoesTarget.ForRegisto(puncoesId!.Value), "41", "1075", "1", "4301");

        Assert.Equal(PuncoesResult.ControloNotFound, await _handler.AssociateAsync(puncoesId.Value, controloId + 99, "4301"));
        Assert.Equal(PuncoesResult.Success, await _handler.AssociateAsync(puncoesId.Value, controloId, "4301"));
        _db.ChangeTracker.Clear();

        var sheet = (await _handler.GetSheetAsync(controloId))!;
        Assert.Equal("41", Assert.Single(sheet.Registo!.Rows).Number);
        Assert.Empty(await _handler.ListStandaloneAsync("1075C142"));
        Assert.Equal(PuncoesResult.AlreadyAssociated, await _handler.AssociateAsync(puncoesId.Value, controloId, "4301"));

        // Outro registo sem Job On não entra numa produção que já tem o seu.
        var (_, outro) = await _handler.CreateStandaloneAsync("1075C142", "1075", "1", "4301");
        Assert.Equal(PuncoesResult.ControloHasRegisto, await _handler.AssociateAsync(outro!.Value, controloId, "4301"));
        Assert.Equal(PuncoesResult.RegistoNotFound, await _handler.AssociateAsync(outro.Value + 99, controloId, "4301"));
    }

    [Fact]
    public async Task StandaloneRegister_IsNotAssociated_ToAPsProduction()
    {
        var controloId = await SeedProductionAsync(CmProcess.Ps);
        var (_, puncoesId) = await _handler.CreateStandaloneAsync("1075C142", "1075", "1", "4301");

        Assert.Equal(PuncoesResult.NotNnpb, await _handler.AssociateAsync(puncoesId!.Value, controloId, "4301"));
        _db.ChangeTracker.Clear();
        Assert.Null((await _handler.GetStandaloneAsync(puncoesId.Value))!.ControloId);
    }

    [Fact]
    public void Pdf_StandaloneRegister_SaysSemJobOn()
    {
        var input = new PuncoesPosicoesPdfGenerator.Input("1075C142", "—", "Sem Job On", null, null, null,
            PuncoesPosicoes.Distribuir(Array.Empty<PuncaoReadModel>(), 12));
        var text = PdfText.Of(new PuncoesPosicoesPdfGenerator().GenerateBytes(input));
        Assert.Contains("Referência 1075C142 · Linha — · Produção Sem Job On", text);
        Assert.Contains("Sem punções repetidos", text);
    }
}
