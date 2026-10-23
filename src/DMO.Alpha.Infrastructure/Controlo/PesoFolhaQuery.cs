using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Composes the initial production sheet through the existing read services.
/// Calls remain sequential because those services share a scoped DbContext.
/// </summary>
public sealed class PesoFolhaQuery(
    IProductionContextQuery productionContext,
    IPesoQuery pesoQuery,
    PesoToolFactsQuery toolFactsQuery,
    IPesoCapSupportQuery capSupportQuery,
    IPesoAssociationQuery pesoAssociation) : IPesoFolhaQuery
{
    public async Task<PesoFolhaInicial> CarregarAsync(
        int controloId,
        int? pesoPedidoId,
        decimal? caloteSimulada,
        CancellationToken cancellationToken = default)
    {
        var context = await productionContext.GetByControloAsync(controloId, cancellationToken);

        // Os Pesos da produção, um por CM+BQ (18/10/2026): a folha abre o
        // Peso das ferramentas que a produção tem agora; um de antes de
        // uma troca abre pelo peso_id dele (só se for desta produção).
        // Depois de uma troca sem o Peso novo ainda, a folha propõe
        // criá-lo; nada é gravado ao abrir.
        var pesos = await pesoQuery.ListByControloAsync(controloId, cancellationToken);
        var peso = (pesoPedidoId is int requested ? pesos.FirstOrDefault(p => p.PesoId == requested) : null)
            ?? pesos.FirstOrDefault(p => p.IsCurrent);

        // Os factos técnicos atuais das ferramentas HERDADAS do contexto
        // (não do Peso!): sem o peso ainda, o formulário precisa deles
        // para saber o que já está registado (só a temperatura e os
        // técnicos EM FALTA pedem input humano).
        // CM, BQ e MF (o MF só pela referência) com o técnico e a
        // densidade do vidro numa só consulta. Um Peso de antes de uma
        // troca mostra os valores congelados nele (na página).
        var facts = context is not null
            ? await toolFactsQuery.GetAsync(context.CmToolId, context.BqToolId, context.MfToolId, cancellationToken)
            : null;

        // O APOIO TP/CALOTE (o painel de factos): a calote efetiva é a do
        // Job On quando existe, senão a h_ref da Tool — calculado PELO
        // BACKEND pela query canónica (fonte única; o browser nunca
        // calcula física). A simulação (cap_h) sobrepõe-se na exibição.
        PesoCapSupportReadModel? capSupport = null;
        if (peso is { IsCurrent: false, ControloId: not null })
        {
            // O apoio de um Peso de antes de uma troca: a CM dele, com a
            // calote do Job On.
            capSupport = await capSupportQuery.GetAsync(
                new PesoCapSupportRequest(peso.CmToolId, controloId, caloteSimulada), cancellationToken);
        }
        else if (context?.CmToolId is { } cmToolId)
        {
            // O técnico da CM e a calote do Job On já foram lidos.
            capSupport = cmToolId.Length > 0 && cmToolId.Trim() == cmToolId
                ? PesoCapSupportQuery.FromKnown(facts?.CmTechnical, context.CaloteMm, caloteSimulada)
                : await capSupportQuery.GetAsync(
                    new PesoCapSupportRequest(cmToolId, controloId, caloteSimulada), cancellationToken);
        }

        // A ASSOCIAÇÃO POSTERIOR (o Owner 2026-10-16): sem Peso ainda,
        // os Pesos sem produção da MESMA CM (tool_id) do Job On são
        // propostos — associar é sempre o botão explícito. Depois de uma
        // troca, os que a associação aceita (a CM e a BQ desta produção,
        // agora ou antes da troca; resposta R16-6 do especialista).
        var candidates = peso is null
            ? await pesoAssociation.FindCandidatesForControloAsync(controloId, cancellationToken)
            : Array.Empty<UnassociatedPesoCandidate>();

        return new PesoFolhaInicial(context, pesos, peso, facts, capSupport, candidates);
    }
}
