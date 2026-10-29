using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task OpcoesDaReferencia_SeparamCompatibilidadeAtualDaLinhaHistorica_ERecusamOutraLinha()
    {
        SkipWithoutConnection();
        await using var db=CreateContext();await db.Database.MigrateAsync();
        await using var transaction=await db.Database.BeginTransactionAsync();
        var marker="REF-"+Guid.NewGuid().ToString("N")[..8];
        var current=new Machine { Code=marker+"-C2" };var historical=new Machine { Code=marker+"-B1" };var other=new Machine { Code=marker+"-B3" };
        db.Machines.AddRange(current,historical,other);
        var tool=new Tool {ToolId=marker,Type=ToolType.Bq,Reference=marker,Lot="3"};
        db.Tools.Add(tool);await db.SaveChangesAsync();
        db.ToolMachineLinks.Add(new ToolMachineLink {ToolId=tool.ToolId,MachineId=current.Id});
        var job=new JobOn {MachineId=historical.Id,Reference=marker+"T173",ProductionNumber=marker};
        var context=new BqContext {JobOn=job,ToolId=tool.ToolId};db.BqContexts.Add(context);await db.SaveChangesAsync();
        var trace=new BqRepairTrace {BqContextId=context.Id};var free=new BqRepairTrace {ToolId=tool.ToolId};db.BqRepairTraces.AddRange(trace,free);await db.SaveChangesAsync();
        db.BqMovements.Add(new BqMovement {BqRepairTraceId=trace.Id,Type=BqMovementType.Saida,Quantity=1,OccurredAtUtc=DateTime.UtcNow,CreatedAtUtc=DateTime.UtcNow,MachineCodeFrozen=historical.Code,BqRepairerNameFrozen="Oficina da referência"});
        db.ArmazemMovimentos.Add(new ArmazemMovimento {ToolId=tool.ToolId,Tipo=ArmazemMovimentoTipo.Saida,MachineId=historical.Id,JobOnId=job.Id,Destino=ArmazemDestino.Reparacao,OcorridoEmUtc=DateTime.UtcNow,RegistadoEmUtc=DateTime.UtcNow,OperacaoId=Guid.NewGuid(),ReparadorNome="Oficina da referência"});
        var modelTool = new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "987654321", Lot = "1" };
        db.Tools.Add(modelTool);
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = modelTool.ToolId, MachineId = current.Id });
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var query=new ReferenceOptionsQuery(db);
        Assert.Equal(new[]{current.Code}, (await query.ReadAsync("jobon", "987654321T173", null))["linha"]);
        var tools=await query.ReadAsync("ferramentas",marker,null);
        Assert.Equal(new[]{current.Id.ToString()},tools["machine_id"]);
        Assert.Equal(new[]{"3"},tools["lot"]);
        Assert.Equal(new[]{current.Code},(await query.ReadAsync("jobon",marker,null))["linha"]);
        var bq=await query.ReadAsync("bq",marker,"3");
        Assert.Equal(new[]{historical.Code},bq["maquina"]);
        Assert.Equal(new[]{"Oficina da referência"},bq["reparador"]);
        var warehouse=await query.ReadAsync("armazem",marker,null);
        Assert.Equal(new[]{historical.Code},warehouse["linha"]);
        Assert.Empty((await query.ReadAsync("bq",marker+"INEXISTENTE",null))["maquina"]);
        foreach(var scope in new[]{"ri","controlo","revisao","jobon","jobon-historico","programada"})
            Assert.NotNull(await query.ReadAsync(scope,marker,null));
        var before=await db.BqMovements.CountAsync();
        var rejected=await new RegisterBqMovementHandler(db,new ModuleSettingsQuery(db))
            .HandleAsync(new RegisterBqMovement(free.Id,"saida",1,MachineId:other.Id));
        Assert.Equal(RegisterBqMovementResult.MachineNotCompatible,rejected.Result);
        db.ChangeTracker.Clear();Assert.Equal(before,await db.BqMovements.AsNoTracking().CountAsync());
        var accepted=await new RegisterBqMovementHandler(db,new ModuleSettingsQuery(db))
            .HandleAsync(new RegisterBqMovement(trace.Id,"saida",1,MachineId:historical.Id));
        Assert.Equal(RegisterBqMovementResult.Success,accepted.Result);
        db.ChangeTracker.Clear();Assert.Equal(before+1,await db.BqMovements.AsNoTracking().CountAsync());
        // A transação repõe exclusivamente os dados descartáveis deste cenário.
    }
}
