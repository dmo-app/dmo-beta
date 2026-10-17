namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Pedido do apoio TP/calote: a CM pelo tool_id canónico, o controlo opcional
/// (o Job On associado MANDA na calote quando existe — o html:
/// <c>ctx.production?.calote</c>) e a calote simulada opcional (o campo do
/// diálogo do html «Calote simulada h (mm)» — sobrepõe-se APENAS na
/// exibição, nunca é gravada em lado nenhum).
/// </summary>
public sealed record PesoCapSupportRequest(
    string CmToolId,
    int? ControloId = null,
    decimal? CaloteSimuladaMm = null);

/// <summary>
/// Read model purpose-specific do apoio TP/calote do Peso (o painel de
/// factos e o diálogo do dmo-html, calculado pelo BACKEND — fonte única de
/// verdade). <see cref="TemReferencia"/> a false é o estado «Referência por
/// completar» (a Tool não tem TP — o html: <c>ctx.cm.tp</c> ausente): o
/// apoio simplesmente não existe e o Peso continua 100% intacto — nunca um
/// gate. OrigemCalote diz de onde veio o h efetivo («Job On» quando a
/// produção tem calote, «Tool CM» quando cai na h_ref, «Simulação» quando o
/// operador simulou um h no diálogo).
/// </summary>
public sealed record PesoCapSupportReadModel(
    bool TemReferencia,
    /// <summary>A mensagem honesta quando não há referência (o aviso do html); null quando há.</summary>
    string? EstadoMensagem,
    string OrigemCalote,
    decimal? DiametroMm,
    decimal? RaioDesenhoMm,
    decimal? AlturaReferenciaMm,
    decimal? AlturaEfetivaMm,
    decimal? RaioReferenciaMm,
    decimal? VolumeReferenciaCm3,
    decimal? RaioCalculadoMm,
    decimal? VolumeEfetivoCm3,
    /// <summary>drawing_delta = reference.R − R_draw.</summary>
    decimal? DiferencaDesenhoMm,
    /// <summary>ΔR = actual.R − reference.R.</summary>
    decimal? DeltaRaioMm,
    /// <summary>ΔV = actual.V − reference.V.</summary>
    decimal? DeltaVolumeCm3);

/// <summary>
/// Query purpose-specific do apoio TP/calote: resolve a geometria TP da CM
/// (ToolTechnicalContext — Tool-owned), a calote efetiva (a do Job On quando
/// existe, senão a h_ref da Tool; a simulação sobrepõe-se na exibição) e
/// devolve o cálculo do <see cref="PesoCapCalculation"/>. Nunca devolve
/// null: a ausência de referência é o estado «Referência por completar».
/// </summary>
public interface IPesoCapSupportQuery
{
    Task<PesoCapSupportReadModel> GetAsync(
        PesoCapSupportRequest request,
        CancellationToken cancellationToken = default);
}
