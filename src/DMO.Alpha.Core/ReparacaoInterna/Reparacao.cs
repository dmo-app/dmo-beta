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
/// </summary>
public sealed class Reparacao
{
    /// <summary>O número reparado tem até 40 caracteres (como na app full antiga).</summary>
    public const int NumeroMaxLength = 40;

    public int Id { get; set; }

    /// <summary>A produção do dia a que a reparação ficou ligada.</summary>
    public int JobOnId { get; set; }

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

    /// <summary>Quando foi anulada; NULL enquanto conta. Quem anulou e o motivo estão no rasto.</summary>
    public DateTime? AnuladaEmUtc { get; set; }
}

/// <summary>O que se fez a uma reparação já registada.</summary>
public enum ReparacaoAcao
{
    Corrigido = 1,
    Anulado = 2
}

/// <summary>
/// Uma linha do rasto (blueprint reparacao-interna/OVERVIEW §2, SG-24): o
/// número antes e depois (sem depois ao anular), quem, quando e o motivo, se
/// o escreveram. Só se acrescenta: nada no rasto se muda nem se apaga; sai
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
}
