using System.Text.Encodings.Web;
using System.Text.Unicode;
using DMO.Alpha.Core.Tools;
using Microsoft.AspNetCore.Html;

namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// O «% uso» de uma ferramenta, em qualquer página, abre o painel com o
/// histórico do uso dessa ferramenta (Diogo, 27/10/2026: «qualquer % uso de
/// uma ferramenta abre um slide dessa tool_id com esse historial»). O
/// componente é <see cref="Link"/>; o painel é o painel de apoio
/// (js/dmo-support.js), que abre a página /Ferramentas/Utilizacao.
/// </summary>
public static class UsoFerramenta
{
    public const string Pagina = "/Ferramentas/Utilizacao";

    // Como o Razor: sem escapar os acentos nem o «—».
    private static readonly HtmlEncoder Codificador = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>
    /// As datas de uma produção com os dias à frente (Diogo, 29/10/2026: «metemos
    /// quantidade de dias em frente às datas»): «19/10/2026 a 28/10/2026 · 10 dias».
    /// Sem fim conta até hoje («em curso»); a que está na máquina hoje leva também
    /// «em curso». O mesmo texto no Histórico da ficha e na Utilização por produção.
    /// </summary>
    public static string Datas(DateOnly? inicio, DateOnly? fim, DateOnly hoje)
    {
        var texto = (inicio?.ToString("dd/MM/yyyy") ?? "—") + " a " + (fim?.ToString("dd/MM/yyyy") ?? "em curso");
        if (inicio is { } i)
        {
            var dias = (fim ?? hoje).DayNumber - i.DayNumber + 1;
            if (dias > 0) texto += $" · {dias} {(dias == 1 ? "dia" : "dias")}";
        }

        return fim is { } f && inicio <= hoje && f >= hoje ? texto + " · em curso" : texto;
    }

    public static string Href(string toolId) => Pagina + "?tool_id=" + Uri.EscapeDataString(toolId);

    /// <summary>
    /// O «% uso» como ligação ao histórico (o painel de apoio abre-a ao lado).
    /// Sem ferramenta ou sem valor («—»), só o texto.
    /// </summary>
    public static IHtmlContent Link(string? toolId, string? texto, string? classe = null)
    {
        var conteudo = Codificador.Encode(texto ?? "—");
        if (string.IsNullOrEmpty(toolId) || string.IsNullOrWhiteSpace(texto) || texto.Trim() == "—")
        {
            return new HtmlString(conteudo);
        }
        var classes = string.IsNullOrEmpty(classe) ? "dmo-uso-link" : "dmo-uso-link " + classe;
        return new HtmlString($"<a class=\"{Codificador.Encode(classes)}\" href=\"{Codificador.Encode(Href(toolId))}\" title=\"O histórico do uso desta ferramenta\">{conteudo}</a>");
    }

    /// <summary>
    /// O uso no fim de cada produção: o medido (a primeira leitura a seguir à
    /// produção) ou, sem ele, a parte do % de hoje que cabe ao que se produziu
    /// até ao fim dessa produção (com «≈»), a mesma conta do delta estimado
    /// (FerramentaProducoes.DeltaPeloProduzido): na última produção dá o % de
    /// hoje. Sem nenhum dos dois fica nulo: nunca um número inventado.
    /// </summary>
    public static IReadOnlyDictionary<int, (decimal Valor, bool Estimado)> UsoDepois(FerramentaProducoes producoes, decimal? percentagemAtual)
    {
        var resultado = new Dictionary<int, (decimal, bool)>();
        long produzido = 0;
        foreach (var producao in producoes.Pagina.Items.OrderBy(p => p.InicioEm ?? DateOnly.MaxValue).ThenBy(p => p.JobOnId))
        {
            produzido += producao.ProducaoBruta ?? 0;
            if (producao.UtilizacaoDepois is { } medido)
            {
                resultado[producao.JobOnId] = (medido, false);
            }
            else if (producao.ProducaoBruta is not null && producoes.TotalBruta is > 0 && percentagemAtual is { } atual)
            {
                resultado[producao.JobOnId] = (Math.Round(atual * produzido / producoes.TotalBruta.Value, 2), true);
            }
        }
        return resultado;
    }
}

/// <summary>O quadro «Utilização por produção»: as produções da ferramenta e a última leitura do uso.</summary>
public sealed record UtilizacaoProducoesView(FerramentaProducoes Producoes, FerramentaUtilizacao? Leitura);
