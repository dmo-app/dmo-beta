using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Implementação purpose-specific de <see cref="IPesoCapSupportQuery"/>: o
/// apoio TP/calote do Peso (o «TP / calote · Apoio separado» do dmo-html).
/// Resolve a geometria TP de referência da CM (ToolTechnicalContext — Tool-
/// owned: D, h_ref), a CALOTE EFETIVA (a do Job On quando
/// existe — o html: <c>ctx.production?.calote</c>; senão a própria h_ref da
/// Tool; a simulação sobrepõe-se apenas na exibição) e devolve o cálculo do
/// <see cref="PesoCapCalculation"/> — a fonte única da matemática, nunca uma
/// segunda implementação. Nunca devolve null: a Tool sem TP é o estado
/// «Referência por completar» e o Peso continua 100% intacto (apoio, nunca
/// gate). Zero escrita: a query é só de leitura.
/// </summary>
public sealed class PesoCapSupportQuery : IPesoCapSupportQuery
{
    private readonly DmoDbContext _db;

    public PesoCapSupportQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<PesoCapSupportReadModel> GetAsync(
        PesoCapSupportRequest request,
        CancellationToken cancellationToken = default)
    {
        var toolId = request.CmToolId?.Trim();
        if (string.IsNullOrEmpty(toolId))
        {
            return SemReferencia();
        }

        // A geometria TP de referência é FACTO DA TOOL (regra «TP drawing/
        // reference geometry»: não varia por módulo nem por produção).
        var tp = await _db.ToolTechnicalContexts
            .AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new { t.TpDiametroMm, t.TpSagittaMm })
            .FirstOrDefaultAsync(cancellationToken);
        if (tp is null)
        {
            return SemReferencia();
        }

        // A calote do Job On (quando o pedido vem pelo controlo): a produção
        // manda na calote efetiva — o html usa ctx.production.calote.
        decimal? caloteJobOn = null;
        if (request.ControloId is int controloId)
        {
            caloteJobOn = await _db.Controlos
                .AsNoTracking()
                .Where(c => c.Id == controloId)
                .Select(c => c.JobOn.CaloteMm)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Calcular(tp.TpDiametroMm, tp.TpSagittaMm, caloteJobOn, request.CaloteSimuladaMm);
    }

    /// <summary>
    /// O mesmo apoio a partir de factos já lidos pela página (o contexto
    /// técnico da CM e a calote do Job On), sem nova consulta.
    /// </summary>
    public static PesoCapSupportReadModel FromKnown(
        ToolTechnicalReadModel? cmTechnical,
        decimal? caloteJobOn,
        decimal? caloteSimuladaMm) =>
        cmTechnical is null
            ? SemReferencia()
            : Calcular(cmTechnical.TpDiametroMm, cmTechnical.TpSagittaMm, caloteJobOn, caloteSimuladaMm);

    private static PesoCapSupportReadModel Calcular(
        decimal? tpDiametroMm,
        decimal? tpSagittaMm,
        decimal? caloteJobOn,
        decimal? caloteSimuladaMm)
    {
        decimal? alturaEfetiva;
        string origem;
        if (caloteSimuladaMm is { } simulada)
        {
            alturaEfetiva = simulada;
            origem = "Simulação";
        }
        else if (caloteJobOn is { } doJobOn)
        {
            alturaEfetiva = doJobOn;
            origem = "Job On";
        }
        else
        {
            alturaEfetiva = tpSagittaMm;
            origem = "Tool CM";
        }

        var support = PesoCapCalculation.TrySupport(
            tpDiametroMm, tpSagittaMm, alturaEfetiva);
        if (support is null)
        {
            return SemReferencia();
        }

        return new PesoCapSupportReadModel(
            TemReferencia: true,
            EstadoMensagem: null,
            OrigemCalote: origem,
            DiametroMm: support.Referencia.DiametroMm,
            AlturaReferenciaMm: support.Referencia.AlturaMm,
            AlturaEfetivaMm: support.Efetivo.AlturaMm,
            RaioReferenciaMm: support.Referencia.RaioMm,
            VolumeReferenciaCm3: support.Referencia.VolumeCm3,
            RaioCalculadoMm: support.Efetivo.RaioMm,
            VolumeEfetivoCm3: support.Efetivo.VolumeCm3,
            DeltaRaioMm: support.DeltaRaioMm,
            DeltaVolumeCm3: support.DeltaVolumeCm3);
    }

    /// <summary>
    /// O estado «Referência por completar» (a mensagem exata do html):
    /// honesto e sem bloqueio — o Peso pode continuar sem este apoio.
    /// </summary>
    private static PesoCapSupportReadModel SemReferencia() => new(
        TemReferencia: false,
        EstadoMensagem: "Complete D e h de referência na ferramenta CM. O Peso pode continuar sem este apoio.",
        OrigemCalote: string.Empty,
        DiametroMm: null,
        AlturaReferenciaMm: null,
        AlturaEfetivaMm: null,
        RaioReferenciaMm: null,
        VolumeReferenciaCm3: null,
        RaioCalculadoMm: null,
        VolumeEfetivoCm3: null,
        DeltaRaioMm: null,
        DeltaVolumeCm3: null);
}
