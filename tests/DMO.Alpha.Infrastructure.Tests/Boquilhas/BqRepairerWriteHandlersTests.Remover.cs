using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

public sealed partial class BqRepairerWriteHandlersTests
{
    [Fact]
    public async Task Delete_SaiOReparador_AsSaidasGuardamONome_EAsRotasFicamSemReparador()
    {
        // Diogo, 21/10/2026: «não existe forma de remover ou editar
        // reparadores». As saídas que apontavam para ele largam a ligação e
        // ficam com o nome (a que já o tinha congelado mantém o dela); a rota
        // da máquina que o usava fica vazia; as outras ficam como estavam.
        using var db = CreateContext();
        var vidroart = new BqRepairer { Name = "Vidroart" };
        var outro = new BqRepairer { Name = "Oficina Norte" };
        db.BqRepairers.AddRange(vidroart, outro);
        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9" };
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        db.BqMovements.AddRange(
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 2, BqRepairerId = vidroart.Id, BqRepairerNameFrozen = "Vidroart Lda" },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 3, BqRepairerId = vidroart.Id });
        db.ModuleSettings.AddRange(
            new ModuleSetting { SettingKey = BqRepairerRouting.SettingKeyFor("B1"), SettingValue = vidroart.Id.ToString() },
            new ModuleSetting { SettingKey = BqRepairerRouting.SettingKeyFor("B2"), SettingValue = outro.Id.ToString() });
        await db.SaveChangesAsync();

        var outcome = await new DeleteBqRepairerHandler(db).HandleAsync(new DeleteBqRepairer(vidroart.Id));

        Assert.Equal(BqRepairerWriteResult.Success, outcome.Result);
        Assert.Equal(["Oficina Norte"], await db.BqRepairers.AsNoTracking().Select(r => r.Name).ToListAsync());
        var saidas = await db.BqMovements.AsNoTracking().OrderBy(m => m.Quantity).ToListAsync();
        Assert.All(saidas, m => Assert.Null(m.BqRepairerId));
        Assert.Equal(["Vidroart Lda", "Vidroart"], saidas.Select(m => m.BqRepairerNameFrozen).ToList());
        var rotas = await db.ModuleSettings.AsNoTracking().ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
        Assert.Equal(string.Empty, rotas[BqRepairerRouting.SettingKeyFor("B1")]);
        Assert.Equal(outro.Id.ToString(), rotas[BqRepairerRouting.SettingKeyFor("B2")]);
    }

    [Fact]
    public async Task Delete_ReparadorQueNaoExiste_RecusaTipada()
    {
        using var db = CreateContext();

        var outcome = await new DeleteBqRepairerHandler(db).HandleAsync(new DeleteBqRepairer(999));

        Assert.Equal(BqRepairerWriteResult.NotFound, outcome.Result);
    }
}
