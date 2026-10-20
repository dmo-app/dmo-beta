using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// O aviso «não comparável» da comparação do Peso (D12): a comparabilidade,
/// a CM da comparação (para o link da ficha) e se leva os links (só na
/// comparação ao vivo, onde ainda se pode corrigir antes de guardar).
/// </summary>
public sealed record PesoNaoComparavelVista(
    PesoComparabilidade Comparabilidade,
    string? CmToolId,
    bool ComLinks);
