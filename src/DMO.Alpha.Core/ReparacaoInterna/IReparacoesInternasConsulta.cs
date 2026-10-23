using DMO.Alpha.Core.Paging;

namespace DMO.Alpha.Core.ReparacaoInterna;

/// <summary>
/// Os filtros da Consulta (blueprint reparacao-interna/OVERVIEW §1 e RI-05):
/// a máquina, o intervalo de dias operacionais (os dois inclusive), quem
/// registou, CM ou MF e a produção em dois passos: a referência (pelo início,
/// como nas outras procuras) e depois uma das últimas produções dela, pelo
/// <c>jobon_id</c>. Com a produção escolhida a referência já não conta.
/// «Só por associar» (peça 4): as que têm a ferramenta escrita e ainda não
/// foram associadas.
/// </summary>
public sealed record RiConsultaFiltro(
    int? MachineId = null,
    DateOnly? De = null,
    DateOnly? Ate = null,
    Guid? PessoaId = null,
    ReparacaoTipo? Tipo = null,
    string? Referencia = null,
    int? JobOnId = null,
    bool PorAssociar = false)
{
    /// <summary>Há pelo menos um filtro: sem nenhum a Consulta não lê nada (abre vazia).</summary>
    public bool ComFiltro => MachineId is not null || De is not null || Ate is not null || PessoaId is not null
        || Tipo is not null || !string.IsNullOrWhiteSpace(Referencia) || JobOnId is not null || PorAssociar;
}

/// <summary>Uma ferramenta registada que serve para associar: o <c>tool_id</c>, a referência e o lote.</summary>
public sealed record RiCandidata(string ToolId, string Referencia, string Lote);

/// <summary>
/// A ferramenta escrita de uma reparação (peça 4): a referência e o lote
/// escritos e, depois de associada, a ferramenta associada (sem referência
/// quando foi eliminada). Por associar, as ferramentas do mesmo tipo que
/// servem: as da referência e do lote escritos; sem nenhuma, as da
/// referência (<see cref="DoLote"/> falso).
/// </summary>
public sealed record RiEscrita(
    string Referencia,
    string Lote,
    string? AssociadaToolId = null,
    string? AssociadaReferencia = null,
    string? AssociadaLote = null,
    IReadOnlyList<RiCandidata>? Candidatas = null,
    bool DoLote = true)
{
    public bool PorAssociar => AssociadaToolId is null;
}

/// <summary>
/// Uma reparação na Consulta: o dia operacional e a hora, a máquina, a
/// produção (nº e referência), CM ou MF, a ferramenta do contexto
/// (referência e lote, nunca o nome técnico; null quando o contexto já não
/// existe), o número e quem registou. Sem Job On (peça 4), a produção e a
/// referência ficam null; com a ferramenta escrita, <see cref="Escrita"/>.
/// </summary>
public sealed record RiConsultaLinha(
    int Id,
    DateOnly Dia,
    DateTime RegistadoEmUtc,
    string Maquina,
    int? JobOnId,
    string? Producao,
    string? Referencia,
    ReparacaoTipo Tipo,
    RiFerramenta? Ferramenta,
    string Numero,
    string? Quem,
    RiEscrita? Escrita = null,
    string? FerramentaToolId = null,
    TimeOnly? Hora = null);

/// <summary>Uma pessoa do filtro «Quem»: um utilizador que já registou reparações.</summary>
public sealed record RiPessoa(Guid Id, string Nome);

/// <summary>
/// A Consulta dos chefes: quem reparou o quê e em que produção. Só lê; as
/// anuladas ficam de fora, como na lista do registo.
/// </summary>
public interface IReparacoesInternasConsulta
{
    /// <summary>As reparações que contam com estes filtros, da mais recente para a mais antiga, em páginas de 25. Sem filtro, vazia.</summary>
    Task<PagedList<RiConsultaLinha>> ConsultarAsync(RiConsultaFiltro filtro, int? pagina, CancellationToken cancellationToken = default);

    /// <summary>Os utilizadores que já registaram reparações (só utilizadores, nunca nomes copiados), por nome.</summary>
    Task<IReadOnlyList<RiPessoa>> PessoasAsync(CancellationToken cancellationToken = default);
}
