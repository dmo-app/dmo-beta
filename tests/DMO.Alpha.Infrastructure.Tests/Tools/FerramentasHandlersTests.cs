using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// Prova os handlers canónicos do corte A (Ferramentas) no registo real
/// (InMemory, sem servidor): a criação minta o tool_id («T» + 10 hex
/// minúsculos, verificação + re-mint — nunca sequencial, nunca derivado
/// dos factos) com recusas tipadas do enum fechado ANTES de qualquer
/// escrita; a edição de factos é full-replace no MESMO tool_id com
/// substituição atómica das máquinas; a edição de valores técnicos
/// substitui TODAS as colunas do contexto técnico (criando a linha quando
/// não existe; vazio = NULL — o estado «em falta» é válido) e a geometria
/// TP é registada TAL COMO VEM DA FOLHA (os valores reais da folha genesis
/// são o worked example: a app NUNCA recalcula derivados); a duplicação
/// copia factos+técnicos como defaults EDITÁVEIS com lote novo OBRIGATÓRIO
/// e fonte intacta; a eliminação é a transação canónica (backfill dos
/// congelados → reatribuição Pesos/Pegamentos à DUMMY REMOVED → âncora dos
/// traces não resolvidos a NULL → apagar ligações/contexto técnico/tool
/// FISICAMENTE) — o histórico nunca é destruído.
/// </summary>
public sealed class FerramentasHandlersTests : IDisposable
{
    private readonly DmoDbContext _db;

    public FerramentasHandlersTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>A linha DUMMY canónica, como a migração #7 semeia.</summary>
    private async Task SeedDummyAsync()
    {
        _db.Tools.Add(new Tool
        {
            ToolId = ToolRegistryConstants.RemovedToolId,
            Type = ToolType.Cm,
            Reference = "REMOVED",
            Lot = "REMOVED"
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Machine> SeedMachineAsync(string code)
    {
        var machine = new Machine { Code = code };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    // ------------------------------------------------------------------
    // CreateTool — o ato canónico do Owner.
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateTool_MintsCanonicalToolId_BackendEmittedNeverDerivedFromFacts()
    {
        var machine = await SeedMachineAsync("B1");

        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));
        var outcome = await handler.HandleAsync(new CreateTool(
            "CM", "5447", "3", "5810", null, new[] { machine.Id }));

        Assert.Equal(CreateToolResult.Success, outcome.Result);
        Assert.NotNull(outcome.Tool);

        // «T» + 10 hex minúsculos: a identidade é emitida pelo backend.
        Assert.Matches("^T[0-9a-f]{10}$", outcome.Tool!.ToolId);
        Assert.Equal(ToolType.Cm, outcome.Tool.Type);
        Assert.Equal("5447", outcome.Tool.Reference);
        Assert.Equal("3", outcome.Tool.Lot);
        Assert.Equal("5810", outcome.Tool.AssociatedMfReference);

        // Persistido com a ligação de máquina aplicável — uma única
        // escrita atómica.
        var persisted = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == outcome.Tool.ToolId);
        Assert.Equal("5447", persisted.Reference);
        Assert.Equal("5810", persisted.AssociatedMfReference);
        var link = await _db.ToolMachineLinks.AsNoTracking().SingleAsync();
        Assert.Equal(outcome.Tool.ToolId, link.ToolId);
        Assert.Equal(machine.Id, link.MachineId);

        // Dois registos de factos iguais: a chave é (tipo, referência,
        // lote) e a repetida é recusada (o proprietário, 18/10/2026; a
        // prova completa vive em CreateTool_Repetida_*).
        await RegistosAntigos.EnvelhecerAsync(_db);
        var second = await handler.HandleAsync(new CreateTool("CM", "5447", "3"));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, second.Result);
        Assert.Equal(outcome.Tool!.ToolId, second.ExistingToolId);
        Assert.Equal(1, await _db.Tools.AsNoTracking().CountAsync());
    }

    // ------------------------------------------------------------------
    // CreateTool — a ferramenta repetida (o proprietário, 18/10/2026).
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateTool_Repetida_ERecusada_EAExistenteFicaComAviso()
    {
        // A chave é (tipo, referência, lote). A segunda com os mesmos três
        // factos é recusada (já não abre a existente, o get-or-create de
        // 12/10/2026): nada é registado, as máquinas não se acrescentam e a
        // existente fica com o aviso, porque a errada pode ser ela.
        var b1 = await SeedMachineAsync("B1");
        var c3 = await SeedMachineAsync("C3");
        var b2 = await SeedMachineAsync("B2");

        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));

        var first = await handler.HandleAsync(new CreateTool("CM", "0086", "1", null, null, new[] { b1.Id }));
        Assert.Equal(CreateToolResult.Success, first.Result);
        Assert.False(first.Tool!.AlreadyExisted);
        Assert.Null((await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == first.Tool.ToolId)).DuplicateWarningAtUtc);

        await RegistosAntigos.EnvelhecerAsync(_db);
        var repeat = await handler.HandleAsync(new CreateTool(
            "CM", "0086", "1", null, null, new[] { b1.Id, c3.Id }, RequestedBy: "Diogo"));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, repeat.Result);
        Assert.Null(repeat.Tool);
        Assert.Equal(first.Tool.ToolId, repeat.ExistingToolId);

        Assert.Equal(1, await _db.Tools.AsNoTracking().CountAsync(t => t.Reference == "0086" && t.Lot == "1"));
        Assert.Equal(new[] { b1.Id }, await _db.ToolMachineLinks.AsNoTracking()
            .Where(l => l.ToolId == first.Tool.ToolId).Select(l => l.MachineId).ToListAsync());
        var marcada = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == first.Tool.ToolId);
        Assert.NotNull(marcada.DuplicateWarningAtUtc);
        Assert.Equal("Diogo", marcada.DuplicateWarningByNameFrozen);

        // Limpar o aviso depois de confirmar que está certa.
        Assert.True(await new ToolDuplicateWarnings(_db).ClearAsync(first.Tool.ToolId));
        _db.ChangeTracker.Clear();
        var limpa = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == first.Tool.ToolId);
        Assert.Null(limpa.DuplicateWarningAtUtc);
        Assert.Null(limpa.DuplicateWarningByNameFrozen);
        Assert.False(await new ToolDuplicateWarnings(_db).ClearAsync("NAO-EXISTE"));

        // Um lote diferente é outra ferramenta legítima.
        var secondLot = await handler.HandleAsync(new CreateTool("CM", "0086", "2", null, null, new[] { b2.Id }));
        Assert.Equal(CreateToolResult.Success, secondLot.Result);
        Assert.NotEqual(first.Tool.ToolId, secondLot.Tool!.ToolId);

        // O TIPO faz parte da chave: (BQ, 0086, lote 1) é outra Tool.
        var otherType = await handler.HandleAsync(new CreateTool("BQ", "0086", "1"));
        Assert.Equal(CreateToolResult.Success, otherType.Result);
        Assert.Equal(3, await _db.Tools.AsNoTracking().CountAsync(t => t.Reference != "REMOVED"));

        // A linha DUMMY nunca conta: (CM, REMOVED, REMOVED) cria a linha
        // própria, não é recusada por causa da tombstone.
        await SeedDummyAsync();
        var notTheDummy = await handler.HandleAsync(new CreateTool("CM", "REMOVED", "REMOVED"));
        Assert.Equal(CreateToolResult.Success, notTheDummy.Result);
        Assert.NotEqual(ToolRegistryConstants.RemovedToolId, notTheDummy.Tool!.ToolId);
    }

    [Fact]
    public async Task Repetida_SoComMaquinaEmComum_CriarDuplicarEditar()
    {
        // Regra do Diogo (19/10/2026): «nao pode existir mesmo lote se as
        // maquinas forem iguais, mas se existir um MF 1075 B3 Lote 4 pode».
        // O mesmo tipo, referência e lote só é repetido com pelo menos uma
        // máquina em comum; uma ferramenta sem máquinas conta como repetida.
        await SeedDummyAsync();
        var b1 = await SeedMachineAsync("B1");
        var c3 = await SeedMachineAsync("C3");
        var b3 = await SeedMachineAsync("B3");
        var c1 = await SeedMachineAsync("C1");
        var create = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));

        var b1c3 = (await create.HandleAsync(new CreateTool("MF", "1075", "4", MachineIds: new[] { b1.Id, c3.Id }))).Tool!.ToolId;
        await RegistosAntigos.EnvelhecerAsync(_db);

        // Sem máquinas em comum: outra ferramenta legítima, sem aviso.
        var naB3 = await create.HandleAsync(new CreateTool("MF", "1075", "4", MachineIds: new[] { b3.Id }));
        Assert.Equal(CreateToolResult.Success, naB3.Result);
        Assert.NotEqual(b1c3, naB3.Tool!.ToolId);
        Assert.Null((await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == b1c3)).DuplicateWarningAtUtc);
        await RegistosAntigos.EnvelhecerAsync(_db);

        // Uma máquina em comum (a C3) basta para ser repetida.
        var parcial = await create.HandleAsync(new CreateTool("MF", "1075", "4", MachineIds: new[] { c3.Id, c1.Id }, RequestedBy: "Diogo"));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, parcial.Result);
        Assert.Equal(b1c3, parcial.ExistingToolId);
        Assert.Equal("Diogo", (await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == b1c3)).DuplicateWarningByNameFrozen);

        // Sem máquinas pode estar em qualquer uma: repetida.
        var semMaquinas = await create.HandleAsync(new CreateTool("MF", "1075", "4"));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, semMaquinas.Result);

        // Duplicar: a cópia leva as máquinas da fonte. Uma MF 1075 lote 5 na
        // C1 não impede duplicar a da B1/C3 para o lote 5.
        var lote5C1 = (await create.HandleAsync(new CreateTool("MF", "1075", "5", MachineIds: new[] { c1.Id }))).Tool!.ToolId;
        await RegistosAntigos.EnvelhecerAsync(_db);
        var duplicate = new DuplicateToolHandler(_db);
        var copia = await duplicate.HandleAsync(new DuplicateTool(b1c3, "5"));
        Assert.Equal(DuplicateToolResult.Success, copia.Result);
        Assert.NotEqual(lote5C1, copia.Tool!.ToolId);
        await RegistosAntigos.EnvelhecerAsync(_db);
        // Duplicar a da B1/C3 outra vez para o lote 5 é repetida; a da B3
        // pode, porque o lote 5 só está na C1 e na B1/C3.
        var outraVez = await duplicate.HandleAsync(new DuplicateTool(b1c3, "5"));
        Assert.Equal(DuplicateToolResult.LotAlreadyExists, outraVez.Result);
        Assert.Equal(copia.Tool.ToolId, outraVez.ExistingToolId);
        Assert.Equal(DuplicateToolResult.Success, (await duplicate.HandleAsync(new DuplicateTool(naB3.Tool.ToolId, "5"))).Result);

        // Editar: pôr a da B3 também na C3 colide com a da B1/C3; ficar só
        // na B3 com outros factos não.
        var edit = new EditToolFactsHandler(_db);
        var colide = await edit.HandleAsync(new EditToolFacts(naB3.Tool.ToolId, "MF", "1075", "4", MachineIds: new[] { b3.Id, c3.Id }));
        Assert.Equal(EditToolFactsResult.LotAlreadyExists, colide.Result);
        Assert.Equal(b1c3, colide.ExistingToolId);
        var mesmaMaquina = await edit.HandleAsync(new EditToolFacts(naB3.Tool.ToolId, "MF", "1075", "4", null, 10, new[] { b3.Id }));
        Assert.Equal(EditToolFactsResult.Success, mesmaMaquina.Result);
    }

    [Fact]
    public async Task CreateTool_DuploClique_DevolveAMesma_SemRecusaNemAviso()
    {
        // Regra do Diogo (18/10/2026): cliques repetidos não mostram nada; o
        // servidor só não cria repetido. O mesmo registo nos 10 s seguintes
        // devolve a ferramenta acabada de registar, sem a marcar.
        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));

        var first = await handler.HandleAsync(new CreateTool("CM", "0086", "1"));
        var again = await handler.HandleAsync(new CreateTool("CM", "0086", "1", RequestedBy: "Diogo"));

        Assert.Equal(CreateToolResult.Success, again.Result);
        Assert.Equal(first.Tool!.ToolId, again.Tool!.ToolId);
        var tool = await _db.Tools.AsNoTracking().SingleAsync();
        Assert.NotNull(tool.RegisteredAtUtc);
        Assert.Null(tool.DuplicateWarningAtUtc);

        // Passado esse tempo já é uma ferramenta repetida.
        var old = await _db.Tools.SingleAsync();
        old.RegisteredAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();
        var late = await handler.HandleAsync(new CreateTool("CM", "0086", "1"));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, late.Result);
    }

    [Fact]
    public async Task Duplicate_DuploClique_DevolveACopia_SemRecusaNemAviso()
    {
        await SeedDummyAsync();
        var create = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));
        var source = (await create.HandleAsync(new CreateTool("CM", "5447", "3"))).Tool!.ToolId;
        var duplicate = new DuplicateToolHandler(_db);

        var copy = await duplicate.HandleAsync(new DuplicateTool(source, "4"));
        var again = await duplicate.HandleAsync(new DuplicateTool(source, "4", "Diogo"));

        Assert.Equal(DuplicateToolResult.Success, copy.Result);
        Assert.Equal(DuplicateToolResult.Success, again.Result);
        Assert.Equal(copy.Tool!.ToolId, again.Tool!.ToolId);
        Assert.Null((await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == copy.Tool.ToolId)).DuplicateWarningAtUtc);
        Assert.Equal(3, await _db.Tools.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateTool_TypedRefusals_BeforeAnyWrite()
    {
        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));

        // Token de tipo desconhecido.
        var unknownType = await handler.HandleAsync(new CreateTool("XX", "5447", "3"));
        Assert.Equal(CreateToolResult.UnknownTypeToken, unknownType.Result);
        Assert.Null(unknownType.Tool);

        // Referência em branco (identidade humana do registo).
        var missingReference = await handler.HandleAsync(new CreateTool("CM", "   ", "3"));
        Assert.Equal(CreateToolResult.MissingReference, missingReference.Result);

        // machine_id que não resolve a máquina persistida.
        var machineNotFound = await handler.HandleAsync(new CreateTool("CM", "5447", "3", null, null, new[] { 999 }));
        Assert.Equal(CreateToolResult.MachineNotFound, machineNotFound.Result);

        // Recusas antes de qualquer escrita: nada foi persistido.
        Assert.Equal(0, await _db.Tools.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.ToolMachineLinks.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateTool_EmptyLot_SavesNoLot_AndASecondOneIsRefused()
    {
        // beta.63 (A12): o lote vazio já não recusa — grava «s/lote» e o
        // outcome marca LotDefaulted para a ficha avisar. A chave (tipo,
        // referência, lote) mantém-se: a segunda sem lote com o mesmo tipo e
        // referência é a mesma ferramenta e é recusada (18/10/2026).
        var b1 = await SeedMachineAsync("B1");
        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));

        var first = await handler.HandleAsync(new CreateTool("BQ", "T194", "   "));
        Assert.Equal(CreateToolResult.Success, first.Result);
        Assert.Equal(ToolRegistryConstants.NoLot, first.Tool!.Lot);
        Assert.True(first.Tool.LotDefaulted);

        await RegistosAntigos.EnvelhecerAsync(_db);
        var second = await handler.HandleAsync(new CreateTool("BQ", "T194", "", null, null, new[] { b1.Id }));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, second.Result);
        Assert.Equal(first.Tool.ToolId, second.ExistingToolId);

        // Escrever «s/lote» à mão também é a mesma ferramenta.
        var typed = await handler.HandleAsync(new CreateTool("BQ", "T194", ToolRegistryConstants.NoLot));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, typed.Result);

        var persisted = await _db.Tools.AsNoTracking().SingleAsync();
        Assert.Equal(ToolRegistryConstants.NoLot, persisted.Lot);
        Assert.Empty(await _db.ToolMachineLinks.AsNoTracking().ToListAsync());

        // Outra referência sem lote é outra ferramenta.
        var other = await handler.HandleAsync(new CreateTool("BQ", "T195", ""));
        Assert.Equal(CreateToolResult.Success, other.Result);
        Assert.Equal(2, await _db.Tools.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateTool_BqAcceptsMultipleOrZeroMachines()
    {
        var m1 = await SeedMachineAsync("B1");
        var m2 = await SeedMachineAsync("B2");

        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));

        // BQ com duas compatibilidades é válida.
        var tooMany = await handler.HandleAsync(new CreateTool("BQ", "T194", "12", null, null, new[] { m1.Id, m2.Id }));
        Assert.Equal(CreateToolResult.Success, tooMany.Result);
        Assert.Equal(2, await _db.ToolMachineLinks.CountAsync(l => l.ToolId == tooMany.Tool!.ToolId));

        // BQ com UMA máquina: permitido.
        var one = await handler.HandleAsync(new CreateTool("BQ", "T194", "13", null, null, new[] { m1.Id }));
        Assert.Equal(CreateToolResult.Success, one.Result);

        // BQ com ZERO máquinas: permitido (completa depois) — a função
        // nunca é bloqueada pela base de dados.
        var zero = await handler.HandleAsync(new CreateTool("BQ", "C142", "8"));
        Assert.Equal(CreateToolResult.Success, zero.Result);
        Assert.Null(zero.Tool!.BqAccountedQuantity);

        // CM/MF carregam o CONJUNTO: duas máquinas permitidas.
        var cmMany = await handler.HandleAsync(new CreateTool("CM", "5447", "3", null, null, new[] { m1.Id, m2.Id }));
        Assert.Equal(CreateToolResult.Success, cmMany.Result);
        Assert.Equal(2, await _db.ToolMachineLinks.AsNoTracking().CountAsync(l => l.ToolId == cmMany.Tool!.ToolId));

        // Quantidade contabilística BQ persiste verbatim (facto do lote).
        var bqQty = await handler.HandleAsync(new CreateTool("BQ", "Y232", "4", null, 144));
        Assert.Equal(CreateToolResult.Success, bqQty.Result);
        Assert.Equal(144, bqQty.Tool!.BqAccountedQuantity);
    }

    [Fact]
    public async Task CreateTool_QualquerFormatoDeReferencia_NuncaRecusa()
    {
        // Uma CM com nome próprio ou fora do padrão e uma BQ em minúsculas
        // são REGISTADAS na mesma (registo truthful): o formato nunca é
        // recusado. A nota de formato («CM = dígitos») saiu a 17/10/2026 e
        // o código dela no sweep de 19/10/2026 (3.15).
        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));

        var cmDeviation = await handler.HandleAsync(new CreateTool("CM", "5447-X", "3"));
        Assert.Equal(CreateToolResult.Success, cmDeviation.Result);
        var persistedCm = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == cmDeviation.Tool!.ToolId);
        Assert.Equal("5447-X", persistedCm.Reference);

        var cmNomeProprio = await handler.HandleAsync(new CreateTool("CM", "ST100", "1"));
        Assert.Equal(CreateToolResult.Success, cmNomeProprio.Result);

        var bqLowercase = await handler.HandleAsync(new CreateTool("BQ", "t194", "12"));
        Assert.Equal(CreateToolResult.Success, bqLowercase.Result);

        var canonical = await handler.HandleAsync(new CreateTool("BQ", "T194", "12"));
        Assert.Equal(CreateToolResult.Success, canonical.Result);

        var mf = await handler.HandleAsync(new CreateTool("MF", "qualquer", "1"));
        Assert.Equal(CreateToolResult.Success, mf.Result);
    }

    // ------------------------------------------------------------------
    // EditToolFacts — full-replace no MESMO tool_id.
    // ------------------------------------------------------------------

    [Fact]
    public async Task EditToolFacts_FullReplaceSameToolId_AtomicMachineSubstitution()
    {
        var m1 = await SeedMachineAsync("B1");
        var m2 = await SeedMachineAsync("B2");
        var m3 = await SeedMachineAsync("B3");

        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(
            new CreateTool("CM", "5447", "3", null, null, new[] { m1.Id, m2.Id }));
        Assert.Equal(CreateToolResult.Success, create.Result);
        var toolId = create.Tool!.ToolId;

        var handler = new EditToolFactsHandler(_db);
        var outcome = await handler.HandleAsync(new EditToolFacts(
            toolId, "CM", "5448", "4", "5811", null, new[] { m3.Id }));

        Assert.Equal(EditToolFactsResult.Success, outcome.Result);
        Assert.NotNull(outcome.Tool);

        // O MESMO tool_id: a identidade canónica nunca muda com a edição.
        Assert.Equal(toolId, outcome.Tool!.ToolId);
        Assert.Equal("5448", outcome.Tool.Reference);
        Assert.Equal("4", outcome.Tool.Lot);
        Assert.Equal("5811", outcome.Tool.AssociatedMfReference);

        // Substituição integral das máquinas: as antigas saem, a nova entra.
        var links = await _db.ToolMachineLinks.AsNoTracking()
            .Where(l => l.ToolId == toolId)
            .ToListAsync();
        var machineId = Assert.Single(links).MachineId;
        Assert.Equal(m3.Id, machineId);

        // Persistido exatamente como substituído.
        var persisted = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal("5448", persisted.Reference);
        Assert.Equal("4", persisted.Lot);
    }

    [Fact]
    public async Task EditToolFacts_TypedRefusals_DummyNeverAddressable()
    {
        await SeedDummyAsync();
        var handler = new EditToolFactsHandler(_db);

        // A DUMMY (REMOVED) nunca é endereçável: ToolNotFound.
        var dummy = await handler.HandleAsync(new EditToolFacts(
            ToolRegistryConstants.RemovedToolId, "CM", "5447", "3"));
        Assert.Equal(EditToolFactsResult.ToolNotFound, dummy.Result);

        // Desconhecida idem.
        var unknown = await handler.HandleAsync(new EditToolFacts("Taaaaaaaaaa", "CM", "5447", "3"));
        Assert.Equal(EditToolFactsResult.ToolNotFound, unknown.Result);

        // Factos em falta: recusas tipadas antes de qualquer escrita.
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("BQ", "T194", "12"));
        var toolId = create.Tool!.ToolId;

        var unknownType = await handler.HandleAsync(new EditToolFacts(toolId, "XX", "T194", "12"));
        Assert.Equal(EditToolFactsResult.UnknownTypeToken, unknownType.Result);

        var missingReference = await handler.HandleAsync(new EditToolFacts(toolId, "BQ", " ", "12"));
        Assert.Equal(EditToolFactsResult.MissingReference, missingReference.Result);

        var machineNotFound = await handler.HandleAsync(new EditToolFacts(toolId, "BQ", "T194", "12", null, null, new[] { 999 }));
        Assert.Equal(EditToolFactsResult.MachineNotFound, machineNotFound.Result);

        // Nada foi mutado.
        var persisted = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal("T194", persisted.Reference);
        Assert.Equal("12", persisted.Lot);
    }

    // ------------------------------------------------------------------
    // EditToolTechnicalValues — full-replace do contexto técnico.
    // ------------------------------------------------------------------

    [Fact]
    public async Task EditToolTechnicalValues_RegistersGenesisSheetTpGeometry_NeverRecalculates()
    {
        // Worked example do corte A: os valores REAIS da folha genesis
        // (7151S022(ST78)) — D 28,45 · R 24,11 · sagitta 4,5 · R base
        // 14,225 · R esfera 28,84061800082953 · volume 1438,3860504277206.
        // A app NUNCA recalcula os derivados: o registo é truthful aos
        // valores fornecidos pelo operador, na precisão exata da folha.
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("CM", "5447", "3"));
        var toolId = create.Tool!.ToolId;

        var handler = new EditToolTechnicalValuesHandler(_db);
        var outcome = await handler.HandleAsync(new EditToolTechnicalValues(
            toolId,
            ProcessToken: "NNPB",
            NominalWeightGrams: 520m,
            VolumePuncaoCm3: 1.438m,
            DiametroGargaloMm: 70.00m,
            TpDiametroMm: 28.45m,
            TpRaioMm: 24.11m,
            TpSagittaMm: 4.5m,
            TpRaioBaseMm: 14.225m,
            TpRaioEsferaMm: 28.84061800082953m,
            TpVolumeMm3: 1438.3860504277206m));

        Assert.Equal(EditToolTechnicalValuesResult.Success, outcome.Result);
        Assert.NotNull(outcome.Technical);

        // A linha foi CRIADA (não existia) com TODOS os valores tal
        // qual fornecidos — precisão integral da folha.
        var persisted = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == toolId);
        Assert.Equal(CmProcess.Nnpb, persisted.Process);
        Assert.Equal(520m, persisted.NominalWeightGrams);
        Assert.Equal(1.438m, persisted.VolumePuncaoCm3);
        Assert.Equal(70.00m, persisted.DiametroGargaloMm);
        Assert.Equal(28.45m, persisted.TpDiametroMm);
        Assert.Equal(24.11m, persisted.TpRaioMm);
        Assert.Equal(4.5m, persisted.TpSagittaMm);
        Assert.Equal(14.225m, persisted.TpRaioBaseMm);
        Assert.Equal(28.84061800082953m, persisted.TpRaioEsferaMm);
        Assert.Equal(1438.3860504277206m, persisted.TpVolumeMm3);

        // O read model devolvido projeta exatamente o persistido.
        Assert.Equal(28.84061800082953m, outcome.Technical!.TpRaioEsferaMm);
        Assert.Equal(1438.3860504277206m, outcome.Technical.TpVolumeMm3);
    }

    [Fact]
    public async Task EditToolTechnicalValues_OmittedTpDerivedValues_ArePreserved()
    {
        // beta.37: a ficha só envia D, raio e calote; os derivados antigos
        // gravados (R base, R esfera, volume) nunca se perdem por omissão.
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("CM", "7151", "8"));
        var toolId = create.Tool!.ToolId;
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = toolId,
            TpDiametroMm = 28.45m,
            TpRaioMm = 24.11m,
            TpSagittaMm = 4.5m,
            TpRaioBaseMm = 14.225m,
            TpRaioEsferaMm = 28.84061800082953m,
            TpVolumeMm3 = 1438.3860504277206m
        });
        await _db.SaveChangesAsync();

        var outcome = await new EditToolTechnicalValuesHandler(_db).HandleAsync(new EditToolTechnicalValues(
            toolId, TpDiametroMm: 28.5m, TpRaioMm: 24.11m, TpSagittaMm: 4.7m));

        Assert.Equal(EditToolTechnicalValuesResult.Success, outcome.Result);
        var persisted = await _db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal(28.5m, persisted.TpDiametroMm);
        Assert.Equal(4.7m, persisted.TpSagittaMm);
        Assert.Equal(14.225m, persisted.TpRaioBaseMm);
        Assert.Equal(28.84061800082953m, persisted.TpRaioEsferaMm);
        Assert.Equal(1438.3860504277206m, persisted.TpVolumeMm3);
    }

    [Fact]
    public async Task EditToolTechnicalValues_FullReplaceEmptyClearsToNull()
    {
        // Substituição integral: campo vazio é NULL (o estado «em falta» é
        // válido e nunca é inventado) — um valor antes registado é LIMPARADO.
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("BQ", "T194", "12"));
        var toolId = create.Tool!.ToolId;

        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = toolId,
            VolumeMarisaCm3 = 2.5m,
            DiametroGargaloMm = 60.00m,
            TpDiametroMm = 28.45m
        });
        await _db.SaveChangesAsync();

        var handler = new EditToolTechnicalValuesHandler(_db);
        var outcome = await handler.HandleAsync(new EditToolTechnicalValues(
            toolId,
            ProcessToken: null,
            VolumeMarisaCm3: 2.75m));

        Assert.Equal(EditToolTechnicalValuesResult.Success, outcome.Result);

        var persisted = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == toolId);
        Assert.Null(persisted.Process);
        Assert.Equal(2.75m, persisted.VolumeMarisaCm3);
        Assert.Null(persisted.DiametroGargaloMm);
        // beta.37: numa BQ os campos da CM não são escritos nem apagados.
        Assert.Equal(28.45m, persisted.TpDiametroMm);
        Assert.Null(persisted.NominalWeightGrams);
        Assert.Null(persisted.TpRaioEsferaMm);
    }

    [Fact]
    public async Task EditToolTechnicalValues_TypedRefusals_DummyNeverAddressable()
    {
        await SeedDummyAsync();
        var handler = new EditToolTechnicalValuesHandler(_db);

        var dummy = await handler.HandleAsync(new EditToolTechnicalValues(
            ToolRegistryConstants.RemovedToolId, ProcessToken: "NNPB"));
        Assert.Equal(EditToolTechnicalValuesResult.ToolNotFound, dummy.Result);

        var unknown = await handler.HandleAsync(new EditToolTechnicalValues(
            "Taaaaaaaaaa", ProcessToken: "NNPB"));
        Assert.Equal(EditToolTechnicalValuesResult.ToolNotFound, unknown.Result);

        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("CM", "5447", "3"));
        var unknownProcess = await handler.HandleAsync(new EditToolTechnicalValues(
            create.Tool!.ToolId, ProcessToken: "PRESS"));
        Assert.Equal(EditToolTechnicalValuesResult.UnknownProcessToken, unknownProcess.Result);

        // Recusa antes de qualquer escrita: nenhuma linha técnica existe.
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task EditToolTechnicalValues_ImpossibleValues_AreRefused_NothingSaved()
    {
        // Diogo, 18/10/2026: o que é fisicamente impossível é recusado. Um
        // volume, peso ou medida de 0 ou menos não grava; a calote 0 grava.
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("CM", "5447", "3"));
        var toolId = create.Tool!.ToolId;
        var handler = new EditToolTechnicalValuesHandler(_db);

        Assert.Equal(EditToolTechnicalValuesResult.ImpossibleValue,
            (await handler.HandleAsync(new EditToolTechnicalValues(toolId, VolumePuncaoCm3: -70m))).Result);
        Assert.Equal(EditToolTechnicalValuesResult.ImpossibleValue,
            (await handler.HandleAsync(new EditToolTechnicalValues(toolId, NominalWeightGrams: 0m))).Result);
        Assert.Equal(EditToolTechnicalValuesResult.ImpossibleValue,
            (await handler.HandleAsync(new EditToolTechnicalValues(toolId, TpSagittaMm: -1m))).Result);
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync());

        Assert.Equal(EditToolTechnicalValuesResult.Success,
            (await handler.HandleAsync(new EditToolTechnicalValues(toolId, VolumePuncaoCm3: 70m, TpSagittaMm: 0m))).Result);
    }

    // ------------------------------------------------------------------
    // DuplicateTool — lote novo obrigatório, fonte intacta.
    // ------------------------------------------------------------------

    [Fact]
    public async Task DuplicateTool_CopiesAsEditableDefaults_MintsNewToolId_SourceIntact()
    {
        var machine = await SeedMachineAsync("B1");

        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(
            new CreateTool("CM", "5447", "3", "5810", null, new[] { machine.Id }));
        var sourceId = create.Tool!.ToolId;

        await new EditToolTechnicalValuesHandler(_db).HandleAsync(new EditToolTechnicalValues(
            sourceId,
            ProcessToken: "NNPB",
            NominalWeightGrams: 520m,
            TpDiametroMm: 28.45m,
            TpSagittaMm: 4.5m,
            TpRaioBaseMm: 14.225m,
            TpRaioEsferaMm: 28.84061800082953m,
            TpVolumeMm3: 1438.3860504277206m));

        var handler = new DuplicateToolHandler(_db);
        var outcome = await handler.HandleAsync(new DuplicateTool(sourceId, "8"));

        Assert.Equal(DuplicateToolResult.Success, outcome.Result);
        Assert.NotNull(outcome.Tool);

        // Novo tool_id mintado (nunca herdado da fonte), lote novo,
        // factos copiados como defaults editáveis.
        var newId = outcome.Tool!.ToolId;
        Assert.Matches("^T[0-9a-f]{10}$", newId);
        Assert.NotEqual(sourceId, newId);
        Assert.Equal("8", outcome.Tool.Lot);
        Assert.Equal("5447", outcome.Tool.Reference);
        Assert.Equal("5810", outcome.Tool.AssociatedMfReference);

        // Defaults TÉCNICOS copiados (incluindo a geometria TP) e a
        // ligação de máquina.
        var newTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == newId);
        Assert.Equal(CmProcess.Nnpb, newTechnical.Process);
        Assert.Equal(28.45m, newTechnical.TpDiametroMm);
        Assert.Equal(1438.3860504277206m, newTechnical.TpVolumeMm3);
        var link = await _db.ToolMachineLinks.AsNoTracking().SingleAsync(l => l.ToolId == newId);
        Assert.Equal(machine.Id, link.MachineId);

        // A FONTE fica intacta: mesmo lote, mesma linha técnica, mesmo
        // link — a duplicação nunca transformou a Tool antiga no novo lote.
        var source = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == sourceId);
        Assert.Equal("3", source.Lot);
        Assert.Equal(2, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync());
        Assert.Equal(2, await _db.ToolMachineLinks.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task DuplicateTool_NewLotRequired_DummyNeverDuplicated()
    {
        await SeedDummyAsync();
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("CM", "5447", "3"));
        var sourceId = create.Tool!.ToolId;

        var handler = new DuplicateToolHandler(_db);

        var missingLot = await handler.HandleAsync(new DuplicateTool(sourceId, "  "));
        Assert.Equal(DuplicateToolResult.MissingNewLot, missingLot.Result);

        var dummy = await handler.HandleAsync(new DuplicateTool(ToolRegistryConstants.RemovedToolId, "8"));
        Assert.Equal(DuplicateToolResult.ToolNotFound, dummy.Result);

        var unknown = await handler.HandleAsync(new DuplicateTool("Taaaaaaaaaa", "8"));
        Assert.Equal(DuplicateToolResult.ToolNotFound, unknown.Result);

        // Nada duplicado: só a tool criada e a tombstone existem.
        Assert.Equal(2, await _db.Tools.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Duplicate_ExistingLotIsRefused_EditCollisionStaysRefused_BothMarkTheExisting()
    {
        await SeedDummyAsync();
        var create = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));
        var lot3 = (await create.HandleAsync(new CreateTool("CM", "5447", "3", "5810"))).Tool!.ToolId;
        var lot4 = (await create.HandleAsync(new CreateTool("CM", "5447", "4"))).Tool!.ToolId;
        await RegistosAntigos.EnvelhecerAsync(_db);

        // Duplicar para um lote já registado é recusado (18/10/2026; antes
        // abria a existente), sem copiar nada, e a existente fica com o aviso.
        var duplicate = await new DuplicateToolHandler(_db).HandleAsync(new DuplicateTool(lot3, "4", "Operador 7"));
        Assert.Equal(DuplicateToolResult.LotAlreadyExists, duplicate.Result);
        Assert.Null(duplicate.Tool);
        Assert.Equal(lot4, duplicate.ExistingToolId);
        var existente = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == lot4);
        Assert.Null(existente.AssociatedMfReference);
        Assert.Equal("Operador 7", existente.DuplicateWarningByNameFrozen);

        // A edição que colidiria com outra ferramenta continua recusada
        // (B: partiria a chave), diz qual é a existente e marca-a.
        await new ToolDuplicateWarnings(_db).ClearAsync(lot4);
        var edit = await new EditToolFactsHandler(_db).HandleAsync(new EditToolFacts(lot3, "CM", "5447", "4", null, null, null, RequestedBy: "Diogo"));
        Assert.Equal(EditToolFactsResult.LotAlreadyExists, edit.Result);
        Assert.Equal(lot4, edit.ExistingToolId);
        Assert.Equal("Diogo", (await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == lot4)).DuplicateWarningByNameFrozen);

        // Editar a própria ferramenta sem mudar o lote continua a funcionar.
        var same = await new EditToolFactsHandler(_db).HandleAsync(new EditToolFacts(lot4, "CM", "5447", "4", null, null, null));
        Assert.Equal(EditToolFactsResult.Success, same.Result);

        var negative = await new EditToolFactsHandler(_db).HandleAsync(new EditToolFacts(lot4, "CM", "5447", "4", null, -1, null));
        Assert.Equal(EditToolFactsResult.InvalidLotQuantity, negative.Result);

        Assert.Equal(3, await _db.Tools.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task EditToolFacts_EmptyLot_SavesNoLot_CollisionWithAnotherNoLotStaysRefused()
    {
        // beta.63 (A12): o lote vazio na edição grava «s/lote» (LotDefaulted);
        // se outra ferramenta do mesmo tipo e referência já é «s/lote», a
        // edição é recusada (B) com o tool_id da existente.
        await SeedDummyAsync();
        var create = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));
        var lot3 = (await create.HandleAsync(new CreateTool("BQ", "T194", "3"))).Tool!.ToolId;
        var lot4 = (await create.HandleAsync(new CreateTool("BQ", "T194", "4"))).Tool!.ToolId;
        var handler = new EditToolFactsHandler(_db);

        var cleared = await handler.HandleAsync(new EditToolFacts(lot3, "BQ", "T194", "  ", null, 120));
        Assert.Equal(EditToolFactsResult.Success, cleared.Result);
        Assert.Equal(ToolRegistryConstants.NoLot, cleared.Tool!.Lot);
        Assert.True(cleared.Tool.LotDefaulted);
        var persisted = await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == lot3);
        Assert.Equal(ToolRegistryConstants.NoLot, persisted.Lot);
        Assert.Equal(120, persisted.BqAccountedQuantity);

        var collision = await handler.HandleAsync(new EditToolFacts(lot4, "BQ", "T194", ""));
        Assert.Equal(EditToolFactsResult.LotAlreadyExists, collision.Result);
        Assert.Equal(lot3, collision.ExistingToolId);
        Assert.Equal("4", (await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == lot4)).Lot);
    }

    // ------------------------------------------------------------------
    // DeleteTool — a transação canónica (worked example do fluxo DUMMY).
    // ------------------------------------------------------------------

    [Fact]
    public async Task DeleteTool_TransactionalDummyFlow_PreservesHistoryAndPhysicalDeletes()
    {
        // O cenário canónico: uma Tool COM histórico de consumo (Pesos,
        // Pegamentos, trace não resolvido, linha técnica, ligação de
        // máquina, contexto de produção) é eliminada — os registos de
        // consumo são reatribuídos à DUMMY com os factos congelados
        // preservados, a âncora do trace é limpa e a Tool desaparece
        // FISICAMENTE. As produções (CM, MF, BQ) e as mudanças de lote
        // passam também para a DUMMY: a base liga-as às ferramentas.
        await SeedDummyAsync();
        var machine = await SeedMachineAsync("B1");

        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(
            new CreateTool("CM", "5447", "3", null, null, new[] { machine.Id }));
        var toolId = create.Tool!.ToolId;
        var otherBq = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("BQ", "T194", "12"));
        var bqId = otherBq.Tool!.ToolId;

        // Registo de consumo ANTERIOR às colunas congeladas (backfill):
        // um Peso cujos factos humanos estão ainda a NULL.
        _db.Pesos.Add(new Peso
        {
            CmToolId = toolId,
            BqToolId = bqId,
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 2.5m,
            VolumePuncaoCm3 = 1.4m,
            GlassDensityGramsPerCm3 = 2.5m
        });

        // Registo de Pegamentos que referencia a tool como MF.
        _db.Pegamentos.Add(new Pegamentos
        {
            ControloId = 1,
            MfToolId = toolId,
            ToleranceMm = 0.20m
        });

        // Trace pré-produção não resolvido ancorado diretamente à tool.
        _db.BqRepairTraces.Add(new BqRepairTrace { ToolId = toolId, BqContextId = null });

        // Contexto técnico e contexto de produção (string pendurada).
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = toolId,
            TpDiametroMm = 28.45m,
            TpVolumeMm3 = 1438.3860504277206m
        });
        _db.JobOns.Add(new JobOn
        {
            Reference = "5810",
            ProductionNumber = "J10",
            Machine = machine
        });
        await _db.SaveChangesAsync();
        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync();
        _db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = toolId });
        _db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = toolId });
        _db.BqContexts.Add(new BqContext { JobOnId = jobOn.Id, ToolId = bqId });
        _db.JobOnLotNotes.Add(new JobOnLotNote
        {
            JobOnId = jobOn.Id,
            Dia = new DateOnly(2026, 10, 17),
            Slot = "CM",
            NewToolId = toolId,
            ToolReferenceFrozen = "5447",
            NewLotFrozen = "3"
        });
        await _db.SaveChangesAsync();

        // (1) ELIMINAR: confirmação explícita do aviso.
        var handler = new DeleteToolHandler(_db);
        var outcome = await handler.HandleAsync(new DeleteTool(toolId));
        Assert.Equal(DeleteToolResult.Success, outcome.Result);

        // A Tool desapareceu FISICAMENTE; a DUMMY permanece (tombstone).
        Assert.Null(await _db.Tools.AsNoTracking().FirstOrDefaultAsync(t => t.ToolId == toolId));
        Assert.NotNull(await _db.Tools.AsNoTracking().SingleAsync(
            t => t.ToolId == ToolRegistryConstants.RemovedToolId));

        // (2) O Peso foi reatribuído à DUMMY COM o backfill dos factos
        // congelados em falta: a referência/lote da tool apagada ficam
        // congeladas como evidência histórica (a outra âncora intacta).
        var peso = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal(ToolRegistryConstants.RemovedToolId, peso.CmToolId);
        Assert.Equal(bqId, peso.BqToolId);
        Assert.Equal("5447", peso.CmToolReferenceFrozen);
        Assert.Equal("3", peso.CmToolLotFrozen);
        Assert.Null(peso.BqToolReferenceFrozen);

        // (3) O registo de Pegamentos idem (componente MF).
        var registro = await _db.Pegamentos.AsNoTracking().SingleAsync();
        Assert.Equal(ToolRegistryConstants.RemovedToolId, registro.MfToolId);
        Assert.Equal("5447", registro.MfToolReferenceFrozen);
        Assert.Equal("3", registro.MfToolLotFrozen);

        // (4) O trace não resolvido perdeu a âncora direta (ToolId NULL —
        // o índice único parcial aceita NULLs) e congelou os factos.
        var trace = await _db.BqRepairTraces.AsNoTracking().SingleAsync();
        Assert.Null(trace.ToolId);
        Assert.Equal("5447", trace.ToolReferenceFrozen);
        Assert.Equal("3", trace.ToolLotFrozen);

        // (5) A ficha técnica (PK 1:1 colidiria na DUMMY) e as ligações
        // de máquina foram APAGADAS com a tool.
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync(t => t.ToolId == toolId));
        Assert.Equal(0, await _db.ToolMachineLinks.AsNoTracking().CountAsync(l => l.ToolId == toolId));

        // (6) O contexto de produção passa para a DUMMY, como os registos
        // de consumo: a FK Restrict não aceita um tool_id pendurado.
        var cmContext = await _db.CmContexts.AsNoTracking().SingleAsync();
        Assert.Equal(ToolRegistryConstants.RemovedToolId, cmContext.ToolId);
        Assert.Equal(jobOn.Id, cmContext.JobOnId);
        var mfContext = await _db.MfContexts.AsNoTracking().SingleAsync();
        Assert.Equal(ToolRegistryConstants.RemovedToolId, mfContext.ToolId);

        // (7) A BQ da mesma produção não muda: só a ferramenta apagada sai.
        var bqContext = await _db.BqContexts.AsNoTracking().SingleAsync();
        Assert.Equal(bqId, bqContext.ToolId);

        // (8) A mudança de lote guarda a referência congelada e aponta
        // para a DUMMY.
        var note = await _db.JobOnLotNotes.AsNoTracking().SingleAsync();
        Assert.Equal(ToolRegistryConstants.RemovedToolId, note.NewToolId);
        Assert.Equal("5447", note.ToolReferenceFrozen);
    }

    [Fact]
    public async Task DeleteTool_ExistingFrozenFactsArePreserved_BackfillOnlyFillsMissing()
    {
        // Um registo de consumo criado DEPOIS destas colunas já congelou os
        // factos à criação: o backfill ??= nunca os reescreve.
        await SeedDummyAsync();
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("CM", "5447", "3"));
        var toolId = create.Tool!.ToolId;

        _db.Pesos.Add(new Peso
        {
            CmToolId = toolId,
            BqToolId = toolId,
            Process = "PS",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 2.5m,
            VolumePuncaoCm3 = 1.4m,
            GlassDensityGramsPerCm3 = 2.5m,
            CmToolReferenceFrozen = "5448-corrigida",
            CmToolLotFrozen = "3",
            BqToolReferenceFrozen = "T194",
            BqToolLotFrozen = "12"
        });
        await _db.SaveChangesAsync();

        var outcome = await new DeleteToolHandler(_db).HandleAsync(new DeleteTool(toolId));
        Assert.Equal(DeleteToolResult.Success, outcome.Result);

        var peso = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal("5448-corrigida", peso.CmToolReferenceFrozen);
        Assert.Equal("3", peso.CmToolLotFrozen);
        Assert.Equal("T194", peso.BqToolReferenceFrozen);
        Assert.Equal("12", peso.BqToolLotFrozen);
        Assert.Equal(ToolRegistryConstants.RemovedToolId, peso.CmToolId);
        Assert.Equal(ToolRegistryConstants.RemovedToolId, peso.BqToolId);
    }

    [Fact]
    public async Task DeleteTool_TypedRefusals_DummyAndUnknown()
    {
        await SeedDummyAsync();

        var handler = new DeleteToolHandler(_db);

        // A DUMMY nunca é eliminável: é a tombstone que recebe as
        // reatribuições.
        var dummy = await handler.HandleAsync(new DeleteTool(ToolRegistryConstants.RemovedToolId));
        Assert.Equal(DeleteToolResult.ToolNotFound, dummy.Result);

        var unknown = await handler.HandleAsync(new DeleteTool("Taaaaaaaaaa"));
        Assert.Equal(DeleteToolResult.ToolNotFound, unknown.Result);

        // A tombstone permanece.
        Assert.Equal(1, await _db.Tools.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task DeleteTool_WithoutDummyRow_ThrowsLoudDeploymentError()
    {
        // Sem a linha DUMMY semeada (migração #7 não aplicada), a
        // reatribuição é impossível: erro de implantação LOUD, nunca uma
        // escrita silenciosa que perca o histórico.
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("CM", "5447", "3"));
        var toolId = create.Tool!.ToolId;

        var handler = new DeleteToolHandler(_db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new DeleteTool(toolId)));
        Assert.Contains("REMOVED", ex.Message);

        // Nada foi apagado nem reatribuído.
        Assert.NotNull(await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId));
    }
    [Fact]
    public async Task EditBq_SeveralMachines_ReplacesCompatibilityWithoutChangingIdentity()
    {
        var b1 = await SeedMachineAsync("B1");
        var c3 = await SeedMachineAsync("C3");
        var created = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("BQ", "Y232", "66", MachineIds: new[] { b1.Id }));
        var id = created.Tool!.ToolId;
        var edited = await new EditToolFactsHandler(_db).HandleAsync(new EditToolFacts(id, "BQ", "Y232", "66", MachineIds: new[] { b1.Id, c3.Id, c3.Id }));
        Assert.Equal(EditToolFactsResult.Success, edited.Result);
        Assert.Equal(2, await _db.ToolMachineLinks.CountAsync(l => l.ToolId == id));
        Assert.Single(await _db.Tools.Where(t => t.ToolId == id).ToListAsync());
    }

    [Fact]
    public async Task EditToolFacts_MudarOTipoDeUmaFerramentaUsada_GravaEContaAsProducoesDoTipoAnterior()
    {
        // Sweep de 19/10/2026, F-1: o tipo de uma BQ usada mudava sem aviso.
        // Grava (o Editar corrige registos errados) e devolve o tipo anterior
        // com as produções que a usaram como BQ, para a ficha avisar.
        var machine = await SeedMachineAsync("B1");
        var create = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(
            new CreateTool("BQ", "T194", "12", null, null, new[] { machine.Id }));
        var toolId = create.Tool!.ToolId;
        _db.JobOns.AddRange(
            new JobOn { Reference = "5447T194", ProductionNumber = "J1", Machine = machine },
            new JobOn { Reference = "5447T194", ProductionNumber = "J2", Machine = machine });
        await _db.SaveChangesAsync();
        var jobOns = await _db.JobOns.AsNoTracking().OrderBy(j => j.Id).Select(j => j.Id).ToListAsync();
        // Numa produção está em uso; na outra foi trocada por outra BQ.
        _db.BqContexts.AddRange(
            new BqContext { JobOnId = jobOns[0], ToolId = toolId },
            new BqContext { ReplacedJobOnId = jobOns[1], ReplacedAtUtc = DateTime.UtcNow, ToolId = toolId });
        await _db.SaveChangesAsync();
        var handler = new EditToolFactsHandler(_db);

        var paraCm = await handler.HandleAsync(new EditToolFacts(toolId, "CM", "T194", "12", MachineIds: new[] { machine.Id }));
        Assert.Equal(EditToolFactsResult.Success, paraCm.Result);
        Assert.Equal(ToolType.Cm, (await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId)).Type);
        Assert.Equal(ToolType.Bq, paraCm.Tool!.PreviousType);
        Assert.Equal(2, paraCm.Tool.PreviousTypeProductions);

        // Voltar a BQ repõe tudo: como CM não foi usada em produção nenhuma.
        var deVolta = await handler.HandleAsync(new EditToolFacts(toolId, "BQ", "T194", "12", MachineIds: new[] { machine.Id }));
        Assert.Equal(ToolType.Cm, deVolta.Tool!.PreviousType);
        Assert.Equal(0, deVolta.Tool.PreviousTypeProductions);

        // Sem mudar o tipo, nada a avisar.
        var mesmoTipo = await handler.HandleAsync(new EditToolFacts(toolId, "BQ", "T194", "13", MachineIds: new[] { machine.Id }));
        Assert.Null(mesmoTipo.Tool!.PreviousType);
        Assert.Equal(0, mesmoTipo.Tool.PreviousTypeProductions);
    }
}
