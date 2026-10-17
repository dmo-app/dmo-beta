namespace DMO.Alpha.Core.Production;

/// <summary>
/// Handler do comando de criação de um Job On. Cria o jobon_id durável, os
/// contextos aplicáveis (cm_id/mf_id/bq_id) ancorados às Tools canónicas
/// explicitamente selecionadas e o controlo_id associado (spec Job On
/// L33) na mesma escrita atómica — o contexto de produção que os módulos
/// seguintes (Pegamentos, Peso) consomem — e devolve o read model do
/// contexto estabelecido. Não regista Tools nem muta o registo
/// Ferramentas, e todas as recusas são tipadas e verificadas antes de
/// qualquer escrita: uma recusa não deixa Job Ons, contextos nem
/// controles parcialmente criados.
/// </summary>
public interface ICreateJobOnHandler
{
    Task<CreateJobOnOutcome> HandleAsync(
        CreateJobOn command,
        CancellationToken cancellationToken = default);
}
