namespace DMO.Alpha.Core.Production;

/// <summary>
/// Uma linha só tem uma produção em curso (Diogo, 27/10/2026: dois C3 em curso
/// «isto não deve acontecer»). Das produções que cobrem o dia na mesma máquina,
/// fica a que começou por último: a entrada da seguinte acaba a anterior, mesmo
/// que esta não tenha fim. É a mesma escolha da produção corrente das Boquilhas.
/// A única exceção (Diogo, 27/10/2026): uma experiência que entra e sai no
/// mesmo dia fica ao lado da produção da linha, sem a tirar.
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

    /// <summary>Uma experiência que entra e sai no mesmo dia (a exceção às duas referências na linha).</summary>
    public static bool ExperienciaDeUmDia(ProductionContextReadModel jobOn) =>
        jobOn.Experiencia && jobOn.InicioEm is { } inicio && jobOn.FimEm == inicio;

    /// <summary>
    /// O que a linha tem no dia: a produção da linha (a que começou por último,
    /// fora as experiências de um dia) e, ao lado, a experiência que entra e
    /// sai nesse dia. Nunca duas produções normais na mesma linha.
    /// </summary>
    public static IReadOnlyList<ProductionContextReadModel> DoDia(
        IEnumerable<ProductionContextReadModel> jobOns,
        DateOnly dia)
    {
        var cobrem = jobOns
            .Where(j => j.InicioEm is { } inicio && inicio <= dia && (j.FimEm is not { } fim || fim >= dia))
            .ToList();
        var linha = UmaPorMaquina(cobrem.Where(j => !ExperienciaDeUmDia(j)), dia);
        var experiencias = cobrem
            .Where(ExperienciaDeUmDia)
            .GroupBy(j => j.Machine)
            .Select(g => g.OrderByDescending(j => j.JobOnId).First());
        return linha.Concat(experiencias).ToList();
    }

    /// <summary>
    /// As produções que entram em cada dia: uma por linha e por dia (a última
    /// lançada, a mesma que o quadro do dia mostra), mais a experiência de um
    /// dia. Várias produções a entrar na mesma linha no mesmo dia não podem
    /// estar todas em máquina.
    /// </summary>
    public static IReadOnlyList<ProductionContextReadModel> EntradasPorDia(
        IEnumerable<ProductionContextReadModel> entradas) =>
        entradas
            .Where(j => j.InicioEm is not null)
            .GroupBy(j => (j.Machine, j.InicioEm, Experiencia: ExperienciaDeUmDia(j)))
            .Select(g => g.OrderByDescending(j => j.JobOnId).First())
            .OrderBy(j => j.InicioEm)
            .ToList();
}
