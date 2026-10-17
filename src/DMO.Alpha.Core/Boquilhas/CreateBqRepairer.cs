namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: criar uma ENTIDADE REPARADORA de Boquilhas (o vocabulário do
/// encaminhamento — Admin → App Definições → Boquilhas). Input: apenas o
/// NOME (o fato humano). O Id é emitido pelo backend.
/// </summary>
public sealed record CreateBqRepairer(string Name);

/// <summary>
/// Comando: editar O NOME de uma entidade reparadora existente. A edição
/// NÃO reescreve os movimentos passados — o nome congelado
/// (BqRepairerNameFrozen) de cada saída é a verdade histórica dela.
/// </summary>
public sealed record EditBqRepairer(int BqRepairerId, string Name);
