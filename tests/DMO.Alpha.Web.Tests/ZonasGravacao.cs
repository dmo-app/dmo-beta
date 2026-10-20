using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// E11 (beta.189): o GET que segue uma gravação leva X-Dmo-Zonas e o
/// servidor responde só com essas zonas. Peças comuns dos testes do Resumo
/// e dos Pegamentos: o pedido com o cabeçalho e as leituras contadas das
/// consultas que essas zonas não precisam.
/// </summary>
internal static class ZonasGravacao
{
    public static HttpRequestMessage Pedido(string url, string zonas)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Requested-With", "fetch");
        request.Headers.Add("X-Dmo-Zonas", zonas);
        return request;
    }

    public static string? Regiao(HttpResponseMessage response) =>
        response.Headers.TryGetValues("X-Dmo-Region", out var values) ? values.Single() : null;

    /// <summary>O HTML sem os tokens antifalsificação (mudam a cada pedido).</summary>
    public static string SemTokens(string html) =>
        Regex.Replace(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"[^\"]*\"", "name=\"__RequestVerificationToken\"");

    /// <summary>As raízes de topo da resposta (os ids, pela ordem).</summary>
    public static IReadOnlyList<string> Raizes(string html)
    {
        var ids = new List<string>();
        var depth = 0;
        foreach (Match tag in Regex.Matches(html, "<(/?)(div|section|summary)\\b([^>]*)>"))
        {
            if (tag.Groups[1].Value == "/")
            {
                depth--;
                continue;
            }
            if (depth == 0)
            {
                ids.Add(Regex.Match(tag.Groups[3].Value, "id=\"([^\"]+)\"").Groups[1].Value);
            }
            depth++;
        }
        return ids;
    }
}

internal sealed class LeiturasContadas
{
    public int Ferramentas;
    public int Peso;
    public int Pegamentos;
    public int FolhasPegamentos;

    public void Zerar()
    {
        Ferramentas = 0;
        Peso = 0;
        Pegamentos = 0;
        FolhasPegamentos = 0;
    }
}

internal sealed class ToolLookupContado(IToolLookupQuery inner, LeiturasContadas leituras) : IToolLookupQuery
{
    public Task<ToolLookupResult?> FindByToolIdAsync(string toolId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Ferramentas);
        return inner.FindByToolIdAsync(toolId, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, ToolLookupResult>> FindByToolIdsAsync(IEnumerable<string?> toolIds, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Ferramentas);
        return inner.FindByToolIdsAsync(toolIds, cancellationToken);
    }

    public Task<ToolLookupResult?> FindByReferenceAsync(string reference, ToolType type, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Ferramentas);
        return inner.FindByReferenceAsync(reference, type, cancellationToken);
    }
}

internal sealed class PesoQueryContado(IPesoQuery inner, LeiturasContadas leituras) : IPesoQuery
{
    public Task<PesoReadModel?> FindPesoAsync(int pesoId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Peso);
        return inner.FindPesoAsync(pesoId, cancellationToken);
    }

    public Task<PesoReadModel?> FindByControloAsync(int controloId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Peso);
        return inner.FindByControloAsync(controloId, cancellationToken);
    }

    public Task<PesoReadModel?> FindCurrentByControloAsync(int controloId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Peso);
        return inner.FindCurrentByControloAsync(controloId, cancellationToken);
    }

    public Task<IReadOnlyList<PesoReadModel>> ListByControloAsync(int controloId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Peso);
        return inner.ListByControloAsync(controloId, cancellationToken);
    }
}

internal sealed class PegamentosQueryContado(IPegamentosQuery inner, LeiturasContadas leituras) : IPegamentosQuery
{
    public Task<PegamentosSheetReadModel?> FindSheetByControloAsync(int controloId, int? pegamentosId = null, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.FolhasPegamentos);
        return inner.FindSheetByControloAsync(controloId, pegamentosId, cancellationToken);
    }

    public Task<PegamentosSheetReadModel?> FindSheetByPegamentosAsync(int pegamentosId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.FolhasPegamentos);
        return inner.FindSheetByPegamentosAsync(pegamentosId, cancellationToken);
    }

    public Task<IReadOnlyList<PegamentosReadModel>> ListSheetsByControloAsync(int controloId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref leituras.Pegamentos);
        return inner.ListSheetsByControloAsync(controloId, cancellationToken);
    }
}
