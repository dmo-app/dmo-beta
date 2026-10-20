using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// A criação de uma ENTIDADE REPARADORA (o vocabulário do encaminhamento
/// por máquina). Recusas tipadas e informativas — nunca bloqueio: o nome
/// vazio (EmptyName) e o nome já usado (NameAlreadyInUse, a unicidade do
/// fato humano; o índice único é a última linha da persistência).
/// </summary>
public sealed class CreateBqRepairerHandler : ICreateBqRepairerHandler
{
    private readonly DmoDbContext _db;

    public CreateBqRepairerHandler(DmoDbContext db)
    {
        _db = db;
    }

    // Dois envios iguais ao mesmo tempo: o segundo bate no índice único do
    // nome; a segunda tentativa volta a verificar e devolve NameAlreadyInUse.
    public Task<BqRepairerWriteOutcome> HandleAsync(
        CreateBqRepairer command,
        CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(_db, () => HandleOnceAsync(command, cancellationToken));

    private async Task<BqRepairerWriteOutcome> HandleOnceAsync(
        CreateBqRepairer command,
        CancellationToken cancellationToken)
    {
        var name = command.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return new BqRepairerWriteOutcome(BqRepairerWriteResult.EmptyName);
        }

        if (await _db.BqRepairers.AsNoTracking()
            .AnyAsync(r => r.Name == name, cancellationToken))
        {
            return new BqRepairerWriteOutcome(BqRepairerWriteResult.NameAlreadyInUse);
        }

        var repairer = new BqRepairer { Name = name };
        _db.BqRepairers.Add(repairer);
        await _db.SaveChangesAsync(cancellationToken);

        return new BqRepairerWriteOutcome(BqRepairerWriteResult.Success, repairer.Id, repairer.Name);
    }
}
