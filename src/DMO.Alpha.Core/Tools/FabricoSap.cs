using System.Globalization;

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Uma linha do relatório de fabrico do SAP («6- Manufacturing Selected»,
/// Diogo, 21/10/2026): o material (a referência com o sufixo do SAP,
/// 5538T324BRA), a linha (a máquina, C3), as datas de início e fim e o que se
/// produziu. Não tem ferramentas: liga-se ao Job On pela referência, pela
/// máquina e pelas datas, e a ferramenta vem do Job On (a CM, o MF e a BQ;
/// Diogo: «a única forma de associar a um Job On é usar a data de início e
/// fim juntamente com o CM, MF e BQ»). Ler o mesmo relatório outra vez
/// atualiza a mesma linha.
/// </summary>
public sealed class SapFabrico
{
    public const int MaterialMaxLength = 64;

    public const int LinhaMaxLength = 32;

    public long Id { get; set; }

    public string Material { get; set; } = null!;

    public string Linha { get; set; } = null!;

    public DateOnly InicioEm { get; set; }

    public DateOnly FimEm { get; set; }

    /// <summary>A produção bruta (Gross Prod), em peças.</summary>
    public long? ProducaoBruta { get; set; }

    /// <summary>A produção líquida (Net Prod.), em peças.</summary>
    public long? ProducaoLiquida { get; set; }

    /// <summary>O peso médio do vidro na ordem (Avg weight), em gramas.</summary>
    public decimal? PesoMedioGramas { get; set; }

    /// <summary>Os dias de produção da ordem (Prod. Days).</summary>
    public int? DiasProducao { get; set; }

    public DateTime LidoEmUtc { get; set; }

    public string? LidoPor { get; set; }

    public Guid? LidoPorUserId { get; set; }

    public string? Ficheiro { get; set; }

    public DateTime? FicheiroModificadoEmUtc { get; set; }
}

/// <summary>Uma linha lida do relatório de fabrico.</summary>
public sealed record FabricoSapLinha(string Material, string Linha, DateOnly InicioEm, DateOnly FimEm, long? ProducaoBruta, long? ProducaoLiquida,
    decimal? PesoMedioGramas = null, int? DiasProducao = null);

/// <summary>
/// A leitura do relatório de fabrico do SAP, em texto separado por
/// tabulações (como o SAP o grava) ou em HTML: o cabeçalho com «Material»,
/// «Line», «Begin Date» e «End Date» e pelo menos uma das produções («Gross
/// Prod», «Net Prod.»). As datas vêm como 21.01.2025 e os números no formato
/// português (1.404.234). Uma linha ilegível fica de fora.
/// </summary>
public static class FabricoSapExportacao
{
    private static readonly string[] FormatosData = ["dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"];

    public sealed record Leitura(bool CabecalhoEncontrado, IReadOnlyList<FabricoSapLinha> Linhas);

    public static Leitura Ler(string? conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return new Leitura(false, []);
        }

        var tabela = UsoSapExportacao.Tabela(conteudo);
        for (var i = 0; i < tabela.Count; i++)
        {
            var cabecalho = tabela[i];
            var material = UsoSapExportacao.Coluna(cabecalho, c => c is "material");
            var linha = UsoSapExportacao.Coluna(cabecalho, c => c is "line" or "linha");
            var inicio = UsoSapExportacao.Coluna(cabecalho, c => c is "begin date" or "data início" or "data inicio");
            var fim = UsoSapExportacao.Coluna(cabecalho, c => c is "end date" or "data fim");
            var bruta = UsoSapExportacao.Coluna(cabecalho, c => c.StartsWith("gross prod", StringComparison.Ordinal));
            var liquida = UsoSapExportacao.Coluna(cabecalho, c => c.StartsWith("net prod", StringComparison.Ordinal));
            var peso = UsoSapExportacao.Coluna(cabecalho, c => c is "avg weight" or "peso médio" or "peso medio");
            var dias = UsoSapExportacao.Coluna(cabecalho, c => c is "prod. days" or "prod days" or "dias");
            if (material < 0 || linha < 0 || inicio < 0 || fim < 0 || (bruta < 0 && liquida < 0))
            {
                continue;
            }

            var linhas = new List<FabricoSapLinha>();
            foreach (var celulas in tabela.Skip(i + 1))
            {
                var mat = UsoSapExportacao.Celula_(celulas, material).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
                var lin = UsoSapExportacao.Celula_(celulas, linha).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
                if (mat.Length is 0 or > SapFabrico.MaterialMaxLength || lin.Length is 0 or > SapFabrico.LinhaMaxLength
                    || !Data(UsoSapExportacao.Celula_(celulas, inicio), out var de)
                    || !Data(UsoSapExportacao.Celula_(celulas, fim), out var ate))
                {
                    continue;
                }

                linhas.Add(new FabricoSapLinha(
                    mat,
                    lin,
                    de,
                    ate < de ? de : ate,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, bruta), out var b) ? (long)Math.Round(b) : null,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, liquida), out var l) ? (long)Math.Round(l) : null,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, peso), out var p) && p > 0 && p < 100_000 ? Math.Round(p, 3) : null,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, dias), out var d) && d < 10_000 ? (int)Math.Round(d) : null));
            }

            return new Leitura(true, linhas);
        }

        return new Leitura(false, []);
    }

    private static bool Data(string texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto.Trim(), FormatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out data);

    /// <summary>
    /// Se a linha do SAP é desta produção: o material começa pela referência
    /// do Job On (5538T324BRA é da 5538T324), a linha é a máquina e as datas
    /// cruzam-se com as do Job On. Sem fim, o Job On ainda está na máquina:
    /// conta tudo desde o início.
    /// </summary>
    public static bool Corresponde(string material, string linha, DateOnly inicioSap, DateOnly fimSap,
        string referencia, string maquina, DateOnly? inicio, DateOnly? fim)
    {
        if (inicio is not { } de || string.IsNullOrWhiteSpace(referencia))
        {
            return false;
        }

        var ate = fim is { } f ? (f >= de ? f : de) : DateOnly.MaxValue;
        return material.StartsWith(referencia.Trim().ToUpperInvariant(), StringComparison.Ordinal)
            && string.Equals(linha, maquina.Trim(), StringComparison.OrdinalIgnoreCase)
            && inicioSap <= ate && fimSap >= de;
    }
}

/// <summary>
/// O peso do SAP de uma produção, para o Peso (Diogo, 21/10/2026: «usa a
/// data desse Job On juntamente com a referência da CM para ir buscar o
/// peso; depois calcula a média com base nos dias e peso, e a moda, o peso
/// que se repete mais»). Cada ordem do SAP conta com os dias que tem dentro
/// das datas do Job On.
/// </summary>
public sealed record PesoSapResumo(decimal MediaGramas, decimal ModaGramas, int Dias, int Ordens);

public static class FabricoSapPeso
{
    /// <summary>
    /// A média pesada pelos dias e a moda (o peso com mais dias; empatados,
    /// o mais recente) das ordens com peso. Os dias de uma ordem são os que
    /// ficam dentro do Job On, contados como o SAP os conta (fim menos
    /// início; pelo menos 1). Nenhum resumo sem ordens com peso.
    /// </summary>
    public static PesoSapResumo? Resumo(IEnumerable<SapFabrico> ordens, DateOnly inicio, DateOnly? fim)
    {
        var ate = fim is { } f && f >= inicio ? f : DateOnly.MaxValue;
        var comPeso = ordens
            .Where(o => o.PesoMedioGramas is > 0)
            .Select(o =>
            {
                var de = o.InicioEm > inicio ? o.InicioEm : inicio;
                var aFim = o.FimEm < ate ? o.FimEm : ate;
                var dias = Math.Max(1, aFim.DayNumber - de.DayNumber);
                return (Peso: o.PesoMedioGramas!.Value, Dias: dias, o.InicioEm);
            })
            .ToList();
        if (comPeso.Count == 0)
        {
            return null;
        }

        var totalDias = comPeso.Sum(o => o.Dias);
        var media = Math.Round(comPeso.Sum(o => o.Peso * o.Dias) / totalDias, 2);
        var moda = comPeso
            .GroupBy(o => o.Peso)
            .OrderByDescending(g => g.Sum(o => o.Dias))
            .ThenByDescending(g => g.Max(o => o.InicioEm))
            .First().Key;
        return new PesoSapResumo(media, moda, totalDias, comPeso.Count);
    }
}

/// <summary>O que se leu do relatório de fabrico: as linhas e quantas eram novas.</summary>
public sealed record ImportarFabricoSapOutcome(int Linhas, int Novas);

/// <summary>
/// Uma produção (Job On) em que a ferramenta esteve, com o que o SAP diz que
/// se produziu nela: a soma das linhas do relatório que lhe correspondem.
/// Sem linha do SAP, as produções ficam vazias. O delta é o que a
/// utilização subiu com ela: a primeira leitura a partir do fim menos a
/// última até ao início (Diogo, 21/10/2026: «o delta e o total»).
/// </summary>
public sealed record FerramentaProducao(
    int JobOnId,
    string Referencia,
    string Producao,
    string Maquina,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    long? ProducaoBruta,
    long? ProducaoLiquida,
    int LinhasSap,
    decimal? UtilizacaoAntes = null,
    decimal? UtilizacaoDepois = null)
{
    /// <summary>Quanto a utilização subiu nesta produção (pontos percentuais); nenhum sem as duas leituras.</summary>
    public decimal? DeltaUtilizacao => UtilizacaoAntes is { } antes && UtilizacaoDepois is { } depois ? depois - antes : null;
}

/// <summary>
/// O quadro de detalhes da utilização na ficha: a página das produções e o
/// total de todas (quantas e o que o SAP diz que se produziu nelas).
/// </summary>
public sealed record FerramentaProducoes(
    DMO.Alpha.Core.Paging.PagedList<FerramentaProducao> Pagina,
    long? TotalBruta,
    long? TotalLiquida);

/// <summary>
/// O fabrico pelo SAP: gravar o relatório lido da pasta do SAP e as
/// produções de uma ferramenta, para a ficha.
/// </summary>
public interface IFerramentasFabricoSap
{
    Task<ImportarFabricoSapOutcome> ImportarAsync(IReadOnlyList<FabricoSapLinha> linhas, string? ficheiro, DateTime? ficheiroModificadoEmUtc, CancellationToken cancellationToken = default);

    /// <summary>A data do último relatório lido, quando o browser a deu.</summary>
    Task<DateTime?> UltimoFicheiroAsync(CancellationToken cancellationToken = default);

    /// <summary>O peso do SAP de cada Job On (a referência, a máquina e as datas); fica de fora o Job On sem ordens com peso.</summary>
    Task<IReadOnlyDictionary<int, PesoSapResumo>> PesoDosJobOnsAsync(IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default);

    /// <summary>Os Job On em que a ferramenta foi a CM, o MF ou a BQ, do mais recente para o mais antigo.</summary>
    Task<FerramentaProducoes> ProducoesAsync(string toolId, int? pagina, int porPagina, CancellationToken cancellationToken = default);
}
