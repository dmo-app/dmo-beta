using Microsoft.AspNetCore.Http;

namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// O servidor desenha só as zonas que mudam depois de uma gravação (E11,
/// beta.189). Um formulário POST dentro de <c>data-dmo-zonas</c> envia o
/// cabeçalho <c>X-Dmo-Zonas</c> (os ids das zonas, separados por espaços) e
/// o fetch do componente leva-o também ao GET do redirect (PRG). A página
/// que o conhece responde a esse GET só com essas zonas, pela ordem pedida,
/// com os mesmos partials da página completa e com <c>X-Dmo-Region</c> igual
/// ao pedido. Uma zona que a página não conhece, ou que não pode desenhar
/// nesse estado, faz responder com a página inteira (sem
/// <c>X-Dmo-Region</c>), como antes. O GET é o mesmo handler da página: as
/// mesmas verificações de autenticação e de entrada no módulo.
/// Ver docs/ui-reference/atualizar-so-o-que-muda.md.
/// </summary>
public static class DmoZonasPedidas
{
    /// <summary>O cabeçalho do pedido com as zonas.</summary>
    public const string Pedido = "X-Dmo-Zonas";

    /// <summary>O cabeçalho da resposta com as zonas desenhadas (o mesmo do Codex).</summary>
    public const string Resposta = "X-Dmo-Region";

    private const int MaxLength = 512;

    /// <summary>
    /// As zonas pedidas por um GET, se todas forem conhecidas e não houver
    /// repetidas; null sem o cabeçalho ou com alguma desconhecida (a página
    /// inteira).
    /// </summary>
    public static IReadOnlyList<string>? Ler(HttpRequest request, Func<string, bool> conhecida)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return null;
        }

        var valor = request.Headers[Pedido].ToString();
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > MaxLength)
        {
            return null;
        }

        var ids = valor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return ids.Length > 0
            && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
            && ids.All(conhecida)
            ? ids
            : null;
    }

    /// <summary>Marca a resposta só com as zonas: X-Dmo-Region igual ao pedido.</summary>
    public static void Marcar(HttpResponse response, IReadOnlyList<string> zonas)
    {
        response.Headers[Resposta] = string.Join(' ', zonas);
        response.Headers.Append("Vary", Pedido);
    }
}
