using DMO.Alpha.Core.Tools;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// Prova o surface de descoberta de candidatos IToolCandidateQuery: o
/// ToolId devolvido é sempre a identidade canónica persistida (nunca
/// derivada de referência/lote/tipo); candidatos com a mesma referência e
/// lotes diferentes nunca colapsam; a filtragem apenas reduz o conjunto de
/// candidatos (incluindo as dimensões do corte A: máquina via ligações
/// Tool–máquina e referência MF/produção pela referência canónica OU pela
/// associação explícita); a query nunca auto-seleciona uma Tool (um único
/// candidato que resta continua a ser uma lista por selecionar por ato
/// humano explícito); valores de filtro desconhecidos devolvem uma lista
/// vazia sem inferir identidade; os factos projetados são exatamente os
/// valores persistidos; a ordenação determinística canónica é pelos
/// FATOS HUMANOS (Type → Reference → Lot, tool_id só como desempate) e a
/// linha DUMMY (REMOVED) é SEMPRE excluída por constante. É estritamente
/// só de leitura (AsNoTracking, sem SaveChanges).
/// </summary>
public sealed class ToolCandidateQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly ToolCandidateQuery _query;
    private readonly ToolLookupQuery _lookup;

    public ToolCandidateQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new ToolCandidateQuery(_db);
        _lookup = new ToolLookupQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>
    /// Registo misto com os três tipos em scope, a mesma referência em
    /// lotes diferentes (identidades canónicas distintas) e referências
    /// distintas no mesmo tipo.
    /// </summary>
    private async Task SeedRegistryAsync()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "9389T194", Lot = "12" },
            new Tool { ToolId = "BQ-TOOL-10", Type = ToolType.Bq, Reference = "9389T194", Lot = "13" },
            new Tool { ToolId = "BQ-TOOL-11", Type = ToolType.Bq, Reference = "7430C092", Lot = "4" },
            new Tool { ToolId = "CM-TOOL-1", Type = ToolType.Cm, Reference = "5809", Lot = "L1" },
            new Tool { ToolId = "MF-TOOL-1", Type = ToolType.Mf, Reference = "5810", Lot = "L2" });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task FindCandidatesAsync_NoFilterApplied_ReturnsEveryRegisteredToolWithCanonicalPersistedIdentity()
    {
        await SeedRegistryAsync();

        // Filtro null: nenhuma dimensão aplicada — o registo completo de
        // candidatos, sem exceção de argumento.
        var candidates = await _query.FindCandidatesAsync(filter: null);

        Assert.Equal(5, candidates.Count);

        // Cada ToolId devolvido é exatamente o tool_id persistido (a chave
        // canónica emitida pelo backend), reconciliado contra as linhas
        // persistidas: nunca um identificador sintetizado.
        var persistedIds = await _db.Tools.AsNoTracking().Select(tool => tool.ToolId).ToListAsync();
        Assert.Equal(persistedIds.ToHashSet(), candidates.Select(c => c.ToolId).ToHashSet());

        // Ordenação canónica do corte A: pelos FATOS HUMANOS
        // (Type → Reference → Lot; o tool_id é apenas o desempate final).
        // A ordem DO TIPO é a ordenação do valor persistido no provider
        // (valor do enum na seam InMemory destes testes: CM < MF < BQ; o
        // token textual em PostgreSQL); dentro do tipo, a referência
        // 7430C092 vem antes de 9389T194 e o lote 12 antes do 13 — a
        // parte significativa para o humano é provider-stable.
        Assert.Equal(
            new[] { "CM-TOOL-1", "MF-TOOL-1", "BQ-TOOL-11", "BQ-TOOL-9", "BQ-TOOL-10" },
            candidates.Select(c => c.ToolId).ToArray());

        foreach (var candidate in candidates)
        {
            var persisted = await _db.Tools.AsNoTracking().SingleAsync(tool => tool.ToolId == candidate.ToolId);
            Assert.Equal(persisted.ToolId, candidate.ToolId);
            Assert.Equal(persisted.Type, candidate.Type);
            Assert.Equal(persisted.Reference, candidate.Reference);
            Assert.Equal(persisted.Lot, candidate.Lot);
        }
    }

    [Fact]
    public async Task FindCandidatesAsync_ExactPersistedValuesAreProjectedForEveryType()
    {
        await SeedRegistryAsync();

        var candidates = await _query.FindCandidatesAsync(filter: null);

        // Projeção exata dos factos persistidos, incluindo o round-trip do
        // ToolType convertido em valor (exatamente CM, MF e BQ).
        Assert.Contains(candidates, c =>
            c.ToolId == "BQ-TOOL-9" && c.Type == ToolType.Bq && c.Reference == "9389T194" && c.Lot == "12");
        Assert.Contains(candidates, c =>
            c.ToolId == "CM-TOOL-1" && c.Type == ToolType.Cm && c.Reference == "5809" && c.Lot == "L1");
        Assert.Contains(candidates, c =>
            c.ToolId == "MF-TOOL-1" && c.Type == ToolType.Mf && c.Reference == "5810" && c.Lot == "L2");
    }

    [Fact]
    public async Task FindCandidatesAsync_SameReferenceWithDifferentLots_NeverCollapsesIntoOneCandidate()
    {
        await SeedRegistryAsync();

        // A mesma referência em lotes diferentes são Tools canónicas
        // distintas: a descoberta devolve ambas como candidatos — o humano
        // é quem distingue pelo lote e seleciona o tool_id exato. A
        // ordenação canónica põe o lote 12 antes do 13 (fatos humanos).
        var byReference = await _query.FindCandidatesAsync(new ToolCandidateFilter(Reference: "9389T194"));

        Assert.Equal(2, byReference.Count);
        Assert.Equal(["BQ-TOOL-9", "BQ-TOOL-10"], byReference.Select(c => c.ToolId));
        Assert.All(byReference, c => Assert.Equal("9389T194", c.Reference));
        Assert.Equal(["12", "13"], byReference.Select(c => c.Lot));

        // A referência comum nunca vira unicidade nem chave derivada.
        Assert.NotEqual(byReference[0].ToolId, byReference[1].ToolId);
    }

    [Fact]
    public async Task FindCandidatesAsync_EachFilterDimensionOnlyReducesTheCandidateSet()
    {
        await SeedRegistryAsync();

        var all = await _query.FindCandidatesAsync(filter: null);
        var bqOnly = await _query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Bq));
        var bqByReference = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, Reference: "9389T194"));
        var bqByReferenceAndLot = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, Reference: "9389T194", Lot: "12"));

        // Cada dimensão aplicada reduz (ou mantém) o conjunto anterior —
        // nunca o amplia e nunca devolve candidatos fora do filtro.
        Assert.Equal(5, all.Count);
        Assert.Equal(3, bqOnly.Count);
        Assert.Equal(2, bqByReference.Count);
        Assert.Single(bqByReferenceAndLot);

        Assert.All(bqOnly, c => Assert.Equal(ToolType.Bq, c.Type));
        Assert.All(bqByReference, c => Assert.Equal(ToolType.Bq, c.Type));
        Assert.All(bqByReference, c => Assert.Equal("9389T194", c.Reference));
        Assert.All(bqByReferenceAndLot, c => Assert.Equal(ToolType.Bq, c.Type));
        Assert.All(bqByReferenceAndLot, c => Assert.Equal("9389T194", c.Reference));
        Assert.All(bqByReferenceAndLot, c => Assert.Equal("12", c.Lot));

        // Relação de subconjunto: filtrar apenas remove candidatos.
        var allIds = all.Select(c => c.ToolId).ToHashSet();
        var bqIds = bqOnly.Select(c => c.ToolId).ToHashSet();
        var bqRefIds = bqByReference.Select(c => c.ToolId).ToHashSet();
        var bqRefLotIds = bqByReferenceAndLot.Select(c => c.ToolId).ToHashSet();
        Assert.Subset(allIds, bqIds);
        Assert.Subset(bqIds, bqRefIds);
        Assert.Subset(bqRefIds, bqRefLotIds);
    }

    [Fact]
    public async Task ReferenceContains_MatchesTheStartOfTheReferenceOfItsBqPartOrOfTheAssociatedMf()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-Y232", Type = ToolType.Bq, Reference = "Y232", Lot = "1" },
            new Tool { ToolId = "BQ-T194", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = "CM-6018Y232", Type = ToolType.Cm, Reference = "6018Y232", Lot = "1" },
            new Tool { ToolId = "CM-0132T173", Type = ToolType.Cm, Reference = "0132T173", Lot = "1" },
            new Tool { ToolId = "CM-1075", Type = ToolType.Cm, Reference = "1075", Lot = "1" },
            new Tool { ToolId = "CM-ST100", Type = ToolType.Cm, Reference = "ST100", Lot = "1", AssociatedMfReference = "6018" });
        await _db.SaveChangesAsync();

        async Task<string[]> Find(string text) => (await _query.FindCandidatesAsync(new ToolCandidateFilter(ReferenceContains: text)))
            .Select(c => c.ToolId).OrderBy(id => id).ToArray();

        // Diogo, 18/10/2026: «Y2» encontra a Y232 e a 6018Y232 (a parte BQ);
        // «0» só a 0132T173, nunca a 1075 por ter um 0 no meio; «60» pela
        // referência ou pela referência MF associada.
        Assert.Equal(new[] { "BQ-Y232", "CM-6018Y232" }, await Find("y2"));
        Assert.Equal(new[] { "CM-0132T173" }, await Find("0"));
        Assert.Equal(new[] { "CM-6018Y232", "CM-ST100" }, await Find("60"));
    }

    // Deep sweep 2 (18/10/2026, F-2): a procura do catálogo das Boquilhas e
    // a do Job On (CM/MF/BQ) encontravam a referência pelo meio («19»
    // trazia a T194). A referência segue a mesma regra; o lote e a máquina
    // continuam por parte do texto.
    [Fact]
    public async Task Search_ReferenceByItsStartOrBqPart_LotAndMachineByPart()
    {
        var machine = new Machine { Code = "ZC9" };
        _db.Machines.Add(machine);
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-T194", Type = ToolType.Bq, Reference = "T194", Lot = "7" },
            new Tool { ToolId = "BQ-Y232", Type = ToolType.Bq, Reference = "Y232", Lot = "1935" },
            new Tool { ToolId = "CM-0132T173", Type = ToolType.Cm, Reference = "0132T173", Lot = "1" },
            new Tool { ToolId = "CM-1075", Type = ToolType.Cm, Reference = "1075", Lot = "2" });
        await _db.SaveChangesAsync();
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "CM-1075", MachineId = machine.Id });
        await _db.SaveChangesAsync();

        async Task<string[]> Find(string text) => (await _query.FindCandidatesAsync(new ToolCandidateFilter(Search: text)))
            .Select(c => c.ToolId).OrderBy(id => id).ToArray();

        Assert.Equal(new[] { "CM-0132T173" }, await Find("0"));
        Assert.Equal(new[] { "BQ-Y232" }, await Find("19"));       // pelo lote 1935, não pela T194
        Assert.Equal(new[] { "BQ-T194" }, await Find("t19"));
        Assert.Equal(new[] { "BQ-Y232" }, await Find("Y2"));
        Assert.Equal(new[] { "CM-0132T173" }, await Find("T17"));
        Assert.Empty(await Find("232"));
        Assert.Equal(new[] { "CM-1075" }, await Find("zc"));       // pela máquina
        Assert.Empty(await Find("075"));                            // nunca a meio da 1075
    }

    // Verificação antes da apresentação (25/10/2026): na B1, com o lote 3 ligado,
    // o lote 4 sem linhas nunca aparecia. Com IncluirSemLinha aparecem as da linha
    // e as sem linha nenhuma; a de outra linha continua de fora, e sem a opção
    // o filtro de linha fica como era.
    [Fact]
    public async Task MachineFilter_IncluirSemLinha_JuntaAsSemLinha_NuncaAsDeOutraLinha()
    {
        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        _db.Machines.AddRange(b1, b2);
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-L3", Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "CM-L4", Type = ToolType.Cm, Reference = "5447T173", Lot = "4" },
            new Tool { ToolId = "CM-L5", Type = ToolType.Cm, Reference = "5447T173", Lot = "5" });
        await _db.SaveChangesAsync();
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = "CM-L3", MachineId = b1.Id },
            new ToolMachineLink { ToolId = "CM-L5", MachineId = b2.Id });
        await _db.SaveChangesAsync();

        async Task<string[]> Find(bool semLinha) => (await _query.FindCandidatesAsync(
                new ToolCandidateFilter(Type: ToolType.Cm, MachineId: b1.Id, Search: "5447", IncluirSemLinha: semLinha)))
            .Select(c => c.ToolId).OrderBy(id => id).ToArray();

        Assert.Equal(new[] { "CM-L3", "CM-L4" }, await Find(true));
        Assert.Equal(new[] { "CM-L3" }, await Find(false));
    }

    // O Diogo (26/10/2026): «um CM já inserido deveria filtrar com base no que faz
    // parte relacionando máquina e referência total». Com a CM escolhida, a BQ usada
    // com ela vem primeiro, depois a usada nesta referência, depois a da linha; sem
    // nada escrito (SoRelacionadas), as outras ficam de fora até se escrever.
    [Fact]
    public async Task JobOn_Companheiras_OrdenamPrimeiroAsUsadasComElas_ESoRelacionadasTiraAsOutras()
    {
        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        _db.Machines.AddRange(b1, b2);
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-12", Type = ToolType.Cm, Reference = "5447", Lot = "12" },
            new Tool { ToolId = "BQ-01", Type = ToolType.Bq, Reference = "T173", Lot = "1" },   // só ligada à B1
            new Tool { ToolId = "BQ-02", Type = ToolType.Bq, Reference = "T173", Lot = "2" },   // usada noutra 5447T173
            new Tool { ToolId = "BQ-03", Type = ToolType.Bq, Reference = "T173", Lot = "3" },   // usada com a CM 12
            new Tool { ToolId = "BQ-04", Type = ToolType.Bq, Reference = "T173", Lot = "4" },   // sem linha nem uso
            new Tool { ToolId = "BQ-05", Type = ToolType.Bq, Reference = "T173", Lot = "5" });  // usada na 0132T173
        await _db.SaveChangesAsync();
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = "BQ-01", MachineId = b1.Id },
            new ToolMachineLink { ToolId = "BQ-05", MachineId = b2.Id });
        var comCm = new JobOn { Reference = "5447T173", ProductionNumber = "202601", MachineId = b2.Id };
        var outra = new JobOn { Reference = "5447T173", ProductionNumber = "202602", MachineId = b2.Id };
        var outraReferencia = new JobOn { Reference = "0132T173", ProductionNumber = "202603", MachineId = b2.Id };
        _db.JobOns.AddRange(comCm, outra, outraReferencia);
        await _db.SaveChangesAsync();
        _db.CmContexts.Add(new CmContext { JobOnId = comCm.Id, ToolId = "CM-12" });
        _db.BqContexts.AddRange(
            new BqContext { ReplacedJobOnId = comCm.Id, ToolId = "BQ-03" },
            new BqContext { JobOnId = outra.Id, ToolId = "BQ-02" },
            new BqContext { JobOnId = outraReferencia.Id, ToolId = "BQ-05" });
        await _db.SaveChangesAsync();

        async Task<string[]> Find(bool soRelacionadas) => (await _query.FindCandidatesAsync(new ToolCandidateFilter(
                Type: ToolType.Bq, MachineId: b1.Id, ProductionReference: "5447T173", IncluirSemLinha: true,
                CompanheiraToolIds: new[] { "CM-12" }, ReferenciaTotal: "5447T173", SoRelacionadas: soRelacionadas)))
            .Select(c => c.ToolId).ToArray();

        // A da B2 (BQ-05) fica de fora pela linha, como antes.
        Assert.Equal(new[] { "BQ-03", "BQ-02", "BQ-01", "BQ-04" }, await Find(false));
        Assert.Equal(new[] { "BQ-03", "BQ-02", "BQ-01" }, await Find(true));
    }

    [Fact]
    public async Task FindCandidatesAsync_SingleRemainingCandidate_StaysAnUnselectedCandidate_NeverAutoSelects()
    {
        await SeedRegistryAsync();

        // A filtragem deixa exatamente um candidato: o contrato continua a
        // devolver uma LISTA de candidatos — não um candidato resolvido, não
        // um marcador de seleção, não um tool_id inferido. A seleção é um
        // ato humano explícito posterior; a query nunca a antecipa.
        var candidates = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, Reference: "9389T194", Lot: "12"));

        var single = Assert.Single(candidates);

        // O tool_id devolvido é o persistido, não uma derivação de
        // referência + lote + tipo.
        Assert.Equal("BQ-TOOL-9", single.ToolId);

        // O candidato resolve de volta à mesma Tool canónica pelo surface
        // de lookup exato — a identidade continua canónica e persistida.
        var resolved = await _lookup.FindByToolIdAsync(single.ToolId);
        Assert.NotNull(resolved);
        Assert.Equal(single.ToolId, resolved.ToolId);
        Assert.Equal(single.Reference, resolved.Reference);
        Assert.Equal(single.Lot, resolved.Lot);
    }

    [Fact]
    public async Task FindCandidatesAsync_UnknownFilterValues_ReturnEmptyList_NeverInferIdentity()
    {
        await SeedRegistryAsync();

        // Valores que não correspondem a nada persistido devolvem uma
        // lista vazia — nunca null, nunca exceção, nunca a "melhor
        // correspondência" nem um tool_id adivinhado.
        var unknownReference = await _query.FindCandidatesAsync(new ToolCandidateFilter(Reference: "UNKNOWN-REF"));
        var knownReferenceUnknownLot = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Reference: "9389T194", Lot: "99"));
        var knownReferenceDifferentType = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Type: ToolType.Cm, Reference: "9389T194"));

        Assert.Empty(unknownReference);
        Assert.Empty(knownReferenceUnknownLot);
        Assert.Empty(knownReferenceDifferentType);

        // Uma dimensão conhecida com valor sem correspondência reduz a
        // zero candidatos sem inferir nada: nunca há fallback parcial para
        // as outras dimensões nem escolha do "mais próximo".
        var bqUnknownLot = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, Reference: "9389T194", Lot: "99"));
        Assert.Empty(bqUnknownLot);
    }

    [Theory]
    [InlineData("9389t194")]   // sem case-insensitivity
    [InlineData(" 9389T194")]  // sem trimming na correspondência
    [InlineData("9389T19")]    // sem prefixo/contains
    public async Task FindCandidatesAsync_InexactReferenceValues_DoNotMatch(string reference)
    {
        await SeedRegistryAsync();

        // Nenhuma semântica difusa: apenas a igualdade exata ao valor
        // persistido corresponde.
        var candidates = await _query.FindCandidatesAsync(new ToolCandidateFilter(Reference: reference));

        Assert.Empty(candidates);
    }

    [Theory]
    [InlineData("12 ")]  // sem trimming
    [InlineData(" 12")]  // sem trimming
    [InlineData("1")]    // sem prefixo/contains
    public async Task FindCandidatesAsync_InexactLotValues_DoNotMatch(string lot)
    {
        await SeedRegistryAsync();

        var candidates = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, Reference: "9389T194", Lot: lot));

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task FindCandidatesAsync_BlankFilterDimensions_AreTreatedAsNotApplied_NotAsInexactMatches()
    {
        await SeedRegistryAsync();

        // Valores nulos/vazios/whitespace significam "dimensão não
        // aplicada" (estilo do IToolLookupQuery com valores
        // desconhecidos): não lançam exceção, não filtram a zero e não são
        // interpretados como um valor a corresponder.
        var emptyDimensions = await _query.FindCandidatesAsync(new ToolCandidateFilter(Reference: "", Lot: "   "));
        Assert.Equal(5, emptyDimensions.Count);

        var nullDimensions = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, Reference: null, Lot: null));
        Assert.Equal(3, nullDimensions.Count);
    }

    [Fact]
    public async Task FindCandidatesAsync_IsReadOnly_DoesNotTrackOrMutate()
    {
        await SeedRegistryAsync();

        // Destaca as entidades semeadas para que qualquer estado de
        // tracking observado adiante provenha apenas da query em teste.
        _db.ChangeTracker.Clear();

        var candidates = await _query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Bq));

        Assert.Equal(3, candidates.Count);

        // AsNoTracking: nenhuma Tool fica rastreada e não há entradas
        // Added/Modified/Deleted — a query nunca muta estado.
        Assert.Empty(_db.ChangeTracker.Entries<Tool>());
        Assert.DoesNotContain(
            _db.ChangeTracker.Entries(),
            entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        // As linhas persistidas mantêm-se inalteradas.
        var persistedCount = await _db.Tools.AsNoTracking().CountAsync();
        Assert.Equal(5, persistedCount);
        var persistedBq = await _db.Tools.AsNoTracking().SingleAsync(tool => tool.ToolId == "BQ-TOOL-9");
        Assert.Equal(ToolType.Bq, persistedBq.Type);
        Assert.Equal("9389T194", persistedBq.Reference);
        Assert.Equal("12", persistedBq.Lot);
    }

    [Fact]
    public async Task FindCandidatesAsync_IsDeterministic_CanonicalFactsOrdering()
    {
        await SeedRegistryAsync();

        var first = await _query.FindCandidatesAsync(filter: null);
        var second = await _query.FindCandidatesAsync(filter: null);

        // A mesma execução devolve a mesma sequência, ordenada de forma
        // determinística pelos FATOS HUMANOS (Type → Reference → Lot, com
        // o tool_id como desempate final — a identidade opaca nunca é a
        // chave de leitura). A ordem do tipo é a do valor persistido no
        // provider (enum na seam InMemory: CM < MF < BQ).
        Assert.Equal(first, second);
        Assert.Equal(
            new[] { "CM-TOOL-1", "MF-TOOL-1", "BQ-TOOL-11", "BQ-TOOL-9", "BQ-TOOL-10" },
            first.Select(c => c.ToolId).ToArray());

        // A ordenação canónica mantém-se com filtros aplicados: dentro do
        // tipo BQ, a referência 7430C092 vem antes de 9389T194 e o lote 12
        // antes do 13.
        var filtered = await _query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Bq));
        Assert.Equal(
            new[] { "BQ-TOOL-11", "BQ-TOOL-9", "BQ-TOOL-10" },
            filtered.Select(c => c.ToolId).ToArray());
    }

    [Fact]
    public async Task FindCandidatesAsync_DummyRemovedLine_IsNeverACandidate()
    {
        await SeedRegistryAsync();

        // A linha DUMMY canónica (REMOVED) semeada pela migração #7 é
        // excluída por CONSTANTE em TODAS as queries de descoberta: nunca
        // é candidata à seleção humana (a tombstone recebe as reatribuições
        // do delete e nunca é uma Tool utilizável).
        _db.Tools.Add(new Tool
        {
            ToolId = ToolRegistryConstants.RemovedToolId,
            Type = ToolType.Cm,
            Reference = "REMOVED",
            Lot = "REMOVED"
        });
        await _db.SaveChangesAsync();

        var all = await _query.FindCandidatesAsync(filter: null);
        Assert.Equal(5, all.Count);
        Assert.DoesNotContain(all, c => c.ToolId == ToolRegistryConstants.RemovedToolId);

        // Nem por referência/lote exatos da tombstone: a constante exclui-a
        // antes de qualquer filtro.
        var byDummyFacts = await _query.FindCandidatesAsync(
            new ToolCandidateFilter(Reference: "REMOVED", Lot: "REMOVED"));
        Assert.Empty(byDummyFacts);
    }
    [Fact]
    public async Task ProductionSuggestions_UseRecordedContextsAndMachineLinks_WithoutInferringReferences()
    {
        var c2 = new Machine { Code = "C2" };
        var c3 = new Machine { Code = "C3" };
        var cm = new Tool { ToolId = "CM-6018", Type = ToolType.Cm, Reference = "6018", Lot = "66" };
        var bq = new Tool { ToolId = "BQ-Y232", Type = ToolType.Bq, Reference = "Y232", Lot = "1" };
        var replacement = new Tool { ToolId = "CM-replacement", Type = ToolType.Cm, Reference = "6018", Lot = "67" };
        var explicitTool = new Tool { ToolId = "CM-associated", Type = ToolType.Cm, Reference = "5371", Lot = "2", AssociatedMfReference = "6018Y232" };
        var unrelated = new Tool { ToolId = "CM-similar", Type = ToolType.Cm, Reference = "6018Y232-OTHER", Lot = "3" };
        _db.Machines.AddRange(c2, c3);
        _db.Tools.AddRange(cm, bq, replacement, explicitTool, unrelated);
        var production = new JobOn { Machine = c2, Reference = "6018Y232", ProductionNumber = "202617" };
        _db.JobOns.Add(production);
        _db.CmContexts.Add(new CmContext { JobOn = production, ToolId = cm.ToolId });
        _db.BqContexts.Add(new BqContext { JobOn = production, ToolId = bq.ToolId });
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { Tool = cm, Machine = c2 },
            new ToolMachineLink { Tool = cm, Machine = c3 },
            new ToolMachineLink { Tool = bq, Machine = c2 },
            new ToolMachineLink { Tool = replacement, Machine = c2 },
            new ToolMachineLink { Tool = explicitTool, Machine = c3 },
            new ToolMachineLink { Tool = unrelated, Machine = c2 });
        await _db.SaveChangesAsync();
        var linkCount = await _db.ToolMachineLinks.CountAsync();

        var matches = await _query.FindCandidatesAsync(new ToolCandidateFilter(MachineId: c2.Id, ProductionReference: " 6018y232 "));
        Assert.Equal(new[] { "CM-6018", "CM-replacement", "BQ-Y232" }, matches.Select(c => c.ToolId));
        Assert.Equal("C2 · C3", matches.Single(c => c.ToolId == cm.ToolId).MachineCodes);
        var c3Matches = await _query.FindCandidatesAsync(new ToolCandidateFilter(MachineId: c3.Id, ProductionReference: "6018Y232"));
        Assert.Equal(2, c3Matches.Count);
        Assert.Contains(c3Matches, c => c.ToolId == explicitTool.ToolId);
        Assert.Empty(await _query.FindCandidatesAsync(new ToolCandidateFilter(ProductionReference: "UNKNOWN")));
        Assert.Equal(linkCount, await _db.ToolMachineLinks.CountAsync());
    }

    [Fact]
    public async Task Paging_AppliesFiltersBeforeCountingAndSlicing_AndKeepsStableDistinctPages()
    {
        for (var i = 1; i <= 25; i++)
            _db.Tools.Add(new Tool { ToolId = $"page-{i:00}", Type = ToolType.Cm, Reference = "6018", Lot = i.ToString("00") });
        _db.Tools.Add(new Tool { ToolId = "other-ref", Type = ToolType.Cm, Reference = "5371", Lot = "1" });
        await _db.SaveChangesAsync();
        var filter = new ToolCandidateFilter(Type: ToolType.Cm, Reference: "6018");
        var first = await _query.FindPageAsync(filter, 1, 10);
        var second = await _query.FindPageAsync(filter, 2, 10);
        var last = await _query.FindPageAsync(filter, int.MaxValue, 10);
        Assert.Equal(25, first.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(10, second.Items.Count);
        Assert.Empty(first.Items.Select(t => t.ToolId).Intersect(second.Items.Select(t => t.ToolId)));
        Assert.Equal("11", second.Items[0].Lot);
        Assert.Equal(3, last.PageNumber);
        Assert.Equal(5, last.Items.Count);
        var twenty = await _query.FindPageAsync(filter, 1, 20);
        Assert.Equal(20, twenty.Items.Count);
        var invalid = await _query.FindPageAsync(filter, -10, 999);
        Assert.Equal(1, invalid.PageNumber);
        Assert.Equal(10, invalid.PageSize);
        var empty = await _query.FindPageAsync(new ToolCandidateFilter(Reference: "missing"), 99, 20);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(1, empty.PageNumber);
    }


    [Fact]
    public async Task ProductionReference_FerramentasComSoUmaParte_TambemPertencem()
    {
        // Diogo, 19/10/2026: no Job On 1075C142, a CM, o MF e a BQ mostram só
        // o que pertence à referência; as registadas antes com uma parte (BQ
        // C142, MF 1075) também.
        _db.Tools.AddRange(
            new Tool { ToolId = "bq-toda", Type = ToolType.Bq, Reference = "1075C142", Lot = "1" },
            new Tool { ToolId = "bq-parte", Type = ToolType.Bq, Reference = "C142", Lot = "2" },
            new Tool { ToolId = "bq-outra", Type = ToolType.Bq, Reference = "T173", Lot = "1" },
            new Tool { ToolId = "mf-parte", Type = ToolType.Mf, Reference = "1075", Lot = "1" },
            new Tool { ToolId = "mf-outra", Type = ToolType.Mf, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "cm-st100", Type = ToolType.Cm, Reference = "ST100", AssociatedMfReference = "1075", Lot = "1" },
            new Tool { ToolId = "cm-c142", Type = ToolType.Cm, Reference = "C142", Lot = "1" });
        await _db.SaveChangesAsync();

        var bq = await _query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Bq, ProductionReference: "1075C142"));
        Assert.Equal(["bq-toda", "bq-parte"], bq.Select(t => t.ToolId).OrderBy(id => id == "bq-parte"));
        var mf = await _query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Mf, ProductionReference: "1075C142"));
        Assert.Equal(["mf-parte"], mf.Select(t => t.ToolId));
        var cm = await _query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Cm, ProductionReference: "1075C142"));
        Assert.Equal(["cm-st100"], cm.Select(t => t.ToolId));
    }

    [Theory]
    [InlineData("1075C142", "1075", "C142")]
    [InlineData("ST100", null, null)]
    [InlineData("6018", null, null)]
    public void SplitReference_ModeloEParteBq(string reference, string? model, string? bq) =>
        Assert.Equal((model, bq), ToolCandidateQuery.SplitReference(reference));
}
