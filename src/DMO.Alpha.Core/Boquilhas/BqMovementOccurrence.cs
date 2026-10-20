using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// A data e a hora reais de um movimento no futuro: o movimento grava e a
/// página avisa, para se corrigir se a data estiver errada (Diogo, 19/10/2026:
/// «as datas têm de ser livres»; «só coisas mesmo complicadas é que se dá
/// block, o resto warnings»). Na beta.241 (BD-8 do segundo deep sweep) era
/// recusado. A folga cobre o relógio do posto um pouco adiantado em relação
/// ao servidor, para não avisar no uso diário.
/// </summary>
public static class BqMovementOccurrence
{
    /// <summary>Os minutos que a data real pode passar do agora do servidor sem aviso (diferença entre relógios).</summary>
    public const int FutureToleranceMinutes = 10;

    /// <summary>
    /// True quando o instante real (UTC) é depois de agora mais a folga de
    /// <see cref="FutureToleranceMinutes"/> minutos.
    /// </summary>
    public static bool IsInTheFuture(DateTime occurredAtUtc, DateTimeOffset now)
    {
        var occurred = occurredAtUtc.Kind == DateTimeKind.Local ? occurredAtUtc.ToUniversalTime() : occurredAtUtc;

        // A hora que se repete quando a hora muda (outubro: 01:00–01:59 passa
        // duas vezes em Lisboa) é dois instantes, e a conversão da hora local
        // fica com o segundo. Conta o primeiro: a hora escrita (ou o agora
        // pré-preenchido) na primeira passagem não é futuro.
        var local = DmoBusinessTime.ToLocal(occurred);
        if (DmoBusinessTime.Zone.IsAmbiguousTime(local))
        {
            var earliestOffset = DmoBusinessTime.Zone.GetAmbiguousTimeOffsets(local).Max();
            occurred = DateTime.SpecifyKind(local - earliestOffset, DateTimeKind.Utc);
        }

        return occurred > now.UtcDateTime.AddMinutes(FutureToleranceMinutes);
    }
}
