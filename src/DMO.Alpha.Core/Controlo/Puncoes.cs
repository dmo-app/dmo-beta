namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O REGISTO DAS PUNÇÕES NNPB de uma produção (o Owner 17/10/2026: depois
/// de um problema, o registo passou a ser feito à parte e entra agora no
/// controlo). Um registo por controlo_id, só para produções cuja CM é NNPB.
/// Pode nascer antes do Job On, como o Peso (o Owner 17/10/2026): fica sem
/// controlo_id, com a referência total escrita à mão, e associa-se depois
/// à mão a uma produção (as punções não têm tool_id que a encontre).
/// A identificação (referência da punção e o lote dos próximos registos)
/// vive aqui; cada punção registada é uma <see cref="PuncaoInspecao"/>.
/// Nunca é gate de nada.
/// </summary>
public sealed class Puncoes
{
    public const int PunchReferenceMaxLength = 100;

    public const int LotMaxLength = 12;

    public const int ReferenceMaxLength = 100;

    public int Id { get; set; }

    /// <summary>
    /// controlo_id da produção (único: um registo por produção); null num
    /// registo sem Job On, até ser associado à mão a uma produção.
    /// </summary>
    public int? ControloId { get; set; }

    /// <summary>
    /// A referência total escrita num registo sem Job On (5447T173); com
    /// produção manda a referência do Job On.
    /// </summary>
    public string? Reference { get; set; }

    public Controlo? Controlo { get; set; }

    /// <summary>Referência da punção (por omissão o modelo da referência: 1075 em 1075C142).</summary>
    public string PunchReference { get; set; } = null!;

    /// <summary>O lote aplicado às próximas punções; as anteriores guardam o seu.</summary>
    public string Lot { get; set; } = null!;

    /// <summary>Quem gravou por último (nº de operador ou email do ADMIN).</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>O utilizador de <see cref="UpdatedBy"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? UpdatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<PuncaoInspecao> Inspecoes { get; set; } = new List<PuncaoInspecao>();
}

/// <summary>
/// Uma punção registada: o número, o lote em vigor no momento do registo e
/// se a punção e o arrefecedor foram vistos (marcados por omissão, como na
/// folha do Owner). Um número repetido no mesmo registo é uma «Alteração».
/// </summary>
public sealed class PuncaoInspecao
{
    public const int NumberMaxLength = 40;

    public int Id { get; set; }

    public int PuncoesId { get; set; }

    public Puncoes? Puncoes { get; set; }

    /// <summary>Ordem de registo (1-based).</summary>
    public int Position { get; set; }

    public string Number { get; set; } = null!;

    public string Lot { get; set; } = null!;

    public bool PunchSeen { get; set; }

    public bool CoolerSeen { get; set; }

    public string? RegisteredBy { get; set; }


    /// <summary>O utilizador de <see cref="RegisteredBy"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>

    public Guid? RegisteredByUserId { get; set; }

    public DateTime RegisteredAtUtc { get; set; }
}
