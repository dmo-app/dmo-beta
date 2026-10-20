using System.Security.Claims;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// O que a escolha «Manter / Colocar de parte» e a aprovação, as duas na
/// tabela da Comparação do Peso, partilham: quem grava, o regresso à
/// página de onde se veio e as mensagens (D10, 19/10/2026).
/// </summary>
public static class CmDeParte
{
    /// <summary>O parâmetro do endereço com o resultado da última gravação.</summary>
    public const string Parametro = "cm_de_parte";

    /// <summary>
    /// Quem grava, como fica no registo: o nome da sessão, senão o email,
    /// senão o número de operador; null sem identidade (nada se grava).
    /// </summary>
    public static string? Quem(ClaimsPrincipal user) =>
        new[] { DmoClaimTypes.DisplayName, DmoClaimTypes.Email, DmoClaimTypes.OperatorId }
            .Select(type => user.FindFirst(type)?.Value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrEmpty(value)) is { } quem
            ? (quem.Length > 254 ? quem[..254] : quem)
            : null;

    /// <summary>
    /// O endereço para voltar depois de gravar: o endereço local dado (só
    /// dentro do Controlo), sem o resultado anterior e com o novo (sem
    /// nenhum quando nada mudou: um clique repetido não mostra mensagem).
    /// </summary>
    public static string Voltar(IUrlHelper url, string? voltar, string padrao, string? resultado)
    {
        var destino = !string.IsNullOrEmpty(voltar) && url.IsLocalUrl(voltar)
            && voltar.StartsWith("/Controlo/", StringComparison.Ordinal)
            ? voltar
            : padrao;
        var semFragmento = destino.Split('#')[0];
        var partes = semFragmento.Split('?', 2);
        var query = partes.Length > 1 ? QueryHelpers.ParseQuery(partes[1]) : new();
        query.Remove(Parametro);
        var pares = query.SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)));
        if (resultado is not null)
        {
            pares = pares.Append(new KeyValuePair<string, string?>(Parametro, resultado));
        }

        return QueryHelpers.AddQueryString(partes[0], pares);
    }

    /// <summary>A mensagem de um resultado («ok-…» fecha sozinha; o resto fica até se fechar).</summary>
    public static (string Tipo, string Texto)? Mensagem(string? resultado) => resultado switch
    {
        "ok-de-parte" => ("ok", "CM posta de parte: fica por aprovar."),
        "ok-manter" => ("ok", "CM a manter."),
        "ok-sem-escolha" => ("ok", "Escolha retirada."),
        "ok-aprovado" => ("ok", "Aprovado: a CM fica de parte."),
        "ok-nao-aprovado" => ("ok", "Não aprovado: a CM continua."),
        "ok-por-aprovar" => ("ok", "A CM voltou a ficar por aprovar."),
        "mudou" => ("erro", "A escolha desta CM mudou entretanto. Nada foi gravado: a lista mostra a escolha nova."),
        "nao-existe" => ("erro", "Esta leitura da comparação já não existe. Nada foi gravado."),
        "sem-identidade" => ("erro", "Nada foi gravado: a sessão não identifica quem grava. Termine a sessão e volte a entrar."),
        _ => null
    };
}
