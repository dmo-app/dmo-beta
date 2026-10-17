using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Ensure do controlo_id de uma produção: get-or-create idempotente por
/// jobon_id (spec Job On L33: o controlo_id é criado com o Job On; spec
/// Controlo: o registo existe desde a criação, antes de o Peso ou outra
/// função estar concluída). Nunca é um ato de criação do utilizador — é
/// bookkeeping idempotente de backfill/abertura: uma produção criada antes
/// deste corte ganha aqui o seu controlo_id UMA vez, e abri-la duas vezes
/// devolve o MESMO controlo_id. O jobon_id é âncora canónica persistida:
/// um jobon_id inexistente é recusa tipada (nenhum controlo é criado para
/// uma produção inexistente) e nenhum facto do Job On ou dos contextos é
/// mutado — o registo Controlo é dono apenas da sua identidade e da
/// relação. Idempotência material: verificação prévia (get) + índice
/// ÚNICO na coluna JobOnId, a última linha da verdade persistida (padrão
/// DuplicateProductionNumber).
/// </summary>
public sealed class EnsureControloForJobOnHandler : IEnsureControloForJobOnHandler
{
    private readonly DmoDbContext _db;

    public EnsureControloForJobOnHandler(DmoDbContext db)
    {
        _db = db;
    }

    public Task<int?> FindExistingIdAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        _db.Controlos.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<EnsureControloForJobOnOutcome> HandleAsync(
        EnsureControloForJobOn command,
        CancellationToken cancellationToken = default)
    {
        // A âncora é o jobon_id persistido: um jobon_id que não resolve é
        // recusa tipada — o ensure assegura o controlo de UMA produção
        // real, nunca cria produção nem inventa âncora.
        var jobOnExists = await _db.JobOns
            .AsNoTracking()
            .AnyAsync(j => j.Id == command.JobOnId, cancellationToken);
        if (!jobOnExists)
        {
            return new EnsureControloForJobOnOutcome(EnsureControloResult.JobOnNotFound);
        }

        // Duas aberturas ao mesmo tempo de uma produção sem controlo: a
        // segunda colide no índice único e volta a correr, encontrando o
        // controlo da primeira.
        return await UniqueViolationRetry.RunAsync(_db, () => GetOrCreateAsync(command.JobOnId, cancellationToken));
    }

    private async Task<EnsureControloForJobOnOutcome> GetOrCreateAsync(int jobOnId, CancellationToken cancellationToken)
    {
        // Get primeiro: um controlo já existente para este jobon_id devolve
        // o MESMO controlo_id sem escrita nenhuma — a re-execução é
        // idempotente por construção.
        var existing = await _db.Controlos
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.JobOnId == jobOnId, cancellationToken);
        if (existing is not null)
        {
            return new EnsureControloForJobOnOutcome(EnsureControloResult.Success, existing.Id);
        }

        // Create (backfill de produções pré-corte): o índice único na
        // coluna JobOnId permanece a última linha da verdade persistida —
        // a mesma produção nunca ganha um segundo controlo_id. (O nome da
        // entidade é qualificado: «Controlo» é também o segmento do
        // namespace em qualquer ficheiro descendente.)
        var created = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
        _db.Controlos.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        return new EnsureControloForJobOnOutcome(EnsureControloResult.Success, created.Id);
    }
}
