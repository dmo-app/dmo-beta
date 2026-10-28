using System.Globalization;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>Um valor das folhas impressas: onde aparece, de onde vem no Job On, e o valor deste Job On.</summary>
/// <param name="Chave">A chave que o _JobOnFolhasImprimir usa («cm.ref»).</param>
/// <param name="Bloco">O bloco da folha («Contra-Moldes», «Cabeçalho», «Calibres»).</param>
/// <param name="Rotulo">O rótulo na folha («Reparador»).</param>
/// <param name="Origem">De onde vem no Job On, em palavras (o campo da folha, a ferramenta, a linha CAL).</param>
/// <param name="Valor">O valor já no formato impresso; vazio quando o Job On não o tem (fica para escrever à mão).</param>
public sealed record ValorImpresso(string Chave, string Bloco, string Rotulo, string Origem, string Valor);

/// <summary>
/// O GERADOR DO IMPRIMIR (Diogo, 27/10/2026: «um generator para saber que
/// valores usar de cada Job On»): o único sítio que diz, para cada linha das 4
/// folhas (Job-On Moldes ×2, Trabalho de equipa e Ficha de artigo), de onde vem
/// o valor no Job On e em que formato sai. O _JobOnFolhasImprimir só desenha o
/// que está aqui, e o <see cref="Mapa"/> lista tudo, com o que está vazio.
/// <para>
/// Formatos pedidos pelo Diogo: números com ponto («70/69.40»), «mm» em
/// minúsculas; CS com Abertos/Fechados em vez de «4x4»/«2x4»
/// (<see cref="CsFurosTexto"/>); a referência do Tampão é «diâmetro x
/// calote»: «24.95 x 4.5mm» (27/10/2026).
/// </para>
/// </summary>
public sealed class JobOnImpressao
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    private readonly FolhaModel _m;
    private readonly Dictionary<string, ValorImpresso> _valores = new(StringComparer.Ordinal);

    public JobOnImpressao(FolhaModel model)
    {
        _m = model;
        var jobOn = model.JobOn ?? throw new ArgumentException("A impressão precisa do Job On.", nameof(model));
        var cm = model.Fact(jobOn.CmToolId);
        var mf = model.Fact(jobOn.MfToolId);
        var bq = model.Fact(jobOn.BqToolId);
        var furos = CsFurosTexto.Ler(Model("cs_furos"));

        // Cabeçalho (as 4 folhas)
        Por("ref", "Cabeçalho", "Referência", "MF + BQ do Job On (sem eles, a referência gravada)", model.ReferenciaTotal);
        Por("linha", "Cabeçalho", "Linha", "Job On · Linha", jobOn.Machine);
        Por("producao", "Cabeçalho", "Produção", "Job On · N.º de produção", jobOn.ProductionNumber);
        Por("entrada", "Cabeçalho", "Entrada", "Job On · Data de entrada", Data(jobOn.InicioEm));
        Por("entrada.dia", "Cabeçalho", "Dia da entrada", "Job On · Data de entrada (dia da semana)", DiaDaSemana(jobOn.InicioEm));
        Por("saida", "Cabeçalho", "Saída", "Job On · Data de saída", Data(jobOn.FimEm));
        Campo("seccoes", "Cabeçalho", "Secções", "cab_seccoes");
        Por("gota", "Cabeçalho", "Gota", "Nome técnico da CM (senão do MF): 55 = 2, 44/4T/50/5T = 3, 95 = 4; sem ele, Folha · Gota (cab_gota)", model.Gota ?? "");
        Por("peso", "Cabeçalho", "Peso", "Folha · Peso (g) (cab_peso)", Model("cab_peso") is { Length: > 0 } p ? p + " g" : "");
        Por("cadencia", "Cabeçalho", "Cadência", "SAP · Cadência da produção (a das ordens com mais dias); sem ela, escreve-se à mão",
            model.CadenciaSap?.ToString(CultureInfo.InvariantCulture) ?? "");
        Por("processo", "Rodapé", "Processo", "Folha · Processo (cab_processo); sem ele, o processo da CM",
            Model("cab_processo") is { Length: > 0 } pr ? pr : (cm?.Processo ?? ""));

        // CM
        Por("cm.ref", "Contra-Moldes", "Referência", "Ferramenta CM do Job On", cm?.Reference ?? "");
        Por("cm.lote", "Contra-Moldes", "Lote", "Ferramenta CM do Job On · lote", LoteDe(cm));
        Campo("cm.reparador", "Contra-Moldes", "Reparador", "cm_reparador");
        Por("cm.utilizacao", "Contra-Moldes", "Utilização", "Folha · CM · Utilização (%) (cm_utilizacao)", Percent("cm_utilizacao"));
        Campo("cm.adaptador", "Contra-Moldes", "Adaptador", "cm_adaptador");
        Campo("cm.inversao", "Contra-Moldes", "Inversão", "cm_inversao");
        Campo("cm.tipo", "Contra-Moldes", "Tipo", "cm_tipo");
        Medidas("cm", "Contra-Moldes", [("exterior", "Diâm. Exterior"), ("pata", "Diâm. Pata"), ("gargalo", "Diâm. Gargalo")]);
        Por("cm.folgas", "Contra-Moldes", "Folgas", "Folha · CM · Folgas 1, 2 e 3 (cm_folga1..3)", Folgas("cm"));
        Campo("cm.notas", "Contra-Moldes", "Notas", "cm_notas");

        // TP: a referência é «diâmetro x calote» (Diogo, 27/10/2026); calote e bacia são a mesma coisa (26/10).
        Campo("tp.diametro", "Tampões", "Diâmetro", "tp_diametro_ps");
        Por("tp.calote", "Tampões", "Calote", "Job On · Calote (mm); sem ela, a Bacia PS (tp_bacia_ps)", CaloteTexto());
        Por("tp.ref", "Tampões", "Referência", "Diâmetro x Calote («24.95 x 4.5mm»); sem diâmetro, a Referência do TP (tp_referencia)", TampaoRef());
        Campo("tp.notas", "Tampões", "Notas", "tp_notas");

        // MF
        Por("mf.ref", "Moldes Finais", "Referência", "Ferramenta MF do Job On", mf?.Reference ?? "");
        Por("mf.lote", "Moldes Finais", "Lote", "Ferramenta MF do Job On · lote", LoteDe(mf));
        Campo("mf.fundo", "Moldes Finais", "Fundo Final", "mf_fundo_final");
        Campo("mf.lote_ff", "Moldes Finais", "Lote do Fundo Final", "mf_lote_ff");
        Campo("mf.reparador", "Moldes Finais", "Reparador", "mf_reparador");
        Por("mf.utilizacao", "Moldes Finais", "Utilização", "Folha · MF · Utilização (%) (mf_utilizacao)", Percent("mf_utilizacao"));
        Campo("mf.adaptador", "Moldes Finais", "Adaptador", "mf_adaptador");
        Campo("mf.inversao", "Moldes Finais", "Inversão", "mf_inversao");
        Campo("mf.tipo", "Moldes Finais", "Tipo", "mf_tipo");
        Medidas("mf", "Moldes Finais", [("exterior", "Diâm. Exterior"), ("corpo", "Diâm. Corpo"), ("gargalo", "Diâm. Gargalo")]);
        Por("mf.folgas", "Moldes Finais", "Folgas", "Folha · MF · Folgas 1, 2 e 3 (mf_folga1..3)", Folgas("mf"));
        Campo("mf.notas", "Moldes Finais", "Notas", "mf_notas");

        // BQ, AN, PU, ARR
        Por("bq.ref", "Boquilhas", "Referência", "Ferramenta BQ do Job On", bq?.Reference ?? "");
        Por("bq.lote", "Boquilhas", "Lote", "Ferramenta BQ do Job On · lote", LoteDe(bq));
        Por("bq.utilizacao", "Boquilhas", "Utilização", "Folha · BQ · Utilização (%) (bq_utilizacao)", Percent("bq_utilizacao"));
        Campo("bq.notas", "Boquilhas", "Notas", "bq_notas");
        Campo("an.ref", "Anel", "Referência", "an_referencia");
        Por("an.lote", "Anel", "Lote", "Folha · AN · Lote (an_lote); sem ele, o lote da BQ",
            Model("an_lote") is { Length: > 0 } al ? al : N(model.AnLoteDaBq));
        Campo("an.notas", "Anel", "Notas", "an_notas");
        Campo("pu.ref", "Punções", "Referência", "pu_referencia");
        Campo("pu.versao", "Punções", "Versão", "pu_versao");
        Campo("pu.notas", "Punções", "Notas", "pu_notas");
        Campo("arr.ref", "Arrefecedores", "Referência", "arr_referencia");
        Campo("arr.notas", "Arrefecedores", "Notas", "arr_notas");

        // CS: Abertos e Fechados em vez de «4x4» / «2x4» (Diogo, 27/10/2026; o formato antigo lê-se pelo CsFurosTexto).
        Campo("cs.ref", "C. de Sopro", "Referência", "cs_referencia");
        Por("cs.abertos", "C. de Sopro", "Abertos", "Folha · CS · Furos (cs_furos): os abertos", furos.Lido ? furos.Abertos ?? "" : Model("cs_furos"));
        Por("cs.fechados", "C. de Sopro", "Fechados", "Folha · CS · Furos (cs_furos): os fechados", furos.Lido ? furos.Fechados ?? "" : "");
        Campo("cs.tubo", "C. de Sopro", "Tubo", "cs_tubo");
        Campo("cs.notas", "C. de Sopro", "Notas", "cs_notas");

        // PI
        Campo("pi.diametro", "Pinças", "Diâmetro", "pi_diametro");
        // As Pinças como estão no cartão PI do Job On, inteiras (Diogo, 27/10/2026: «imprimir jobon não usa a
        // informação das pinças que está no jobon»; antes só saía «Latão»/«Grafite»).
        Campo("pi.pincas", "Pinças", "Pinças", "pi_pincas");
        Campo("pi.notas", "Pinças", "Notas", "pi_notas");

        Campo("notas", "Notas gerais · Job-On", "Notas gerais", "notas_gerais");

        // Calibres: as linhas CAL, pela designação.
        Cal("cal.inv_mp", "Inversão MP", "Inversão MP");
        Cal("cal.tampao", "Tampão", "Tampão");
        Cal("cal.pinca", "Pinça", "Pinças", "Pinça");
        Cal("cal.inv_mf", "Inversão MF", "Inversão MF");
        Cal("cal.nivel", "Nível", "Nível");
        Cal("cal.cs", "Cab. enchimento", "Cab.Sopro", "Cab. Sopro");
    }

    /// <summary>Todos os valores das folhas, pela ordem acima: o mapa «linha da folha → campo do Job On».</summary>
    public IReadOnlyList<ValorImpresso> Mapa => _valores.Values.ToList();

    /// <summary>O valor impresso da chave (vazio quando o Job On não o tem).</summary>
    /// <exception cref="KeyNotFoundException">A chave não é do mapa.</exception>
    public string this[string chave] => _valores[chave].Valor;

    /// <summary>As linhas de uma nota (as Notas de cada bloco e as Notas gerais), sem as vazias.</summary>
    public IEnumerable<string> Linhas(string chave) =>
        this[chave].Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

    /// <summary>Dois valores com « / » («30 / 4T»), ou só o que existe.</summary>
    public string Par(string a, string b) => string.Join(" / ", new[] { this[a], this[b] }.Where(v => v.Length > 0));

    private void Por(string chave, string bloco, string rotulo, string origem, string valor) =>
        _valores.Add(chave, new ValorImpresso(chave, bloco, rotulo, origem, valor));

    private void Campo(string chave, string bloco, string rotulo, string campo) =>
        Por(chave, bloco, rotulo, $"Folha · {JobOnFolhaCampos.Encontrar(campo)?.RotuloAlteracao ?? campo} ({campo})", Model(campo));

    private void Medidas(string tipo, string bloco, (string Campo, string Rotulo)[] medidas)
    {
        foreach (var (campo, rotulo) in medidas)
        {
            Campo($"{tipo}.{campo}", bloco, rotulo, $"{tipo}_{campo}");
            Campo($"{tipo}.{campo}_tol", bloco, rotulo + " · Tol.", $"{tipo}_{campo}_tol");
        }
    }

    private void Cal(string chave, string rotulo, params string[] designacoes) =>
        Por(chave, "Calibres", rotulo, $"Linha CAL «{designacoes[0]}» · valor",
            _m.Folha.Cal.Where(l => !l.Extra && designacoes.Contains(l.Designacao?.Trim(), StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(l.Valor))
                .Select(l => N(l.Valor)).FirstOrDefault() ?? "");

    // Os números como na folha do Job On: ponto, duas casas quando têm casas, e «mm» em minúsculas.
    private static string N(string? v) =>
        Regex.Replace(DmoNumberFormat.FolhaTexto(v) ?? "", @"\bmm\b", "mm", RegexOptions.IgnoreCase);

    private string Model(string campo) => N(_m.Folha.Valor(campo));

    private string Percent(string campo) => Model(campo) is { Length: > 0 } x ? x.TrimEnd('%') + "%" : "";

    // Um número por espaço (JobOnFolgas): no MF, «0.12 / 0.16» no primeiro vai para o primeiro e o último.
    private string Folgas(string tipo) =>
        string.Join(" | ", (tipo == "mf"
            ? JobOnFolgas.UmaPorEspaco(_m.Folha.Valor("mf_folga1"), _m.Folha.Valor("mf_folga2"), _m.Folha.Valor("mf_folga3"))
            : [_m.Folha.Valor("cm_folga1"), _m.Folha.Valor("cm_folga2"), _m.Folha.Valor("cm_folga3")]).Select(N).Where(f => f.Length > 0));

    private static string LoteDe(JobOnToolFacts? f) => f is null ? "" : FolhaModel.Lote(f.Lot);

    private static string Data(DateOnly? d) => d is { } x ? x.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "";

    private static string DiaDaSemana(DateOnly? d) => d is { } x ? x.ToString("dddd", Pt) : "";

    // A calote numa casa decimal pelo menos («4.5», «6.25»): o «n.n» do Diogo.
    private static string Calote(decimal c) => Math.Round(c, 2).ToString("0.0#", CultureInfo.InvariantCulture);

    private static decimal? Numero(string? v) =>
        v?.Trim().Replace(',', '.') is { Length: > 0 } t && decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private decimal? CaloteMm() => _m.JobOn?.CaloteMm ?? Numero(_m.Folha.Valor("tp_bacia_ps"));

    private string CaloteTexto() =>
        CaloteMm() is { } c ? Calote(c) + " mm"
        : Model("tp_bacia_ps") is { Length: > 0 } b ? b + " mm" : "";

    // «24.95 x 4.5mm» (Diogo, 27/10/2026): o diâmetro com duas casas e a calote com uma.
    private string TampaoRef()
    {
        var diametro = Numero(_m.Folha.Valor("tp_diametro_ps"));
        if (diametro is null) return Model("tp_referencia");
        var d = Math.Round(diametro.Value, 2).ToString("0.00", CultureInfo.InvariantCulture);
        return CaloteMm() is { } c ? $"{d} x {Calote(c)}mm" : $"{d}mm";
    }
}
