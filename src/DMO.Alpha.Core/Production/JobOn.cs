namespace DMO.Alpha.Core.Production;

/// <summary>
/// Job On (registo de produção). Entidade mínima para a proof de contexto;
/// não pretende ser um modelo de domínio completo.
/// </summary>
public sealed class JobOn
{
    public int Id { get; set; }

    public string Reference { get; set; } = null!;

    public string ProductionNumber { get; set; } = null!;

    public int MachineId { get; set; }

    /// <summary>
    /// O DIA EM QUE A PRODUÇÃO ENTRA EM MÁQUINA (o corte B — o Owner
    /// 2026-10-10: «em produçao é a que esta hj em maquina e segue as
    /// datas do jobon»; o ping das boquilhas só entra em vigor nesta
    /// data — a regra do canal canónico). Null = a janela ainda não
    /// definida (o fallback: a última produção da máquina).
    /// </summary>
    public DateOnly? InicioEm { get; set; }

    /// <summary>
    /// É uma EXPERIÊNCIA e não uma produção (Diogo, 26/10/2026): usa o número
    /// AAAA00 (ex.: 202600), entra e sai no mesmo dia e pode estar na mesma
    /// linha que a produção em curso; não conta como «a produção da linha».
    /// </summary>
    public bool Experiencia { get; set; }

    /// <summary>
    /// O DIA EM QUE A PRODUÇÃO SAI DE MÁQUINA (a troca da referência).
    /// Null = até aviso (a produção corrente até próxima indicação).
    /// </summary>
    public DateOnly? FimEm { get; set; }

    /// <summary>
    /// Calote DESTA produção (mm) — o campo «Calote desta produção (mm)» da
    /// ficha do Job On no dmo-html (j-calote, placeholder «Usar referência
    /// da Tool»): a calote efetiva da produção quando existe. Null = a
    /// referência da Tool manda (o html: <c>ctx.production?.calote ?? tool
    /// h_ref</c>). Consumida pelo apoio TP/calote do Peso (cálculo de apoio,
    /// NUNCA um gate do Peso) e editável na ficha (corte próprio).
    /// </summary>
    public decimal? CaloteMm { get; set; }

    /// <summary>
    /// Diâmetro D (mm) do tampão DESTA produção (Diogo, 23/10/2026: o Tampão
    /// passa da ficha da CM para o Job On e para o Peso). Com a calote forma
    /// o tampão da produção; o Peso congela-o ao ser criado. Null = ainda
    /// não registado (o Peso continua, o apoio fica «por completar»).
    /// </summary>
    public decimal? TampaoDiametroMm { get; set; }

    /// <summary>
    /// Quando o Job On foi gravado pela primeira vez (UTC). O Sumário do
    /// Planeamento mostra-o nesse dia (Diogo, 19/10/2026: «quando damos lock
    /// deveria guardar»). Null nos Job Ons gravados antes desta coluna.
    /// </summary>
    public DateTime? CriadoEmUtc { get; set; }

    public Machine Machine { get; set; } = null!;

    public CmContext? CmContext { get; set; }

    public MfContext? MfContext { get; set; }

    public BqContext? BqContext { get; set; }
}
