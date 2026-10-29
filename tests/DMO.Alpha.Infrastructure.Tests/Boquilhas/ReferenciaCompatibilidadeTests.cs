using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

public sealed class ReferenciaCompatibilidadeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UmaLinhaForaDaFichaNaoGravaMovimento_SalvoAProducaoHistorica(bool historical)
    {
        using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var c2 = new Machine { Code = "C2" }; var b1 = new Machine { Code = "B1" };
        db.Machines.AddRange(c2,b1);
        db.Tools.Add(new Tool { ToolId = "BQ", Type = ToolType.Bq, Reference = "T173", Lot = "3" });
        await db.SaveChangesAsync();
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ", MachineId = c2.Id });
        var trace = new BqRepairTrace { ToolId = "BQ" };
        if (historical)
        {
            var job = new JobOn { MachineId = b1.Id, Reference = "1001T173", ProductionNumber = "209901" };
            var context = new BqContext { JobOn = job, ToolId = "BQ" };
            db.BqContexts.Add(context);await db.SaveChangesAsync();trace.ToolId = null;trace.BqContextId = context.Id;
        }
        db.BqRepairTraces.Add(trace);await db.SaveChangesAsync();
        var result = await new RegisterBqMovementHandler(db,new ModuleSettingsQuery(db))
            .HandleAsync(new RegisterBqMovement(trace.Id,"saida",1,MachineId:b1.Id));
        if (historical) Assert.Equal(RegisterBqMovementResult.Success,result.Result);
        else Assert.NotEqual(RegisterBqMovementResult.Success,result.Result);
        db.ChangeTracker.Clear();
        Assert.Equal(historical ? 1 : 0,await db.BqMovements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CriarJobOnComFerramentaAssociadaAOutraLinhaNaoGrava()
    {
        using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var c2 = new Machine { Code = "C2" }; var b1 = new Machine { Code = "B1" };
        db.Machines.AddRange(c2,b1);db.Tools.Add(new Tool { ToolId = "MF", Type = ToolType.Mf,Reference = "1001",Lot = "1" });await db.SaveChangesAsync();
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "MF", MachineId = c2.Id });await db.SaveChangesAsync();
        var result = await new CreateJobOnHandler(db).HandleAsync(new CreateJobOn("209901","1001T173",b1.Id,null,"MF",null));
        Assert.NotEqual(CreateJobOnResult.Success,result.Result);db.ChangeTracker.Clear();
        Assert.Empty(await db.JobOns.AsNoTracking().ToListAsync());
    }
}
