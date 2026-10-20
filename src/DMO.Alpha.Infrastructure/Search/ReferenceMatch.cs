using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Query;

namespace DMO.Alpha.Infrastructure.Search;

/// <summary>
/// A regra única da procura de uma referência (Diogo, 18/10/2026): pelo
/// início da referência completa ou da parte BQ, nunca por um carácter a
/// meio, sem distinção de maiúsculas. «0» encontra a 0132T173 e nunca a
/// 1075; «Y2» encontra a 6018Y232; «T17» encontra a 5447T173; «018» não
/// encontra nada. A parte BQ é a que começa na primeira letra, por isso um
/// texto que começa por letra tem de vir logo a seguir aos algarismos do
/// início («T1» encontra a 0132T173 e nunca a ST100, onde o T é a segunda
/// letra; sweep de 19/10/2026, DB-8); um texto que começa por algarismo só
/// pode estar no início. É a regra que a procura das Ferramentas e a do Job
/// On já seguiam, agora num só sítio.
/// <para>
/// <see cref="Predicate{T}"/> constrói a expressão para o EF (traduzida no
/// PostgreSQL; um método C# dentro de um Where só funcionava no InMemory) e
/// <see cref="Matches"/> é a mesma regra em memória, para decidir depois da
/// leitura porque é que uma linha apareceu.
/// </para>
/// </summary>
public static class ReferenceMatch
{
    /// <summary>O texto procurado sem espaços à volta e em maiúsculas; null quando fica vazio.</summary>
    public static string? Normalize(string? term)
    {
        var wanted = term?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(wanted) ? null : wanted;
    }

    /// <summary>A regra em memória: o mesmo resultado que <see cref="Predicate{T}"/> dá na base.</summary>
    public static bool Matches(string? value, string? term)
    {
        if (value is null || Normalize(term) is not { } wanted)
        {
            return false;
        }

        var upper = value.ToUpperInvariant();
        return char.IsLetter(wanted[0])
            ? Regex.IsMatch(upper, BqPartPattern(wanted))
            : upper.StartsWith(wanted, StringComparison.Ordinal);
    }

    // O texto no início da parte BQ: nenhuma letra antes dele. O Npgsql
    // traduz o Regex.IsMatch para o operador ~ do PostgreSQL, com o padrão
    // como parâmetro; o Regex.Escape faz de um ponto um ponto.
    private static string BqPartPattern(string wanted) => "^[^A-Z]*" + Regex.Escape(wanted);

    /// <summary>
    /// A condição «a referência de <paramref name="selector"/> corresponde a
    /// <paramref name="term"/>», pronta para um Where do EF. O texto vai como
    /// parâmetro da consulta (nunca como constante), e a escolha entre o
    /// início e a parte BQ faz-se aqui, em C#, fora da expressão.
    /// </summary>
    public static Expression<Func<T, bool>> Predicate<T>(Expression<Func<T, string?>> selector, string term)
    {
        var wanted = Normalize(term) ?? throw new ArgumentException("O texto procurado está vazio.", nameof(term));
        // Com letra, o padrão já inclui o início (uma referência que começa
        // pela parte BQ, como a T194, não tem algarismos antes dela).
        var pattern = BqPartPattern(wanted);
        Expression<Func<string?, bool>> match = char.IsLetter(wanted[0])
            ? value => value != null && Regex.IsMatch(value.ToUpper(), pattern)
            : value => value != null && value.ToUpper().StartsWith(wanted);
        var body = ReplacingExpressionVisitor.Replace(match.Parameters[0], selector.Body, match.Body);
        return Expression.Lambda<Func<T, bool>>(body, selector.Parameters);
    }

    /// <summary>
    /// Junta condições com OU, para uma procura de texto que olha para a
    /// referência e para outros campos (o lote, a máquina, o nº...).
    /// </summary>
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rightBody = ReplacingExpressionVisitor.Replace(right.Parameters[0], parameter, right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left.Body, rightBody), parameter);
    }
}
