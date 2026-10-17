namespace DMO.Alpha.Core.Production;

/// <summary>
/// O AVISO INFORMATIVO de sobreposição de janelas na MESMA máquina (o
/// Owner 2026-10-12: «overlap da maquina deve dar um warning, nao deve
/// bloquear, o warning deve dizer detalhadamente o que esta mal como
/// resolver»). NUNCA uma recusa, nunca um bloqueio: a produção fica
/// GRAVADA na mesma e o aviso viaja por cima do facto gravado — as
/// produções em conflito (a referência total, o nº, AS JANELAS de cada)
/// e o período exato da sobreposição, com a sugestão concreta de
/// resolução no <see cref="Message"/>.
/// </summary>
public sealed record OverlapWarning(
    IReadOnlyList<OverlappingProduction> Conflicts,
    string MachineCode,
    string Message);

/// <summary>
/// Uma produção em conflito na mesma máquina: os factos humanos dela
/// (a referência TOTAL, o nº de produção, a janela início→fim) e o
/// período exato da sobreposição com a produção gravada (OverlapFrom /
/// OverlapUntil; null = o limite em aberto — o início indefinido ou o
/// «até aviso»).
/// </summary>
public sealed record OverlappingProduction(
    int JobOnId,
    string Reference,
    string ProductionNumber,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    DateOnly? OverlapFrom,
    DateOnly? OverlapUntil);

/// <summary>
/// O candidato ao cálculo do overlap: outra produção da MESMA máquina
/// com a sua janela temporal (InicioEm/FimEm; null = em aberto — o
/// fallback «última produção da máquina» / «até aviso»).
/// </summary>
public sealed record OverlapCandidate(
    int JobOnId,
    string Reference,
    string ProductionNumber,
    DateOnly? InicioEm,
    DateOnly? FimEm);
