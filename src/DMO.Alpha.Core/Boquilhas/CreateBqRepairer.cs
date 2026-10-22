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

/// <summary>
/// Comando: remover uma entidade reparadora (Diogo, 21/10/2026: «não existe
/// forma de remover ou editar reparadores»). As saídas já feitas guardam o
/// nome com que foram feitas; as máquinas que a usavam ficam sem reparador.
/// </summary>
public sealed record DeleteBqRepairer(int BqRepairerId);
