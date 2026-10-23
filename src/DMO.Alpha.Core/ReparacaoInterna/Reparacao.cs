namespace DMO.Alpha.Core.ReparacaoInterna;

/// <summary>O que se reparou: só a CM ou o MF da produção (a BQ repara-se nas Boquilhas).</summary>
public enum ReparacaoTipo
{
    Cm = 1,
    Mf = 2
}

/// <summary>
/// Uma reparação interna (blueprint reparacao-interna/OVERVIEW §3): o número
/// individual de uma CM ou de um MF reparado na máquina, ligado à produção do
/// dia e a quem reparou. É o módulo com mais registos (Diogo, 17/10/2026), por
/// isso a linha é curta: nada de referência, lote ou nomes copiados; a
/// ferramenta chega-se pelo contexto (o <c>cm_id</c> ou o <c>mf_id</c> do Job
/// On no momento) e o nome pelo utilizador. Cada reparação é uma linha (sem
/// sessões); o mesmo número pode reparar-se muitas vezes. Registar não mexe
/// em mais nada. Corrigir muda só o número; anular tira-a da lista e das
/// contagens sem a apagar (fica para a História); os dois deixam rasto em
/// <see cref="ReparacaoRasto"/>.
/// <para>
/// A ferramenta escrita (peça 4, Diogo, 21/10/2026): a CM ou o MF pode ter
/// sido trocado na máquina antes de a troca chegar ao Job On, e a máquina
/// pode não ter produção para o dia. Aí escreve-se a referência e o lote, a
/// reparação fica sem contexto (e sem Job On, se não houver) e o responsável
/// associa depois a ferramenta (<see cref="AssociadaToolId"/>), com rasto.
/// </para>
/// </summary>
public sealed class Reparacao
{
    /// <summary>O número reparado tem até 40 caracteres (como na app full antiga).</summary>
    public const int NumeroMaxLength = 40;

    /// <summary>A referência escrita: até 40 (a da ferramenta não tem limite na base; 40 chega para «5447T173»).</summary>
    public const int ReferenciaMaxLength = 40;

    /// <summary>O lote escrito: até 12 (o das Punções; o das Ferramentas tem 3 ou «s/lote»).</summary>
    public const int LoteMaxLength = 12;

    public int Id { get; set; }

    /// <summary>A produção do dia a que a reparação ficou ligada; NULL quando a máquina não tinha produção (só com a ferramenta escrita).</summary>
    public int? JobOnId { get; set; }

    /// <summary>A máquina escolhida (a da produção nesse momento).</summary>
    public int MachineId { get; set; }

    public ReparacaoTipo Tipo { get; set; }

    /// <summary>O <c>cm_id</c> da produção no momento (só numa reparação de CM).</summary>
    public int? CmContextId { get; set; }

    /// <summary>O <c>mf_id</c> da produção no momento (só numa reparação de MF).</summary>
    public int? MfContextId { get; set; }

    /// <summary>O número individual reparado: texto curto, nunca um <c>tool_id</c>.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Quem registou (o utilizador da sessão); NULL no ADMIN e quando o utilizador foi eliminado.</summary>
    public Guid? RegistadoPorUserId { get; set; }

    public DateTime RegistadoEmUtc { get; set; }

    /// <summary>O dia operacional da fábrica em que se registou (vira à hora do Canal Jobon).</summary>
    public DateOnly DiaOperacional { get; set; }

    /// <summary>
    /// A hora da reparação, em hora local da fábrica (Diogo, 23/10/2026): vem
    /// com a hora de agora e a pessoa pode mudá-la. Vai com o
    /// <see cref="DiaOperacional"/> (o dia vira à hora do Canal Jobon, por isso
    /// não se junta numa data: 03:00 do dia 22 é de madrugada de 23).
    /// NULL nas reparações de antes: mostra-se a hora do registo.
    /// <see cref="RegistadoEmUtc"/> fica como o carimbo de quando se registou.
    /// </summary>
    public TimeOnly? Hora { get; set; }

    /// <summary>Quando foi anulada; NULL enquanto conta. Quem anulou e o motivo estão no rasto.</summary>
    public DateTime? AnuladaEmUtc { get; set; }

    /// <summary>A referência da CM ou do MF escrita (sem espaços nas pontas, em maiúsculas); só sem contexto.</summary>
    public string? ReferenciaEscrita { get; set; }

    /// <summary>O lote escrito, com a referência.</summary>
    public string? LoteEscrito { get; set; }

    /// <summary>A ferramenta que o responsável associou à escrita; NULL enquanto está por associar.</summary>
    public string? AssociadaToolId { get; set; }

    /// <summary>Quem associou; NULL no ADMIN e quando o utilizador foi eliminado.</summary>
    public Guid? AssociadaPorUserId { get; set; }

    public DateTime? AssociadaEmUtc { get; set; }

    /// <summary>A ferramenta foi escrita e ainda ninguém a associou.</summary>
    public bool PorAssociar => ReferenciaEscrita is not null && AssociadaToolId is null;
}

/// <summary>O que se fez a uma reparação já registada.</summary>
public enum ReparacaoAcao
{
    Corrigido = 1,
    Anulado = 2,
    /// <summary>A ferramenta escrita foi associada a uma ferramenta registada (peça 4).</summary>
    Associado = 3
}

/// <summary>
/// Uma linha do rasto (blueprint reparacao-interna/OVERVIEW §2, SG-24): o
/// número antes e depois (sem depois ao anular e ao associar), quem, quando
/// e o motivo, se o escreveram; ao associar, a ferramenta associada. Só se acrescenta: nada no rasto se muda nem se apaga; sai
/// apenas com a reparação, quando se elimina o Job On.
/// </summary>
public sealed class ReparacaoRasto
{
    /// <summary>O motivo é curto e opcional.</summary>
    public const int MotivoMaxLength = 200;

    public int Id { get; set; }

    public int ReparacaoId { get; set; }

    public ReparacaoAcao Acao { get; set; }

    public string NumeroAntes { get; set; } = string.Empty;

    /// <summary>O número corrigido; NULL ao anular.</summary>
    public string? NumeroDepois { get; set; }

    /// <summary>Quem corrigiu ou anulou; NULL no ADMIN e quando o utilizador foi eliminado.</summary>
    public Guid? PorUserId { get; set; }

    public DateTime EmUtc { get; set; }

    public string? Motivo { get; set; }

    /// <summary>A hora antes de corrigir (só quando a correção a mudou; NULL se não tinha).</summary>
    public TimeOnly? HoraAntes { get; set; }

    /// <summary>A hora corrigida (só quando a correção a mudou).</summary>
    public TimeOnly? HoraDepois { get; set; }

    /// <summary>O <c>tool_id</c> associado (só ao associar). Fica como estava, mesmo que a ferramenta se elimine.</summary>
    public string? ToolIdAssociado { get; set; }
}
