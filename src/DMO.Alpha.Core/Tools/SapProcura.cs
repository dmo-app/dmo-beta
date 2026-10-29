using System.Globalization;

namespace DMO.Alpha.Core.Tools;

/// <summary>O que vai no meio do código de procura: o nome técnico ou a referência da ferramenta.</summary>
public enum SapProcuraMeio
{
    NomeTecnico,
    Referencia,
    /// <summary>O nome técnico da BQ até ao NX/PX/SX (ABC002NX3460 → ABC002NX): os algarismos a seguir são características.</summary>
    NomeTecnicoAteProcesso
}

/// <summary>
/// A TABELA DOS CÓDIGOS DE PROCURA NO SAP (Diogo, 28/10/2026: «uma tabela com
/// os códigos que usamos para B1, B2… se mais tarde mudarem, na B1 as BQ têm NX
/// passa a NZ»; «não deve ser só para BQ»; «eu pedi para alterar no UI»): para
/// cada tipo e Linha, como se monta o código que o script procura na ZPPG.
/// Antes + (nome técnico ou referência) + Depois + lote (dois algarismos).
/// A Linha vazia vale para as Linhas sem regra própria.
/// </summary>
public sealed record SapProcuraRegra(ToolType Tipo, string Linha, string Antes, SapProcuraMeio Meio, string Depois, bool ComLote)
{
    public const int TextoMaximo = 20;

    /// <summary>A regra de sempre: o nome técnico seguido do lote (M544744MPPSX + 11).</summary>
    /// A BQ procura-se pelo início e o lote vem no fim (Diogo, 28/10/2026 11:56:
    /// «AB + referência + NX + * + lote»; 29/10 «ABC002NX*LOTE»; o SAP tem ABC002NX3460164).
    public static SapProcuraRegra PorOmissao(ToolType tipo) => tipo == ToolType.Bq
        ? new(tipo, string.Empty, string.Empty, SapProcuraMeio.NomeTecnicoAteProcesso, "*", true)
        : new(tipo, string.Empty, string.Empty, SapProcuraMeio.NomeTecnico, string.Empty, true);


    /// <summary>O código desta regra para uma ferramenta; null quando falta o nome técnico, a referência ou o lote em número.</summary>
    public string? Codigo(string? nomeTecnico, string? referencia, string? lote)
    {
        var meio = Meio switch
        {
            SapProcuraMeio.Referencia => Limpo(referencia),
            SapProcuraMeio.NomeTecnicoAteProcesso => AteProcesso(Limpo(nomeTecnico)),
            _ => Limpo(nomeTecnico)
        };
        if (meio.Length == 0)
        {
            return null;
        }

        var codigo = Limpo(Antes) + meio + Limpo(Depois);
        if (ComLote)
        {
            if (!int.TryParse(lote?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
            {
                return null;
            }

            codigo += numero.ToString("00", CultureInfo.InvariantCulture);
        }

        return codigo.Length <= FerramentaUtilizacao.CodigoMaxLength ? codigo : null;
    }

    private static string AteProcesso(string nome)
    {
        var m = System.Text.RegularExpressions.Regex.Match(nome, "^AB[A-Z0-9]{4}[A-Z]{2}");
        return m.Success ? m.Value : nome;
    }

    /// <summary>Como fica, com nomes em vez de valores (para a tabela do Admin).</summary>
    public string Exemplo =>
        Limpo(Antes) + Meio switch
        {
            SapProcuraMeio.Referencia => "[referência]",
            SapProcuraMeio.NomeTecnicoAteProcesso => "[nome técnico até NX/PX]",
            _ => "[nome técnico]"
        } + Limpo(Depois) + (ComLote ? "[lote]" : string.Empty);

    internal static string Limpo(string? texto) =>
        (texto ?? "").Trim().ToUpperInvariant().Replace(".", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
}

public static class SapProcura
{
    /// <summary>O prefixo em ModuleSettings: Sap:Procura:CM:B1 (Linha vazia = todas).</summary>
    public const string Chave = "Sap:Procura:";

    /// <summary>
    /// O código a procurar para uma ferramenta: o «Código SAP» escrito à mão
    /// ganha; senão a regra da primeira Linha dela que tiver regra; senão a
    /// regra geral do tipo; senão o nome técnico + lote.
    /// </summary>
    public static string? Codigo(
        IReadOnlyCollection<SapProcuraRegra> regras, ToolType tipo, IEnumerable<string> linhas,
        string? codigoAMao, string? nomeTecnico, string? referencia, string? lote)
    {
        var aMao = SapProcuraRegra.Limpo(codigoAMao);
        if (aMao.Length > 0)
        {
            return aMao.Length <= FerramentaUtilizacao.CodigoMaxLength ? aMao : null;
        }

        var doTipo = regras.Where(r => r.Tipo == tipo).ToList();
        var regra = linhas
            .Select(l => doTipo.FirstOrDefault(r => string.Equals(r.Linha, l, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(r => r is not null)
            ?? doTipo.FirstOrDefault(r => r.Linha.Length == 0)
            ?? SapProcuraRegra.PorOmissao(tipo);
        return regra.Codigo(nomeTecnico, referencia, lote);
    }
}

public enum SapProcuraGravacao
{
    Gravado,
    LinhaInvalida,
    TextoInvalido
}

public interface ISapProcuraRegras
{
    /// <summary>As regras guardadas; as três gerais (CM, MF, BQ) estão sempre, com o nome técnico + lote quando ninguém as mudou.</summary>
    Task<IReadOnlyList<SapProcuraRegra>> LerAsync(CancellationToken cancellationToken = default);

    /// <summary>Grava (ou troca) uma regra.</summary>
    Task<SapProcuraGravacao> GravarAsync(SapProcuraRegra regra, CancellationToken cancellationToken = default);

    /// <summary>O código que o script procura para a ferramenta e o «Código SAP» escrito nela (null quando vem da tabela).</summary>
    Task<(string? Codigo, string? AMao)> DaFerramentaAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>Grava o «Código SAP» da ferramenta; vazio volta à tabela. False quando a ferramenta não existe ou o código é inválido.</summary>
    Task<bool> GravarCodigoAsync(string toolId, string? codigo, CancellationToken cancellationToken = default);

    /// <summary>Tira a regra de uma Linha (a geral do tipo volta ao nome técnico + lote).</summary>
    Task ApagarAsync(ToolType tipo, string linha, CancellationToken cancellationToken = default);
}
