using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O carimbo dos registos do Controlo (17/10/2026): ao gravar um Peso ou uns
/// Pegamentos novos, guarda quando e quem os gravou. Fica num só sítio para
/// os handlers de criação não terem de mudar; a Revisão do Controlo usa a
/// data para o calendário e o nome para o «Operador». As leituras novas do
/// Peso e dos Pegamentos levam só a data (19/10/2026); o rasto de uma
/// leitura dos Pegamentos corrigida ou apagada leva quem (4.6).
/// <para>
/// O autor pelo utilizador (fundações do full, E3 do DMO-FULL): sempre que um
/// facto escreve o autor como texto (as colunas da <see cref="AutoriaMap"/>),
/// o identificador ao lado fica com o utilizador da sessão; o ADMIN e quem
/// não tem sessão ficam sem ele. Apagar o texto apaga o identificador.
/// </para>
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
            if (entry.State == EntityState.Modified)
            {
                CarimbarUtilizador(entry);
                continue;
            }

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
                // As leituras também (Diogo, 19/10/2026, C9): a Revisão avisa
                // «Registado depois da aprovação».
                case PesoMeasurement leituraPeso:
                    leituraPeso.RegistadoEmUtc ??= agora;
                    break;
                case PegamentosMeasurement leituraPegamentos:
                    leituraPegamentos.RegistadoEmUtc ??= agora;
                    break;
                // Quem corrigiu ou apagou uma leitura dos Pegamentos (4.6).
                case PegamentosMeasurementHistorico rasto:
                    rasto.Por ??= quem;
                    break;
                // E de uma leitura do Peso (27/10/2026).
                case PesoMeasurementHistorico rastoPeso:
                    rastoPeso.Por ??= quem;
                    break;
            }

            CarimbarUtilizador(entry);
        }
    }

    // O identificador ao lado de cada autor escrito neste SaveChanges: num
    // facto novo, o de quem tem a sessão quando o texto existe; num facto
    // alterado, só quando o texto mudou (e nenhum quando o texto ficou vazio).
    private void CarimbarUtilizador(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        foreach (var autoria in AutoriaMap.De(entry.Metadata.ClrType))
        {
            var texto = entry.Property(autoria.Texto);
            var identificador = entry.Property(autoria.Identificador);
            if (entry.State == EntityState.Added)
            {
                if (texto.CurrentValue is not null && identificador.CurrentValue is null)
                {
                    identificador.CurrentValue = UtilizadorDaSessao();
                }
            }
            else if (texto.IsModified && !identificador.IsModified)
            {
                identificador.CurrentValue = texto.CurrentValue is null ? null : UtilizadorDaSessao();
            }
        }
    }

    private Guid? UtilizadorDaSessao()
    {
        var actor = _account.Current;
        return actor is { IsAuthenticated: true, IsAdmin: false } ? actor.ActorId : null;
    }
}
