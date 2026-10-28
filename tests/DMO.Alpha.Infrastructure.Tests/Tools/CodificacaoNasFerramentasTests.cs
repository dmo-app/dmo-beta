using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// A codificação por linha nas ferramentas (Diogo, 28/10/2026): o Criar põe
/// o nome técnico e o modo da B2, o Editar muda o modo, e o Job On da B2 só
/// mostra as ferramentas do modo escolhido nas Definições.
/// </summary>
public sealed class CodificacaoNasFerramentasTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CodificacaoFerramentas _codificacao;
    private readonly Dictionary<string, int> _maquinas = new();

    public CodificacaoNasFerramentasTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        foreach (var codigo in new[] { "B1", "B2", "C1", "C3" })
        {
            var maquina = new Machine { Code = codigo };
            _db.Machines.Add(maquina);
            _db.SaveChanges();
            _maquinas[codigo] = maquina.Id;
        }

        _codificacao = new CodificacaoFerramentas(_db);
    }

    public void Dispose() => _db.Dispose();

    private Task<CreateToolOutcome> CriarAsync(CreateTool pedido) =>
        new CreateToolHandler(_db, new ToolLotQuantitySettings(_db), _codificacao).HandleAsync(pedido);

    /// <summary>Garante: criar sem nome técnico põe o da linha; um nome escrito fica; a C3+B2 fica B2 Tripla; a B2 sozinha fica sem modo.</summary>
    [Fact]
    public async Task Criar_poe_o_nome_tecnico_da_linha_e_o_modo_da_B2()
    {
        var b1 = await CriarAsync(new CreateTool("BQ", "C142", "7", MachineIds: [_maquinas["B1"]]));
        var escrito = await CriarAsync(new CreateTool("CM", "2032", "1", MachineIds: [_maquinas["B1"]], TechnicalName: "M203295MPPSX"));
        var tripla = await CriarAsync(new CreateTool("CM", "1075", "2", MachineIds: [_maquinas["C3"], _maquinas["B2"]]));
        var semModo = await CriarAsync(new CreateTool("MF", "1076", "3", MachineIds: [_maquinas["B2"]]));
        var escolhido = await CriarAsync(new CreateTool("MF", "1077", "4", MachineIds: [_maquinas["B2"]], B2Modo: B2Modo.Tripla));

        Tool Ler(CreateToolOutcome o) => _db.Tools.AsNoTracking().Single(t => t.ToolId == o.Tool!.ToolId);
        Assert.Equal(("ABC142NX34", 180), (Ler(b1).TechnicalName, Ler(b1).AnQuantity));
        Assert.Equal("M203295MPPSX", Ler(escrito).TechnicalName);
        Assert.Equal(("M107544MPPSX", B2Modo.Tripla), (Ler(tripla).TechnicalName, Ler(tripla).B2Modo));
        Assert.Equal((null, null), (Ler(semModo).TechnicalName, Ler(semModo).B2Modo));
        Assert.Equal(("M10774TMFPSV", B2Modo.Tripla, 54), (Ler(escolhido).TechnicalName, Ler(escolhido).B2Modo, Ler(escolhido).BqAccountedQuantity));
    }

    /// <summary>Garante: o Editar escolhe e tira o modo da B2, e sem a B2 nas linhas o modo sai.</summary>
    [Fact]
    public async Task Editar_muda_o_modo_da_B2()
    {
        var criada = await CriarAsync(new CreateTool("CM", "1076", "3", MachineIds: [_maquinas["B2"]]));
        var id = criada.Tool!.ToolId;
        var editar = new EditToolFactsHandler(_db);
        B2Modo? Modo() => _db.Tools.AsNoTracking().Single(t => t.ToolId == id).B2Modo;

        await editar.HandleAsync(new EditToolFacts(id, "CM", "1076", "3", MachineIds: [_maquinas["B2"]], B2Modo: "Dupla"));
        Assert.Equal(B2Modo.Dupla, Modo());
        await editar.HandleAsync(new EditToolFacts(id, "CM", "1076", "3", MachineIds: [_maquinas["B2"]]));
        Assert.Equal(B2Modo.Dupla, Modo());
        await editar.HandleAsync(new EditToolFacts(id, "CM", "1076", "3", MachineIds: [_maquinas["B2"]], B2Modo: ""));
        Assert.Null(Modo());
        await editar.HandleAsync(new EditToolFacts(id, "CM", "1076", "3", MachineIds: [_maquinas["B1"]], B2Modo: "Tripla"));
        Assert.Null(Modo());
    }

    /// <summary>Garante: no Job On da B2 só aparecem as ferramentas do modo escolhido e as sem modo; nas outras linhas nada muda.</summary>
    [Fact]
    public async Task O_Job_On_da_B2_filtra_pelo_modo_escolhido()
    {
        var dupla = (await CriarAsync(new CreateTool("CM", "5001", "1", MachineIds: [_maquinas["C1"], _maquinas["B2"]]))).Tool!.ToolId;
        var tripla = (await CriarAsync(new CreateTool("CM", "5002", "1", MachineIds: [_maquinas["C3"], _maquinas["B2"]]))).Tool!.ToolId;
        var semModo = (await CriarAsync(new CreateTool("CM", "5003", "1", MachineIds: [_maquinas["B2"]]))).Tool!.ToolId;
        var query = new ToolCandidateQuery(_db);
        async Task<string[]> Na(string maquina) => (await query.FindCandidatesAsync(new ToolCandidateFilter(MachineId: _maquinas[maquina])))
            .Select(c => c.ToolId).Order().ToArray();

        Assert.Equal(new[] { dupla, semModo }.Order(), await Na("B2"));
        await _codificacao.GuardarModoB2Async(B2Modo.Tripla);
        Assert.Equal(new[] { tripla, semModo }.Order(), await Na("B2"));
        Assert.Equal(new[] { dupla }, await Na("C1"));
    }
}
