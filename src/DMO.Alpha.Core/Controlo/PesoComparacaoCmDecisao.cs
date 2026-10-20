namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// «Manter» ou «Colocar de parte» uma CM medida numa comparação de pesos (o
/// Diogo, 19/10/2026, D10: «ao comparar, os CM que são postos de parte
/// depois são enviados para aprovação, e a aprovação deve ser feita a cada
/// CM individual»). Uma por leitura da comparação (cada CM). A CM posta de
/// parte fica por aprovar até quem tem o Controlo Aprovar decidir; mudar a
/// escolha volta a pedir a aprovação. É só registo: pôr de parte não muda o
/// estado da CM nas Ferramentas (D11: o estado só muda pela escolha de quem
/// regista) e não bloqueia nada. A leitura da comparação continua um facto
/// que nunca se reescreve; a decisão vive nesta tabela, ao lado.
/// </summary>
public sealed class PesoComparacaoCmDecisao
{
    public int Id { get; set; }

    /// <summary>A leitura da comparação (a CM medida) a que a escolha se refere.</summary>
    public int MeasurementId { get; set; }

    public PesoComparacaoMeasurement Measurement { get; set; } = null!;

    /// <summary>true = colocar de parte (vai para aprovação); false = manter.</summary>
    public bool DeParte { get; set; }

    /// <summary>Quem escolheu (o nome da sessão, congelado).</summary>
    public string EscolhidoPor { get; set; } = "";

    /// <summary>Quando escolheu (UTC). Serve também de versão da escolha na aprovação.</summary>
    public DateTime EscolhidoEmUtc { get; set; }

    /// <summary>
    /// A decisão sobre pôr de parte: true = aprovado (fica de parte), false =
    /// não aprovado (a CM continua); null = por aprovar (ou «Manter», que não
    /// precisa de aprovação).
    /// </summary>
    public bool? Aprovado { get; set; }

    /// <summary>Quem decidiu a aprovação (o nome da sessão, congelado).</summary>
    public string? AprovadoPor { get; set; }

    public DateTime? AprovadoEmUtc { get; set; }
}
