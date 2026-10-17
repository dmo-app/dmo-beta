namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Outcome tipado da escrita de uma entidade reparadora; em caso de
/// sucesso, o fato humano (o nome) e o Id emitido pelo backend (o valor
/// das chaves de rota, nunca uma identidade nas folhas operacionais).
/// </summary>
public sealed record BqRepairerWriteOutcome(
    BqRepairerWriteResult Result,
    int? BqRepairerId = null,
    string? Name = null);

/// <summary>Handler da criação de uma entidade reparadora.</summary>
public interface ICreateBqRepairerHandler
{
    Task<BqRepairerWriteOutcome> HandleAsync(CreateBqRepairer command, CancellationToken cancellationToken = default);
}

/// <summary>Handler da edição do nome de uma entidade reparadora.</summary>
public interface IEditBqRepairerHandler
{
    Task<BqRepairerWriteOutcome> HandleAsync(EditBqRepairer command, CancellationToken cancellationToken = default);
}
