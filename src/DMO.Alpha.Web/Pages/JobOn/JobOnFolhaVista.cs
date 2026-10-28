using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// O que o envio do formulário do Job On trouxe da folha da fábrica: os campos
/// escritos (<c>Folha.*</c>, por chave do catálogo) e as linhas CAL
/// (<c>Cal[i].*</c>), como foram escritos. Os dois são null quando o envio não
/// traz nenhum <c>Folha.*</c> (um pedido que só muda as ferramentas): a folha
/// fica como está.
/// </summary>
public sealed record EnvioDaFolha(
    IReadOnlyDictionary<string, string?>? Folha,
    IReadOnlyList<LinhaCalEnvio>? Cal)
{
    private static readonly System.Text.RegularExpressions.Regex ChaveCal =
        new(@"^Cal\[(\d{1,4})\]\.(?:Padrao|Designacao|Valor|QtdMaq)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// A folha enviada tem alguma coisa escrita (um campo com texto ou uma
    /// linha CAL que fica): uma recusa volta à página a partir do envio, para
    /// não se perder o que se escreveu (não cabe no endereço).
    /// </summary>
    public bool TemEscrito => JobOnFolhaRegras.TemEscrito(
        Folha is null ? null : JobOnFolhaRegras.NormalizarFolha(Folha),
        Cal?.Select(JobOnFolhaRegras.NormalizarLinha).ToList());

    /// <summary>
    /// Lê o envio. Um campo do catálogo que não vem fica vazio; as linhas CAL
    /// vêm pela ordem do índice <c>Cal[i]</c>, sem as que não têm nada escrito.
    /// </summary>
    public static EnvioDaFolha Ler(IFormCollection form)
    {
        var temFolha = form.Keys.Any(k => k.StartsWith(JobOnFolhaCampos.PrefixoDoFormulario, StringComparison.Ordinal));
        if (!temFolha)
        {
            return new EnvioDaFolha(null, null);
        }

        var folha = JobOnFolhaCampos.Todos.ToDictionary(
            c => c.Chave,
            c => form.TryGetValue(c.NomeNoFormulario, out var valor) ? (string?)valor.ToString() : null,
            StringComparer.Ordinal);
        var cal = form.Keys
            .Select(k => ChaveCal.Match(k))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .Select(i => new LinhaCalEnvio(
                Campo(form, $"Cal[{i}].Designacao"),
                Campo(form, $"Cal[{i}].Valor"),
                Campo(form, $"Cal[{i}].QtdMaq"),
                Campo(form, $"Cal[{i}].Padrao")))
            // Uma linha sem nada escrito (a linha vazia do fim, ou uma que se
            // apagou para a retirar) não conta, nem para a posição numa recusa.
            .Where(l => !string.IsNullOrWhiteSpace(l.Designacao)
                || !string.IsNullOrWhiteSpace(l.Valor)
                || !string.IsNullOrWhiteSpace(l.QtdMaq))
            .ToList();
        return new EnvioDaFolha(folha, cal);
    }

    private static string? Campo(IFormCollection form, string nome) =>
        form.TryGetValue(nome, out var valor) ? valor.ToString() : null;
}

/// <summary>
/// Uma linha CAL como a folha a mostra: o índice no formulário (<c>Cal[i]</c>),
/// a marca de por omissão (<c>Cal[i].Padrao</c>) e o que está escrito.
/// <see cref="Extra"/> marca a linha vazia do fim, que serve para acrescentar
/// (retirar uma linha é apagar o que tem escrito).
/// </summary>
public sealed record FolhaCalLinhaVista(
    int Indice,
    string? Padrao,
    string? Designacao,
    string? Valor,
    string? QtdMaq,
    bool Extra = false)
{
    /// <summary>O nome da linha nos rótulos: a designação, ou «linha N» sem ela (N a contar de 1).</summary>
    public string Nome => string.IsNullOrWhiteSpace(Designacao) ? $"linha {Indice + 1}" : Designacao.Trim();

    /// <summary>O id de um campo da linha no ecrã: <c>jobon-cal-0-designacao</c>, <c>jobon-cal-0-valor</c>, <c>jobon-cal-0-qtd-maq</c>.</summary>
    public string IdDe(JobOnFolhaCampoCal campo) => FolhaDoJobOnVista.IdCal(Indice, campo);
}

/// <summary>
/// A folha da fábrica no formulário do Job On: o valor de cada campo do
/// catálogo, as linhas CAL e o campo recusado por ser longo demais (que fica
/// marcado e com o foco). Abre com o que está gravado (Editar), com a da
/// produção de origem (Duplicar), vazia (Novo) ou, depois de uma recusa, com o
/// que se escreveu.
/// </summary>
public sealed class FolhaDoJobOnVista
{
    /// <summary>
    /// Os campos da folha que o Duplicar copia mas que quase sempre mudam de uma
    /// produção para a outra (Diogo): as utilizações das ferramentas. Levam a
    /// marca «Confirmar», sem bloquear nada.
    /// </summary>
    public static IReadOnlySet<string> ChavesAConfirmar { get; } =
        new HashSet<string>(["cm_utilizacao", "mf_utilizacao", "bq_utilizacao"], StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, string?> _valores;

    private FolhaDoJobOnVista(
        IReadOnlyDictionary<string, string?> valores,
        IReadOnlyList<FolhaCalLinhaVista> cal,
        string? chaveRecusada,
        bool duplicada = false)
    {
        _valores = valores;
        Cal = cal;
        Duplicada = duplicada;
        ChaveRecusada = chaveRecusada is { } chave && JobOnFolhaRegras.RotuloDaChave(chave) is not null ? chave : null;
        IdInvalido = ChaveRecusada is null ? null : IdDaChave(ChaveRecusada);
    }

    /// <summary>As linhas CAL a mostrar, com a linha vazia do fim.</summary>
    public IReadOnlyList<FolhaCalLinhaVista> Cal { get; }

    /// <summary>A chave do texto longo demais (a recusa TextoLongo); null sem recusa.</summary>
    public string? ChaveRecusada { get; }

    /// <summary>O id do campo recusado no ecrã; null sem recusa.</summary>
    public string? IdInvalido { get; }

    /// <summary>A folha veio copiada da produção de origem (o Duplicar).</summary>
    public bool Duplicada { get; }

    /// <summary>O valor de um campo do catálogo (null = vazio).</summary>
    public string? Valor(string chave) => _valores.TryGetValue(chave, out var valor) ? valor : null;

    /// <summary>
    /// O campo veio copiado no Duplicar e é dos que quase sempre mudam
    /// (<see cref="ChavesAConfirmar"/>): o rótulo leva a marca «Confirmar».
    /// </summary>
    public bool Confirmar(string chave) => Duplicada && ChavesAConfirmar.Contains(chave) && Valor(chave) is not null;

    /// <summary>O campo com este id foi o recusado.</summary>
    public bool Invalido(string id) => string.Equals(IdInvalido, id, StringComparison.Ordinal);

    /// <summary>
    /// A mensagem da recusa de tamanho: «CM · Notas» tem mais de 1000
    /// caracteres; com o <paramref name="fim"/> («Nenhum Job On foi criado.»).
    /// </summary>
    public string? MensagemTextoLongo(string fim) =>
        ChaveRecusada is { } folga && JobOnFolhaRegras.EFolga(folga) && JobOnFolhaRegras.RotuloDaChave(folga) is { } rotuloFolga
            ? $"«{rotuloFolga}» só leva um número: a folga no primeiro espaço, a tolerância no do meio e a folga da flecha no último. {fim}"
        : ChaveRecusada is { } chave
        && JobOnFolhaRegras.RotuloDaChave(chave) is { } rotulo
        && JobOnFolhaRegras.MaximoDaChave(chave) is { } maximo
            ? $"«{rotulo}» tem mais de {maximo} caracteres. Encurte o texto e volte a guardar. {fim}"
            : null;

    /// <summary>A folha de um Job On novo: tudo vazio e as oito linhas CAL por omissão.</summary>
    public static FolhaDoJobOnVista Vazia() => DaLida(JobOnFolhaLida.VaziaDe(0));

    /// <summary>A folha gravada de um Job On (Editar).</summary>
    public static FolhaDoJobOnVista DaLida(JobOnFolhaLida lida) =>
        new(lida.Valores, ComExtra(JobOnFolhaRegras.CalParaAbrir(lida.Cal)), null);

    /// <summary>
    /// A folha da produção de origem no Duplicar: os campos e as linhas CAL
    /// copiados, que o Guardar grava com a produção nova.
    /// </summary>
    public static FolhaDoJobOnVista DoDuplicar(JobOnFolhaLida origem) =>
        new(origem.Valores, ComExtra(JobOnFolhaRegras.CalParaAbrir(origem.Cal)), null, duplicada: true);

    /// <summary>
    /// A folha depois de uma recusa: o que se escreveu, como se escreveu, e a
    /// linha vazia de novo no fim.
    /// </summary>
    public static FolhaDoJobOnVista DoEnvio(EnvioDaFolha envio, string? chaveRecusada) =>
        new(envio.Folha ?? new Dictionary<string, string?>(), ComExtra(envio.Cal ?? []), chaveRecusada);

    /// <summary>
    /// O «Lote FF» vazio abre com o lote do MF (Diogo, 22/10/2026: o FF quase
    /// sempre trabalha com o lote do MF; muda-se quando não). Escrito, fica.
    /// </summary>
    public FolhaDoJobOnVista ComLoteFfDoMf(string? loteMf)
    {
        if (string.IsNullOrWhiteSpace(loteMf) || !string.IsNullOrWhiteSpace(Valor("mf_lote_ff")))
        {
            return this;
        }

        var valores = new Dictionary<string, string?>(_valores, StringComparer.Ordinal) { ["mf_lote_ff"] = loteMf };
        return new FolhaDoJobOnVista(valores, Cal, ChaveRecusada, Duplicada);
    }

    /// <summary>O id no ecrã de um campo de uma linha CAL pelo índice no formulário.</summary>
    public static string IdCal(int indice, JobOnFolhaCampoCal campo) => campo switch
    {
        JobOnFolhaCampoCal.Designacao => $"jobon-cal-{indice}-designacao",
        JobOnFolhaCampoCal.Valor => $"jobon-cal-{indice}-valor",
        _ => $"jobon-cal-{indice}-qtd-maq"
    };

    // A chave recusada no ecrã: um campo do catálogo pelo seu id; uma linha CAL
    // pela posição no envio (a linha N é o índice N-1, porque a folha volta com
    // as linhas pela mesma ordem).
    private static string? IdDaChave(string chave) =>
        JobOnFolhaCampos.Encontrar(chave)?.Id
        ?? (JobOnFolhaRegras.LerChaveCal(chave) is var (linha, campo) ? IdCal(linha - 1, campo) : null);

    private static IReadOnlyList<FolhaCalLinhaVista> ComExtra(IReadOnlyList<LinhaCalEnvio> linhas) =>
        linhas
            .Select((l, i) => new FolhaCalLinhaVista(i, l.Padrao, l.Designacao, l.Valor, l.QtdMaq))
            .Append(new FolhaCalLinhaVista(linhas.Count, null, null, null, null, Extra: true))
            .ToList();
}

/// <summary>
/// Uma família da folha no formulário (o partial <c>_JobOnFolhaFamilia</c>): a
/// sigla, a folha e, no TP, a calote desta produção (a coluna da
/// <c>JobOns</c>, que continua com o nome <c>calote_mm</c>).
/// </summary>
public sealed record JobOnFolhaFamiliaVista(string Familia, FolhaDoJobOnVista Folha, string? Calote);
