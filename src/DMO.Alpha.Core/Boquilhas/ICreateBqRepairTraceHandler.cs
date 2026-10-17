namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Handler do comando de criação de um bq_repair_trace_id ancorado a um
/// tool_id canónico existente. Existe no máximo um trace pré-produção não
/// resolvido por tool_id: se já existir um, o handler reutiliza-o em vez de
/// criar um segundo. Nada é escrito no Job On nem no BqContext.
/// </summary>
public interface ICreateBqRepairTraceHandler
{
    Task<CreateBqRepairTraceOutcome> HandleAsync(
        CreateBqRepairTrace command,
        CancellationToken cancellationToken = default);
}
