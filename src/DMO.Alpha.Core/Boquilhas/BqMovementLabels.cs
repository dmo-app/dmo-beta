namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// A TRADUÇÃO dos movimentos de Boquilhas para a língua do operador (a A5
/// repair, o Owner 14/10: o papel mostrava «EntradaSemReparacao» cru).
///
/// Os read models falam DUAS línguas legítimas e é isso que enganava o
/// switch antigo, que só conhecia uma:
/// 1. O TOKEN de armazenamento (<c>saida</c> / <c>entrada</c> /
///    <c>entrada_sem_reparacao</c>) — é o que o <c>BoquilhasRegistoQuery</c>
///    devolve via <c>BqMovementTypeTokens.ToStorage</c>.
/// 2. O NOME do enum (<c>Saida</c> / <c>Entrada</c> /
///    <c>EntradaSemReparacao</c>) — é o que o <c>BqToolHistoryQuery</c>
///    devolve via <c>mov.Type.ToString()</c>.
///
/// O switch velho comparava tudo contra a forma minúscula do token: os dois
/// nomes de uma só palavra (<c>Saida</c>, <c>Entrada</c>) sobreviviam por
/// acaso (minúsculos batem certo), mas <c>EntradaSemReparacao</c> minúsculo
/// não bate com <c>entrada_sem_reparacao</c> e caía no <c>_ =&gt; type</c>,
/// i.e. ia CRU para o ecrã e para os 3 PDFs. Esta classe é o ÚNICO lugar do
/// produto onde essas duas vocabulários se traduzem — a folha
/// (Boquilhas/Index.cshtml) e o papel (BqHistoryPdfGenerator) chamam cá e
/// nunca repetem a lista.
/// </summary>
public static class BqMovementLabels
{
    /// <summary>O rótulo em PT de um movimento, dado o valor tal como o read
    /// model o escreve (token de armazenamento OU nome do enum). Um valor
    /// desconhecido sai TAL E QUAL — a casa nunca inventa texto por cima de
    /// dados que não reconhece.</summary>
    public static string Of(string? movementType)
    {
        if (string.IsNullOrWhiteSpace(movementType))
        {
            return "—";
        }

        var token = movementType.Trim();

        // 1. O vocabulário do HISTÓRICO: o nome do enum. Só se o valor começar
        //    por letra e corresponder a um membro DEFINIDO — um número perdido
        //    não se disfarça de rótulo.
        if (char.IsLetter(token[0])
            && Enum.TryParse<BqMovementType>(token, ignoreCase: true, out var byName)
            && Enum.IsDefined(byName))
        {
            return Of(byName);
        }

        // 2. O vocabulário do REGISTO / armazenamento: o token canónico.
        return token.ToLowerInvariant() switch
        {
            "saida" => "Saída",
            "entrada" => "Entrada",
            "entrada_sem_reparacao" => "Entrada sem reparação",
            _ => token
        };
    }

    /// <summary>O rótulo em PT de um movimento dado o tipo forte — a forma
    /// canónica, para quem tem o enum na mão (o backend, os testes).</summary>
    public static string Of(BqMovementType type) => type switch
    {
        BqMovementType.Saida => "Saída",
        BqMovementType.Entrada => "Entrada",
        BqMovementType.EntradaSemReparacao => "Entrada sem reparação",
        _ => type.ToString()
    };
}
