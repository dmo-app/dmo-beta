namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O REGISTO DE VISÃO de um controlo (o corte approve-mínimo, a decisão
/// GATE 4 do Owner 2026-10-10, verbatim: «Cria um registo historico de
/// como existia informação sobre acontecimentos e foi visto pelo chefes.
/// MAS!!! o nao estar aprovado nao deve criar blocks ou negar
/// continuidade. deve apenas deixar registado que ninguem viu... Esta
/// logica aplica se a tudo, a app de forma alguma pode bloquear»): quem
/// abriu a folha do controlo e quando (a ÚLTIMA vez). NUNCA é gate — é
/// informação: a folha regista a visita, o resto da app segue igual.
/// Uma linha por (controlo, operador): reabrir atualiza o momento.
/// </summary>
public sealed class ControloViewing
{
    /// <summary>Identidade do registo de visão.</summary>
    public int Id { get; set; }

    /// <summary>O controlo cuja folha foi vista.</summary>
    public int ControloId { get; set; }

    /// <summary>A navegação ao controlo (a folha vista).</summary>
    public Controlo? Controlo { get; set; }

    /// <summary>O nº de operador QUEM viu (o human-readable; o fato).</summary>
    public string OperatorId { get; set; } = null!;

    /// <summary>O momento (UTC) da última visita deste operador à folha.</summary>
    public DateTime ViewedAtUtc { get; set; }
}
