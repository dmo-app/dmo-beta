namespace DMO.Alpha.Core.Production;

/// <summary>
/// Uma linha só tem uma produção em curso (Diogo, 27/10/2026: dois C3 em curso
/// «isto não deve acontecer»). Das produções que cobrem o dia na mesma máquina,
/// fica a que começou por último: a entrada da seguinte acaba a anterior, mesmo
/// que esta não tenha fim. É a mesma escolha da produção corrente das Boquilhas.
/// </summary>
public static class ProducaoEmCurso
{
    /// <summary>As produções que cobrem o dia, uma por máquina: a que começou por último.</summary>
    public static IReadOnlyList<ProductionContextReadModel> UmaPorMaquina(
        IEnumerable<ProductionContextReadModel> jobOns,
        DateOnly dia) =>
        jobOns
            .Where(j => j.InicioEm is { } inicio && inicio <= dia && (j.FimEm is not { } fim || fim >= dia))
            .GroupBy(j => j.Machine)
            .Select(g => g.OrderByDescending(j => j.InicioEm).ThenByDescending(j => j.JobOnId).First())
            .ToList();
}
