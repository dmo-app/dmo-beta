using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O carimbo dos registos do Controlo (17/10/2026): ao gravar um Peso ou uns
/// Pegamentos novos, guarda quando e quem os gravou. Fica num só sítio para
/// os handlers de criação não terem de mudar; a Revisão do Controlo usa a
/// data para o calendário e o nome para o «Operador».
/// </summary>
public sealed class RegistoCarimboInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentAccountContext _account;

    public RegistoCarimboInterceptor(ICurrentAccountContext account)
    {
        _account = account;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Carimbar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Carimbar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Carimbar(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var agora = DateTime.UtcNow;
        string? quem = null;
        var quemLido = false;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (!quemLido)
            {
                var actor = _account.Current;
                quem = actor.IsAuthenticated
                    ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName)
                    : null;
                quemLido = true;
            }

            switch (entry.Entity)
            {
                case Peso peso:
                    peso.RegistadoEmUtc ??= agora;
                    peso.RegistadoPor ??= quem;
                    break;
                case Pegamentos pegamentos:
                    pegamentos.RegistadoEmUtc ??= agora;
                    pegamentos.RegistadoPor ??= quem;
                    break;
            }
        }
    }
}
