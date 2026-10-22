namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Surface só de leitura do Peso: resolução de um Peso pelo seu peso_id
/// canónico (com as leituras persistidas e os valores derivados do cálculo
/// principal) e a lista do histórico de Pesos de uma Tool, para que um Peso
/// tomado antes da produção permaneça diretamente consultável a partir da
/// sua ferramenta. Não é um repositório genérico: as projeções são
/// purpose-specific do surface Peso e o cálculo é o detido pelo backend.
/// </summary>
public interface IPesoQuery
{
    /// <summary>
    /// Devolve o read model do Peso pedido, ou null quando o peso_id não
    /// existe. Os valores derivados são calculados a partir dos inputs
    /// consumidos persistidos no Peso e das leituras persistidas — nunca a
    /// partir do estado corrente das Tools ou da configuração.
    /// </summary>
    Task<PesoReadModel?> FindPesoAsync(int pesoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O Peso do controlo para um valor só (os ecrãs de revisão, o PDF da
    /// produção): o das ferramentas que a produção tem agora ou, sem esse
    /// (depois de uma troca da CM ou da BQ), o mais recente. Null quando o
    /// controlo ainda não tem Peso. Com um só Peso, esse.
    /// </summary>
    Task<PesoReadModel?> FindByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O Peso da CM e da BQ que a produção tem agora (o único que recebe
    /// leituras novas), ou null: antes do primeiro Peso, ou depois de trocar a
    /// CM ou a BQ a meio da produção, até ser criado o Peso das ferramentas
    /// novas (respostas R15 e R16-5 do especialista, 18/10/2026).
    /// </summary>
    Task<PesoReadModel?> FindCurrentByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Todos os Pesos do controlo, pela ordem de gravação, cada um com as
    /// suas ferramentas congeladas e a marca do atual
    /// (<see cref="PesoReadModel.IsCurrent"/>); vazia sem Peso.
    /// </summary>
    Task<IReadOnlyList<PesoReadModel>> ListByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os Pesos criados só para comparação com este Peso, o mais recente
    /// primeiro; vazia sem nenhum.
    /// </summary>
    Task<IReadOnlyList<PesoReadModel>> ListParaComparacaoAsync(
        int pesoId,
        CancellationToken cancellationToken = default);
}
