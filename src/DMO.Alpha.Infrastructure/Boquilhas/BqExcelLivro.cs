using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DMO.Alpha.Core.Boquilhas;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// ESCREVE O EXCEL DAS BOQUILHAS DO CHEFE (Diogo, 21/10/2026), no formato do
/// ficheiro dele: um separador por mês (Janeiro..Dezembro), cada dia em 5
/// linhas a partir da linha 4 (a data na 1.ª, o dia da semana na 2.ª), e cada
/// máquina em 4 colunas (Ref, Sai, Ent, N.Fat) com o código na linha 1.
///
/// Mexe só nos valores das células dos dias pedidos: estilos, larguras,
/// cabeçalhos e os outros dias ficam como estavam. Trabalha no XML do .xlsx
/// (um zip), sem bibliotecas: o que não conhece fica intacto. Um mês sem
/// separador cria-se a partir do último separador com o mesmo formato.
/// </summary>
public static class BqExcelLivro
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Pr = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";

    private const string TipoFolha = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
    private const string TipoCalcChain = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/calcChain";
    private const string ConteudoFolha = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";

    /// <summary>A primeira linha de dias e as linhas de cada dia.</summary>
    public const int PrimeiraLinha = 4;
    public const int LinhasPorDia = 5;

    public static readonly IReadOnlyList<string> Meses =
        ["Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"];

    private static readonly string[] DiasDaSemana = ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];

    /// <summary>O formato das datas: dd/mm/aaaa em qualquer computador.</summary>
    public const string FormatoData = "dd/mm/yyyy";

    public static int LinhaDoDia(int dia) => PrimeiraLinha + LinhasPorDia * (dia - 1);

    /// <summary>
    /// UM EXCEL NOVO (Diogo, 21/10/2026: «escolher dir do excel que vamos
    /// criar»): o livro vazio no formato do ficheiro dele, com o separador do
    /// mês pedido. Os outros meses criam-se quando tiverem movimentos.
    ///
    /// Igual ao original (Diogo, 22/10/2026: «está sem as cores do original e
    /// não tem merges de células»): os estilos e o tema vêm do ficheiro dele
    /// (Molde/bq-estilos.xml e bq-tema.xml); cada máquina tem a sua cor, Sai a
    /// cinza claro e N.Fat a cinza; as linhas 1–2 de cada máquina juntam-se
    /// (B1:E2 …), «Data» junta A1:A3 e o dia da semana junta as 4 linhas de
    /// baixo da data (A5:A8 …); as larguras, as alturas e as bordas grossas a
    /// separar os dias e as máquinas são as do separador «Janeiro». Só leva os
    /// dias do mês, como o «Fevereiro» dele.
    /// </summary>
    public static byte[] Novo(int ano, int mes)
    {
        var maquinas = BqExcelPlano.Maquinas;
        var ultimaColuna = 1 + 4 * maquinas.Count;
        var dias = DateTime.DaysInMonth(ano, mes);
        var ultimaLinha = LinhaDoDia(dias) + LinhasPorDia - 1;
        var spans = $"1:{ultimaColuna}";
        var linhas = new StringBuilder();

        void Linha(int r, string atributos, Func<int, string?> conteudo, int[] estilos)
        {
            linhas.Append(CultureInfo.InvariantCulture, $"<row r=\"{r}\" spans=\"{spans}\"{atributos}>");
            for (var c = 1; c <= ultimaColuna; c++)
            {
                var estilo = Estilo(estilos, c).ToString(CultureInfo.InvariantCulture);
                linhas.Append(conteudo(c) is { } x ? x.Replace("{s}", estilo) : $"<c r=\"{NomeDaColuna(c)}{r}\" s=\"{estilo}\"/>");
            }

            linhas.Append("</row>");
        }

        string[] titulos = ["Ref", "Sai", "Ent", "N.Fat"];
        Linha(1, "", c => c == 1 ? Inline("A1", "Data") : (c - 2) % 4 == 0 ? Inline($"{NomeDaColuna(c)}1", maquinas[(c - 2) / 4]) : null, MoldeLinha1);
        Linha(2, " ht=\"15\" customHeight=\"1\"", _ => null, MoldeLinha2);
        Linha(3, " ht=\"19.5\" customHeight=\"1\" thickBot=\"1\"", c => c == 1 ? null : Inline($"{NomeDaColuna(c)}3", titulos[(c - 2) % 4]), MoldeLinha3);

        var serie = new DateTime(ano, mes, 1).ToOADate();
        for (var dia = 1; dia <= dias; dia++)
        {
            var primeira = LinhaDoDia(dia);
            var formula = dia == 1 ? $"DATE({ano},{mes},1)" : $"A{primeira - LinhasPorDia}+1";
            var data = string.Create(CultureInfo.InvariantCulture, $"<c r=\"A{primeira}\" s=\"{{s}}\"><f>{formula}</f><v>{serie + dia - 1}</v></c>");
            Linha(primeira, " ht=\"21\"", c => c == 1 ? data : null, MoldeDiaData);
            var semana = DiasDaSemana[(int)new DateTime(ano, mes, dia).DayOfWeek];
            Linha(primeira + 1, " ht=\"18.75\"", c => c == 1 ? Inline($"A{primeira + 1}", semana) : null, MoldeDiaMeio);
            Linha(primeira + 2, " ht=\"18.75\"", _ => null, MoldeDiaMeio);
            Linha(primeira + 3, " ht=\"18.75\"", _ => null, MoldeDiaMeio);
            Linha(primeira + 4, " ht=\"19.5\" thickBot=\"1\"", _ => null, MoldeDiaFim);
        }

        var juntas = new List<string> { "A1:A3" };
        juntas.AddRange(Enumerable.Range(0, maquinas.Count).Select(m => $"{NomeDaColuna(2 + 4 * m)}1:{NomeDaColuna(5 + 4 * m)}2"));
        juntas.AddRange(Enumerable.Range(1, dias).Select(d => $"A{LinhaDoDia(d) + 1}:A{LinhaDoDia(d) + LinhasPorDia - 1}"));

        // As larguras do «Janeiro»: A 20,29, B 15,71, C 12,43 e a Ref das outras máquinas ~12,3.
        var colunas = new StringBuilder("<cols><col min=\"1\" max=\"1\" width=\"20.28515625\" customWidth=\"1\"/>");
        for (var m = 0; m < maquinas.Count; m++)
        {
            var largura = LargurasDaRef[Math.Min(m, LargurasDaRef.Length - 1)];
            colunas.Append(CultureInfo.InvariantCulture, $"<col min=\"{2 + 4 * m}\" max=\"{2 + 4 * m}\" width=\"{largura}\" customWidth=\"1\"/>");
            if (m == 0) colunas.Append("<col min=\"3\" max=\"3\" width=\"12.42578125\" customWidth=\"1\"/>");
        }

        colunas.Append("</cols>");

        var folha = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + $"<dimension ref=\"A1:{NomeDaColuna(ultimaColuna)}{ultimaLinha}\"/>"
            + "<sheetViews><sheetView tabSelected=\"1\" zoomScale=\"55\" zoomScaleNormal=\"55\" workbookViewId=\"0\"><selection activeCell=\"B4\" sqref=\"B4\"/></sheetView></sheetViews>"
            + "<sheetFormatPr defaultRowHeight=\"15\"/>"
            + colunas + "<sheetData>" + linhas + "</sheetData>"
            + $"<mergeCells count=\"{juntas.Count}\">" + string.Concat(juntas.Select(j => $"<mergeCell ref=\"{j}\"/>")) + "</mergeCells>"
            + "<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>";

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Parte(string caminho, string xml)
            {
                using var escrita = new StreamWriter(zip.CreateEntry(caminho).Open(), new UTF8Encoding(false));
                escrita.Write(xml);
            }

            Parte("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"" + ConteudoFolha + "\"/><Override PartName=\"/xl/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
            Parte("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Parte("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><workbookPr defaultThemeVersion=\"124226\"/><sheets><sheet name=\"{Meses[mes - 1]}\" sheetId=\"1\" r:id=\"rId1\"/></sheets><calcPr calcId=\"191029\" fullCalcOnLoad=\"1\"/></workbook>");
            Parte("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"" + TipoFolha + "\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"theme/theme1.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Parte("xl/worksheets/sheet1.xml", folha);
            Parte("xl/theme/theme1.xml", Recurso("DMO.Bq.Excel.Tema.xml"));
            Parte("xl/styles.xml", Recurso("DMO.Bq.Excel.Estilos.xml"));
        }

        // Com os 12 meses, as datas em dd/mm/aaaa e as juntas sem linhas por dentro.
        return Escrever(memoria.ToArray(), []).Livro;

        static string Inline(string celula, string texto) =>
            $"<c r=\"{celula}\" s=\"{{s}}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(texto)}</t></is></c>";
    }

    // Os estilos (índices de Molde/bq-estilos.xml) de cada coluna, A..Y, como
    // no «Janeiro» do Diogo: A, e 4 colunas (Ref, Sai, Ent, N.Fat) por máquina
    // B1, B2, B3, C1, C2, C3, cada uma com a sua cor.
    internal static readonly int[] MoldeLinha1 = [1, 2, 3, 3, 4, 5, 6, 6, 7, 8, 9, 9, 10, 11, 12, 12, 13, 14, 15, 15, 16, 17, 18, 18, 19];
    internal static readonly int[] MoldeLinha2 = [1, 20, 21, 21, 22, 23, 24, 24, 25, 26, 27, 27, 28, 29, 30, 30, 31, 32, 33, 33, 34, 35, 36, 36, 37];
    internal static readonly int[] MoldeLinha3 = [38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 44, 45, 48, 49, 44, 45, 48, 50, 44, 45, 48, 51, 44, 45, 48];
    internal static readonly int[] MoldeDiaData = [52, 53, 54, 55, 56, 57, 54, 55, 56, 58, 54, 55, 56, 59, 54, 55, 56, 60, 54, 55, 56, 61, 54, 55, 56];
    internal static readonly int[] MoldeDiaMeio = [62, 63, 64, 65, 66, 67, 64, 65, 66, 68, 64, 65, 66, 69, 64, 65, 66, 70, 64, 65, 66, 71, 64, 65, 66];
    internal static readonly int[] MoldeDiaFim = [62, 72, 73, 74, 75, 76, 73, 74, 75, 77, 73, 74, 75, 78, 73, 74, 75, 79, 73, 74, 75, 80, 73, 74, 75];
    private static readonly string[] LargurasDaRef = ["15.7109375", "12.28515625", "12.85546875", "12.28515625", "12.28515625", "12.28515625"];

    /// <summary>O estilo da coluna c (1 = A); com mais máquinas, as cores repetem-se.</summary>
    private static int Estilo(int[] molde, int c) => c == 1 ? molde[0] : molde[1 + (c - 2) % (molde.Length - 1)];

    private static string Recurso(string nome)
    {
        using var fluxo = typeof(BqExcelLivro).Assembly.GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"Falta o recurso {nome} do Excel das Boquilhas.");
        using var leitor = new StreamReader(fluxo, Encoding.UTF8);
        return leitor.ReadToEnd();
    }

    public static BqExcelResultado Escrever(byte[] livro, IReadOnlyList<BqExcelDia> dias, IReadOnlyList<BqExcelEscrita>? anteriores = null)
    {
        var avisos = new List<string>();
        var escritas = new List<BqExcelEscrita>();
        // As escritas de antes fora do intervalo pedido continuam a ser da app.
        var pedidos = dias.Select(d => d.Dia).ToHashSet();
        escritas.AddRange((anteriores ?? []).Where(e => !pedidos.Contains(e.Dia)));
        using var memoria = new MemoryStream();
        memoria.Write(livro);
        try
        {
            using (var zip = new ZipArchive(memoria, ZipArchiveMode.Update, leaveOpen: true))
            {
                var pacote = new Pacote(zip);
                var ano = pacote.Ano();
                // Cada mês no seu separador (Diogo, 26/10/2026: «cada mês deveria
                // ser uma página diferente»): os que faltam criam-se já, por ordem.
                for (var m = 1; m <= 12; m++)
                {
                    var doMes = pacote.Folha(m) ?? pacote.CriarFolha(ano, m);
                    DatasComValor(doMes.Xml, ano, m);
                    doMes.Mudou = true;
                }

                foreach (var mes in dias.GroupBy(dia => (dia.Dia.Year, dia.Dia.Month)))
                {
                    if (mes.Key.Year != ano)
                    {
                        if (mes.Any(TemLinhas))
                        {
                            avisos.Add($"O Excel é de {ano}: os movimentos de {Meses[mes.Key.Month - 1].ToLowerInvariant()} de {mes.Key.Year} não foram escritos.");
                        }
                        continue;
                    }

                    var folha = pacote.Folha(mes.Key.Month);
                    if (folha is null)
                    {
                        if (!mes.Any(TemLinhas)) continue;
                        folha = pacote.CriarFolha(mes.Key.Year, mes.Key.Month);
                    }

                    var colunas = Colunas(folha.Xml, pacote.Textos);
                    var diasNoMes = DateTime.DaysInMonth(mes.Key.Year, mes.Key.Month);
                    if (colunas.Count < BqExcelPlano.Maquinas.Count || !TemDia(folha.Xml, 1) || !TemDia(folha.Xml, diasNoMes))
                    {
                        avisos.Add($"O separador «{folha.Nome}» não tem o formato do Excel das Boquilhas (máquinas na linha 1, um dia em cada {LinhasPorDia} linhas a partir da linha {PrimeiraLinha}): não foi escrito.");
                        continue;
                    }

                    foreach (var dia in mes)
                    {
                        EscreverDia(folha.Xml, colunas, pacote.Textos, dia, anteriores ?? [], escritas, avisos);
                    }

                    folha.Mudou = true;
                }

                pacote.Gravar();
            }
        }
        catch (Exception erro) when (erro is InvalidDataException or System.Xml.XmlException)
        {
            throw new BqExcelInvalidoException("O ficheiro escolhido não é um Excel (.xlsx) que a app consiga ler.");
        }

        return new BqExcelResultado(memoria.ToArray(), avisos, Escritas: escritas);
    }

    /// <summary>
    /// «ATUALIZAR» (Diogo, 22/10/2026: «dia anterior está igual ao que a app
    /// tem? Se sim não atualizamos, acabou. Se não, atualiza e pergunta se o
    /// dia antes do anterior está igual … e assim sucessivamente»).
    ///
    /// Os dias depois de «ontem» (hoje) escrevem-se sempre. Depois, de ontem
    /// para trás, um dia de cada vez: se as linhas da app no Excel já são as
    /// da app, pára; senão o dia vai ser escrito e passa ao dia antes. Pára
    /// também no primeiro dia do plano (o primeiro com dados na app), num dia
    /// de outro ano que não o do livro, ou num separador sem o formato.
    /// Compara no livro tal como chegou; no fim escreve tudo de uma vez, só
    /// nas linhas da app (o que foi escrito à mão fica como está).
    /// </summary>
    public static BqExcelAtualizacao Atualizar(byte[] livro, IReadOnlyList<BqExcelDia> dias, DateOnly ontem, IReadOnlyList<BqExcelEscrita>? anteriores = null)
    {
        anteriores ??= [];
        var avisos = new List<string>();
        var aEscrever = dias.Where(d => d.Dia > ontem).ToList();
        var atualizados = new List<BqExcelDia>();
        try
        {
            using var memoria = new MemoryStream(livro, writable: false);
            using var zip = new ZipArchive(memoria, ZipArchiveMode.Read);
            var pacote = new Pacote(zip);
            var ano = pacote.Ano();
            foreach (var dia in dias.Where(d => d.Dia <= ontem).OrderByDescending(d => d.Dia))
            {
                if (dia.Dia.Year != ano) break;
                var folha = pacote.Folha(dia.Dia.Month);
                if (folha is null)
                {
                    // Sem separador do mês: um dia vazio na app não tem nada a escrever.
                    if (TemLinhas(dia)) atualizados.Add(dia);
                    continue;
                }

                var colunas = Colunas(folha.Xml, pacote.Textos);
                var diasNoMes = DateTime.DaysInMonth(dia.Dia.Year, dia.Dia.Month);
                if (colunas.Count < BqExcelPlano.Maquinas.Count || !TemDia(folha.Xml, 1) || !TemDia(folha.Xml, diasNoMes))
                {
                    avisos.Add($"O separador «{folha.Nome}» não tem o formato do Excel das Boquilhas (máquinas na linha 1, um dia em cada {LinhasPorDia} linhas a partir da linha {PrimeiraLinha}): não foi atualizado.");
                    break;
                }

                // Só pára num dia com movimentos já igual (Diogo, 26/10/2026: «ao
                // carregar atualizar não mete os movimentos»): um dia vazio na app
                // e no Excel (um domingo) não diz nada sobre os dias de antes.
                if (DiaIgual(folha.Xml, colunas, pacote.Textos, dia, anteriores))
                {
                    if (TemLinhas(dia)) break;
                    continue;
                }

                atualizados.Add(dia);
            }
        }
        catch (Exception erro) when (erro is InvalidDataException or System.Xml.XmlException)
        {
            throw new BqExcelInvalidoException("O ficheiro escolhido não é um Excel (.xlsx) que a app consiga ler.");
        }

        atualizados.Reverse();
        var resultado = Escrever(livro, atualizados.Concat(aEscrever).ToList(), anteriores);
        return new BqExcelAtualizacao(resultado with { Avisos = avisos.Concat(resultado.Avisos).ToList() }, atualizados.Select(d => d.Dia).ToList());
    }

    /// <summary>
    /// O dia no Excel é igual ao da app quando cada linha da app (Ref, Sai,
    /// Ent, N.Fat) está numa linha do dia dessa máquina e nenhuma linha que a
    /// app escreveu antes (e ainda lá está) ficou a mais. As linhas à mão não
    /// contam.
    /// </summary>
    private static bool DiaIgual(XElement folha, IReadOnlyDictionary<string, int> colunas, IReadOnlyList<string> textos, BqExcelDia dia, IReadOnlyList<BqExcelEscrita> anteriores)
    {
        var linha = LinhaDoDia(dia.Dia.Day);
        foreach (var maquina in BqExcelPlano.Maquinas)
        {
            var primeira = colunas[maquina];
            string?[] Ler(int r) => Enumerable.Range(primeira, 4)
                .Select(c => Celula(folha, r, c, criar: false) is { } x ? (x.Element(S + "f") is { } f ? "=" + f.Value.Trim() : Valor(x, textos)?.Trim()) : null)
                .Select(v => string.IsNullOrEmpty(v) ? null : v)
                .ToArray();

            var noExcel = Enumerable.Range(linha, LinhasPorDia).ToDictionary(r => r, Ler);
            var usadas = new HashSet<int>();
            foreach (var item in dia.Maquinas.GetValueOrDefault(maquina) ?? [])
            {
                string?[] esperado = [item.Ref, Formatar(item.Saidas), Formatar(item.Entradas), Formatar(item.SemReparacao)];
                var achada = noExcel.Keys.FirstOrDefault(r => !usadas.Contains(r) && noExcel[r].SequenceEqual(esperado));
                if (achada == 0) return false;
                usadas.Add(achada);
            }

            var aMais = anteriores.Any(e => e.Dia == dia.Dia && e.Maquina == maquina
                && e.Linha >= linha && e.Linha < linha + LinhasPorDia
                && !usadas.Contains(e.Linha) && noExcel[e.Linha][0] == e.Ref);
            if (aMais) return false;
        }

        return true;

        static string? Formatar(IReadOnlyList<int> quantidades) => quantidades.Count switch
        {
            0 => null,
            1 => quantidades[0].ToString(CultureInfo.InvariantCulture),
            _ => "=" + string.Join("+", quantidades.Select(q => q.ToString(CultureInfo.InvariantCulture)))
        };
    }

    private static bool TemLinhas(BqExcelDia dia) => dia.Maquinas.Values.Any(linhas => linhas.Count > 0);

    /// <summary>
    /// Um dia de uma máquina: a app só mexe nas linhas que ela própria
    /// escreveu (as escritas de antes, se a Ref ainda lá está) e nas linhas
    /// livres do dia; o que foi escrito à mão fica como está.
    /// </summary>
    private static void EscreverDia(XElement folha, IReadOnlyDictionary<string, int> colunas, IReadOnlyList<string> textos, BqExcelDia dia,
        IReadOnlyList<BqExcelEscrita> anteriores, List<BqExcelEscrita> escritas, List<string> avisos)
    {
        var linha = LinhaDoDia(dia.Dia.Day);
        foreach (var maquina in BqExcelPlano.Maquinas)
        {
            var primeira = colunas[maquina];
            foreach (var antes in anteriores.Where(e => e.Dia == dia.Dia && e.Maquina == maquina
                && e.Linha >= linha && e.Linha < linha + LinhasPorDia))
            {
                var refCelula = Celula(folha, antes.Linha, primeira, criar: false);
                if (refCelula is null || Valor(refCelula, textos)?.Trim() != antes.Ref) continue;
                for (var c = primeira; c < primeira + 4; c++) Limpar(Celula(folha, antes.Linha, c, criar: false));
            }

            var livres = Enumerable.Range(linha, LinhasPorDia)
                .Where(r => Enumerable.Range(primeira, 4).All(c => Celula(folha, r, c, criar: false) is not { } x
                    || (x.Element(S + "v") is null && x.Element(S + "f") is null && x.Element(S + "is") is null)))
                .ToList();
            var linhas = dia.Maquinas.GetValueOrDefault(maquina) ?? [];
            if (linhas.Count > livres.Count)
            {
                avisos.Add($"{dia.Dia.ToString("dd'/'MM", CultureInfo.InvariantCulture)} · {maquina}: {linhas.Count} boquilhas no dia, só há {livres.Count} linhas livres; ficaram de fora {string.Join(", ", linhas.Skip(livres.Count).Select(l => l.Ref))}.");
            }

            for (var i = 0; i < Math.Min(linhas.Count, livres.Count); i++)
            {
                var item = linhas[i];
                var r = livres[i];
                Texto(Celula(folha, r, primeira, criar: true)!, item.Ref);
                Quantidades(Celula(folha, r, primeira + 1, criar: true)!, item.Saidas);
                Quantidades(Celula(folha, r, primeira + 2, criar: true)!, item.Entradas);
                Quantidades(Celula(folha, r, primeira + 3, criar: true)!, item.SemReparacao);
                escritas.Add(new BqExcelEscrita(dia.Dia, maquina, r, item.Ref));
            }
        }
    }

    /// <summary>A coluna «Ref» de cada máquina, lida dos códigos da linha 1.</summary>
    private static Dictionary<string, int> Colunas(XElement folha, IReadOnlyList<string> textos)
    {
        var colunas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var linha1 = Linhas(folha).FirstOrDefault(row => NumeroDaLinha(row) == 1);
        if (linha1 is null) return colunas;
        foreach (var c in linha1.Elements(S + "c"))
        {
            var valor = Valor(c, textos)?.Trim();
            if (valor is not null && BqExcelPlano.Maquinas.Contains(valor, StringComparer.OrdinalIgnoreCase) && !colunas.ContainsKey(valor))
            {
                colunas[valor.ToUpperInvariant()] = Referencia(c).Coluna;
            }
        }

        return colunas;
    }

    /// <summary>O dia N tem a data (uma fórmula ou um número) na coluna A da sua linha.</summary>
    private static bool TemDia(XElement folha, int dia)
    {
        var celula = Celula(folha, LinhaDoDia(dia), 1, criar: false);
        return celula is not null && (celula.Element(S + "f") is not null || celula.Element(S + "v") is not null);
    }

    /// <summary>
    /// Os dias que faltam a um separador (até ao dia «dias»): cópias das
    /// linhas do último dia, com os mesmos estilos, alturas e a junção do dia
    /// da semana. Os valores limpam-se depois, com os do resto do separador.
    /// </summary>
    private static void AcrescentarDias(XElement folha, int dias)
    {
        var ultimo = Enumerable.Range(1, dias).LastOrDefault(d => TemDia(folha, d));
        if (ultimo == 0 || ultimo >= dias) return;
        var dados = folha.Element(S + "sheetData")!;
        var origem = LinhaDoDia(ultimo);
        var modelo = Linhas(folha).Where(r => NumeroDaLinha(r) >= origem && NumeroDaLinha(r) < origem + LinhasPorDia).ToList();
        var juntas = folha.Element(S + "mergeCells");
        var juntaDoDia = juntas?.Elements(S + "mergeCell").Any(m => (string?)m.Attribute("ref") == $"A{origem + 1}:A{origem + LinhasPorDia - 1}") == true;
        foreach (var r in Linhas(folha).Where(r => NumeroDaLinha(r) >= origem + LinhasPorDia).ToList()) r.Remove();
        for (var dia = ultimo + 1; dia <= dias; dia++)
        {
            var deslocamento = LinhaDoDia(dia) - origem;
            foreach (var row in modelo)
            {
                var nova = new XElement(row);
                var numero = NumeroDaLinha(row) + deslocamento;
                nova.SetAttributeValue("r", numero);
                foreach (var c in nova.Elements(S + "c"))
                {
                    c.SetAttributeValue("r", NomeDaColuna(Referencia(c).Coluna) + numero.ToString(CultureInfo.InvariantCulture));
                }

                dados.Add(nova);
            }

            if (juntaDoDia)
            {
                juntas!.Add(new XElement(S + "mergeCell", new XAttribute("ref", $"A{LinhaDoDia(dia) + 1}:A{LinhaDoDia(dia) + LinhasPorDia - 1}")));
                juntas.SetAttributeValue("count", juntas.Elements(S + "mergeCell").Count());
            }
        }
    }

    private static string? Valor(XElement c, IReadOnlyList<string> textos)
    {
        var tipo = (string?)c.Attribute("t");
        if (tipo == "inlineStr") return string.Concat(c.Descendants(S + "t").Select(t => t.Value));
        var v = c.Element(S + "v")?.Value;
        if (v is null) return null;
        return tipo == "s" && int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < textos.Count ? textos[i] : v;
    }

    private static void Limpar(XElement? c)
    {
        if (c is null) return;
        c.Attribute("t")?.Remove();
        c.Elements().Where(e => e.Name == S + "f" || e.Name == S + "v" || e.Name == S + "is").Remove();
    }

    private static void Texto(XElement c, string texto)
    {
        Limpar(c);
        c.SetAttributeValue("t", "inlineStr");
        c.Add(new XElement(S + "is", new XElement(S + "t", texto)));
    }

    private static void Quantidades(XElement c, IReadOnlyList<int> quantidades)
    {
        Limpar(c);
        if (quantidades.Count == 1)
        {
            c.Add(new XElement(S + "v", quantidades[0].ToString(CultureInfo.InvariantCulture)));
        }
        else if (quantidades.Count > 1)
        {
            c.Add(new XElement(S + "f", string.Join("+", quantidades.Select(q => q.ToString(CultureInfo.InvariantCulture)))));
        }
    }

    private static void Formula(XElement c, string formula, double? valor = null)
    {
        Limpar(c);
        c.Add(new XElement(S + "f", formula));
        if (valor is { } v) c.Add(new XElement(S + "v", v.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// A data de cada dia fica também escrita na célula (Diogo, 26/10/2026:
    /// «não tem dia dd/mm/aaaa»): só com a fórmula, quem abre o ficheiro sem
    /// recalcular (pré-visualização, telemóvel) via a célula vazia.
    /// </summary>
    private static void DatasComValor(XElement folha, int ano, int mes)
    {
        var serie = new DateTime(ano, mes, 1).ToOADate();
        for (var dia = 1; dia <= DateTime.DaysInMonth(ano, mes); dia++)
        {
            var c = Celula(folha, LinhaDoDia(dia), 1, criar: false);
            if (c?.Element(S + "f") is null) continue;
            c.Element(S + "v")?.Remove();
            c.Add(new XElement(S + "v", (serie + dia - 1).ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static IEnumerable<XElement> Linhas(XElement folha) =>
        folha.Element(S + "sheetData")?.Elements(S + "row") ?? [];

    private static int NumeroDaLinha(XElement row) =>
        int.Parse((string)row.Attribute("r")!, CultureInfo.InvariantCulture);

    /// <summary>A célula (linha, coluna); com criar, cria a linha e a célula no sítio certo.</summary>
    private static XElement? Celula(XElement folha, int linha, int coluna, bool criar)
    {
        var dados = folha.Element(S + "sheetData");
        if (dados is null) return null;
        var row = dados.Elements(S + "row").FirstOrDefault(r => NumeroDaLinha(r) == linha);
        if (row is null)
        {
            if (!criar) return null;
            row = new XElement(S + "row", new XAttribute("r", linha));
            var depois = dados.Elements(S + "row").FirstOrDefault(r => NumeroDaLinha(r) > linha);
            if (depois is null) dados.Add(row); else depois.AddBeforeSelf(row);
        }

        var nome = NomeDaColuna(coluna) + linha.ToString(CultureInfo.InvariantCulture);
        var c = row.Elements(S + "c").FirstOrDefault(x => (string?)x.Attribute("r") == nome);
        if (c is not null || !criar) return c;
        c = new XElement(S + "c", new XAttribute("r", nome));
        // Uma célula nova fica com o formato da linha, se a linha tiver um.
        if ((string?)row.Attribute("customFormat") is "1" or "true" && row.Attribute("s") is { } estiloDaLinha)
        {
            c.SetAttributeValue("s", estiloDaLinha.Value);
        }

        var seguinte = row.Elements(S + "c").FirstOrDefault(x => Referencia(x).Coluna > coluna);
        if (seguinte is null) row.Add(c); else seguinte.AddBeforeSelf(c);
        return c;
    }

    private static (int Coluna, int Linha) Referencia(XElement c)
    {
        var r = (string)c.Attribute("r")!;
        var letras = 0;
        var coluna = 0;
        while (letras < r.Length && char.IsLetter(r[letras]))
        {
            coluna = coluna * 26 + (char.ToUpperInvariant(r[letras]) - 'A' + 1);
            letras++;
        }

        return (coluna, int.Parse(r[letras..], CultureInfo.InvariantCulture));
    }

    public static string NomeDaColuna(int coluna)
    {
        var nome = new StringBuilder();
        while (coluna > 0)
        {
            var resto = (coluna - 1) % 26;
            nome.Insert(0, (char)('A' + resto));
            coluna = (coluna - 1) / 26;
        }

        return nome.ToString();
    }

    private static string SemAcentos(string texto)
    {
        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        return new string(decomposto.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray())
            .ToLowerInvariant();
    }

    private sealed class Folha(string nome, string caminho, XDocument documento)
    {
        public string Nome { get; } = nome;
        public string Caminho { get; } = caminho;
        public XDocument Documento { get; } = documento;
        public XElement Xml => Documento.Root!;
        public bool Mudou { get; set; }
    }

    /// <summary>As partes do .xlsx que a escrita lê e muda.</summary>
    private sealed class Pacote
    {
        private readonly ZipArchive _zip;
        private readonly XDocument _livro;
        private readonly XDocument _relacoes;
        private readonly XDocument _tipos;
        private readonly List<(string Nome, string Caminho)> _folhas;
        private readonly Dictionary<string, Folha> _abertas = new(StringComparer.Ordinal);
        private readonly List<Folha> _novas = [];

        public IReadOnlyList<string> Textos { get; }

        public Pacote(ZipArchive zip)
        {
            _zip = zip;
            _livro = Ler("xl/workbook.xml") ?? throw new BqExcelInvalidoException("O ficheiro escolhido não é um livro do Excel (.xlsx).");
            _relacoes = Ler("xl/_rels/workbook.xml.rels") ?? throw new BqExcelInvalidoException("O ficheiro escolhido não é um livro do Excel (.xlsx).");
            _tipos = Ler("[Content_Types].xml") ?? throw new BqExcelInvalidoException("O ficheiro escolhido não é um livro do Excel (.xlsx).");
            var alvos = _relacoes.Root!.Elements(Pr + "Relationship")
                .ToDictionary(r => (string)r.Attribute("Id")!, r => Caminho((string)r.Attribute("Target")!));
            _folhas = _livro.Root!.Element(S + "sheets")!.Elements(S + "sheet")
                .Select(s => ((string)s.Attribute("name")!, alvos.GetValueOrDefault((string?)s.Attribute(R + "id") ?? "") ?? ""))
                .Where(s => s.Item2.Length > 0)
                .ToList();
            Textos = Ler("xl/sharedStrings.xml")?.Root!.Elements(S + "si")
                .Select(si => string.Concat(si.Descendants(S + "t").Select(t => t.Value)))
                .ToList() ?? [];
        }

        private static string Caminho(string alvo) => alvo.StartsWith('/') ? alvo[1..] : "xl/" + alvo;

        private XDocument? Ler(string caminho)
        {
            var entrada = _zip.GetEntry(caminho);
            if (entrada is null) return null;
            using var fluxo = entrada.Open();
            return XDocument.Load(fluxo);
        }

        private void Escrever(string caminho, XDocument documento)
        {
            _zip.GetEntry(caminho)?.Delete();
            var entrada = _zip.CreateEntry(caminho, CompressionLevel.Optimal);
            using var fluxo = entrada.Open();
            documento.Save(fluxo, SaveOptions.DisableFormatting);
        }

        /// <summary>O ano do livro, da data do 1.º dia de um separador (=DATE(2026,7,1)); sem ele, o ano corrente.</summary>
        public int Ano()
        {
            foreach (var (_, caminho) in _folhas)
            {
                var folha = Ler(caminho)?.Root;
                var formula = folha is null ? null : Celula(folha, PrimeiraLinha, 1, criar: false)?.Element(S + "f")?.Value;
                var ano = formula is null ? null : Regex.Match(formula, @"DATE\(\s*(\d{4})", RegexOptions.IgnoreCase);
                if (ano is { Success: true }) return int.Parse(ano.Groups[1].Value, CultureInfo.InvariantCulture);
            }

            return DateTime.Today.Year;
        }

        public Folha? Folha(int mes)
        {
            var nome = SemAcentos(Meses[mes - 1]);
            var achada = _folhas.FirstOrDefault(f => SemAcentos(f.Nome) == nome);
            return achada.Nome is null ? null : Abrir(achada.Nome, achada.Caminho);
        }

        private Folha Abrir(string nome, string caminho)
        {
            if (_abertas.TryGetValue(caminho, out var aberta)) return aberta;
            var documento = Ler(caminho) ?? throw new BqExcelInvalidoException($"O separador «{nome}» não se consegue ler.");
            return _abertas[caminho] = new Folha(nome, caminho, documento);
        }

        /// <summary>
        /// O separador de um mês que ainda não existe: a cópia do último
        /// separador de um mês com dias suficientes, com os valores limpos, a
        /// data do 1.º dia, as datas seguintes (=A4+1…) e os dias da semana.
        /// </summary>
        public Folha CriarFolha(int ano, int mes)
        {
            var dias = DateTime.DaysInMonth(ano, mes);
            var meses = _folhas
                .Where(f => Meses.Any(m => SemAcentos(m) == SemAcentos(f.Nome)))
                .Select(f => Abrir(f.Nome, f.Caminho))
                .Where(f => TemDia(f.Xml, 28))
                .ToList();
            // Sem um mês com dias que cheguem (um Excel novo de fevereiro e
            // movimentos em março), o mais comprido serve e ganha os dias que
            // faltam, com o formato do seu último dia.
            var molde = meses.LastOrDefault(f => TemDia(f.Xml, dias))
                ?? meses.OrderByDescending(f => Enumerable.Range(28, 4).Count(d => TemDia(f.Xml, d))).FirstOrDefault()
                ?? throw new BqExcelInvalidoException($"Não há nenhum separador de mês com {dias} dias para servir de modelo a «{Meses[mes - 1]}».");

            var copia = new XDocument(molde.Documento);
            var xml = copia.Root!;
            AcrescentarDias(xml, dias);
            // A cópia não leva as partes ligadas ao separador (impressora, desenhos).
            xml.Descendants().Attributes().Where(a => a.Name.Namespace == R).Remove();
            xml.Elements().Where(e => e.Name.LocalName is "drawing" or "legacyDrawing" or "legacyDrawingHF" or "picture" or "tableParts" or "oleObjects" or "controls" or "hyperlinks").Remove();
            foreach (var vista in xml.Descendants(S + "sheetView"))
            {
                vista.Attribute("tabSelected")?.Remove();
                vista.Attribute("topLeftCell")?.Remove();
            }

            var ultima = LinhaDoDia(dias) + LinhasPorDia - 1;
            foreach (var row in Linhas(xml).Where(r => NumeroDaLinha(r) > ultima).ToList()) row.Remove();
            var juntas = xml.Element(S + "mergeCells");
            if (juntas is not null)
            {
                juntas.Elements(S + "mergeCell")
                    .Where(m => Regex.Match((string)m.Attribute("ref")!, @"\d+").Value is var linha && int.Parse(linha, CultureInfo.InvariantCulture) > ultima)
                    .Remove();
                juntas.SetAttributeValue("count", juntas.Elements(S + "mergeCell").Count());
            }

            var dimensao = xml.Element(S + "dimension");
            dimensao?.SetAttributeValue("ref", Regex.Replace((string?)dimensao.Attribute("ref") ?? "A1", @"\d+$", ultima.ToString(CultureInfo.InvariantCulture)));

            foreach (var row in Linhas(xml).Where(r => NumeroDaLinha(r) >= PrimeiraLinha))
            {
                foreach (var c in row.Elements(S + "c")) Limpar(c);
            }

            for (var dia = 1; dia <= dias; dia++)
            {
                var linha = LinhaDoDia(dia);
                Formula(Celula(xml, linha, 1, criar: true)!, dia == 1
                    ? $"DATE({ano},{mes},1)"
                    : $"A{linha - LinhasPorDia}+1", new DateTime(ano, mes, dia).ToOADate());
                Texto(Celula(xml, linha + 1, 1, criar: true)!, DiasDaSemana[(int)new DateTime(ano, mes, dia).DayOfWeek]);
            }

            // As folhas criadas nesta escrita só entram no zip ao gravar.
            var numero = _zip.Entries.Select(e => e.FullName).Concat(_novas.Select(f => f.Caminho))
                .Select(nome => Regex.Match(nome, @"^xl/worksheets/sheet(\d+)\.xml$"))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0).Max() + 1;
            var caminho = $"xl/worksheets/sheet{numero}.xml";
            var nome = Meses[mes - 1];

            var relacoes = _relacoes.Root!;
            var id = "rIdDmo" + numero.ToString(CultureInfo.InvariantCulture);
            relacoes.Add(new XElement(Pr + "Relationship",
                new XAttribute("Id", id), new XAttribute("Type", TipoFolha), new XAttribute("Target", $"worksheets/sheet{numero}.xml")));
            var folhas = _livro.Root!.Element(S + "sheets")!;
            var sheetId = folhas.Elements(S + "sheet").Select(s => (int)s.Attribute("sheetId")!).DefaultIfEmpty(0).Max() + 1;
            var separador = new XElement(S + "sheet", new XAttribute("name", nome), new XAttribute("sheetId", sheetId), new XAttribute(R + "id", id));
            // Por ordem dos meses: antes do primeiro separador de um mês depois deste.
            var ordem = Meses.Select(SemAcentos).ToList();
            var depois = folhas.Elements(S + "sheet").FirstOrDefault(s => ordem.IndexOf(SemAcentos((string)s.Attribute("name")!)) >= mes);
            if (depois is null)
            {
                folhas.Add(separador);
            }
            else
            {
                depois.AddBeforeSelf(separador);
                // O separador ativo (activeTab) é uma posição: passa uma para a frente.
                var vista = _livro.Root!.Element(S + "bookViews")?.Element(S + "workbookView");
                var ativo = (int?)vista?.Attribute("activeTab") ?? 0;
                var posicao = folhas.Elements(S + "sheet").ToList().IndexOf(separador);
                if (vista is not null && ativo >= posicao) vista.SetAttributeValue("activeTab", ativo + 1);
            }
            _tipos.Root!.Add(new XElement(Ct + "Override",
                new XAttribute("PartName", "/" + caminho), new XAttribute("ContentType", ConteudoFolha)));

            _folhas.Add((nome, caminho));
            var nova = new Folha(nome, caminho, copia) { Mudou = true };
            _abertas[caminho] = nova;
            _novas.Add(nova);
            return nova;
        }

        /// <summary>
        /// As datas em dd/mm/aaaa (Diogo, 26/10/2026): o formato 14 do Excel
        /// muda com o computador (saía mm-dd-yy); os estilos com ele passam a
        /// um formato próprio «dd/mm/yyyy».
        /// </summary>
        private void DatasPt()
        {
            var estilos = Ler("xl/styles.xml");
            var raiz = estilos?.Root;
            if (raiz is null) return;
            var comData = raiz.Element(S + "cellXfs")?.Elements(S + "xf").Where(x => (string?)x.Attribute("numFmtId") == "14").ToList() ?? [];
            if (comData.Count == 0) return;
            var formatos = raiz.Element(S + "numFmts");
            if (formatos is null)
            {
                formatos = new XElement(S + "numFmts");
                raiz.AddFirst(formatos);
            }

            var proprio = formatos.Elements(S + "numFmt").FirstOrDefault(f => (string?)f.Attribute("formatCode") == FormatoData);
            var id = (int?)proprio?.Attribute("numFmtId")
                ?? Math.Max(164, formatos.Elements(S + "numFmt").Select(f => (int)f.Attribute("numFmtId")! + 1).DefaultIfEmpty(164).Max());
            if (proprio is null) formatos.Add(new XElement(S + "numFmt", new XAttribute("numFmtId", id), new XAttribute("formatCode", FormatoData)));
            formatos.SetAttributeValue("count", formatos.Elements(S + "numFmt").Count());
            foreach (var xf in comData) xf.SetAttributeValue("numFmtId", id);
            Escrever("xl/styles.xml", estilos!);
        }

        /// <summary>
        /// As células juntas (B1:E2 …) sem linhas por dentro (Diogo, 26/10/2026:
        /// «merge das células»): as células tapadas pela junção levavam as
        /// bordas finas de cada coluna, e há programas que as desenham. Cada
        /// uma passa a um estilo igual mas só com as bordas de fora.
        /// </summary>
        private void JuntasSemLinhasPorDentro()
        {
            var estilos = Ler("xl/styles.xml");
            var xfs = estilos?.Root!.Element(S + "cellXfs");
            var bordas = estilos?.Root!.Element(S + "borders");
            if (xfs is null || bordas is null) return;
            var lista = xfs.Elements(S + "xf").ToList();
            var listaBordas = bordas.Elements(S + "border").ToList();
            var feitos = new Dictionary<(int, int), int>();
            var mudou = false;
            foreach (var folha in _abertas.Values.Where(f => f.Mudou))
            {
                foreach (var junta in folha.Xml.Descendants(S + "mergeCell"))
                {
                    var partes = ((string)junta.Attribute("ref")!).Split(':');
                    if (partes.Length != 2) continue;
                    var (c1, l1) = Referencia(new XElement("c", new XAttribute("r", partes[0])));
                    var (c2, l2) = Referencia(new XElement("c", new XAttribute("r", partes[1])));
                    for (var l = l1; l <= l2; l++)
                    {
                        for (var c = c1; c <= c2; c++)
                        {
                            var celula = Celula(folha.Xml, l, c, criar: false);
                            if (celula is null) continue;
                            var mascara = (c > c1 ? 1 : 0) | (c < c2 ? 2 : 0) | (l > l1 ? 4 : 0) | (l < l2 ? 8 : 0);
                            var estilo = (int?)celula.Attribute("s") ?? 0;
                            if (mascara == 0 || estilo >= lista.Count) continue;
                            if (!feitos.TryGetValue((estilo, mascara), out var novo))
                            {
                                var xf = new XElement(lista[estilo]);
                                var idBorda = (int?)xf.Attribute("borderId") ?? 0;
                                var borda = new XElement(idBorda < listaBordas.Count ? listaBordas[idBorda] : new XElement(S + "border"));
                                foreach (var (bit, lado) in new[] { (1, "left"), (2, "right"), (4, "top"), (8, "bottom") })
                                {
                                    if ((mascara & bit) == 0) continue;
                                    borda.Element(S + lado)?.ReplaceWith(new XElement(S + lado));
                                }

                                if (!XNode.DeepEquals(borda, idBorda < listaBordas.Count ? listaBordas[idBorda] : null))
                                {
                                    bordas.Add(borda);
                                    listaBordas.Add(borda);
                                    xf.SetAttributeValue("borderId", listaBordas.Count - 1);
                                    xf.SetAttributeValue("applyBorder", "1");
                                    xfs.Add(xf);
                                    lista.Add(xf);
                                    novo = lista.Count - 1;
                                }
                                else
                                {
                                    novo = estilo;
                                }

                                feitos[(estilo, mascara)] = novo;
                            }

                            if (novo != estilo)
                            {
                                celula.SetAttributeValue("s", novo);
                                mudou = true;
                            }
                        }
                    }
                }
            }

            if (!mudou) return;
            bordas.SetAttributeValue("count", listaBordas.Count);
            xfs.SetAttributeValue("count", lista.Count);
            Escrever("xl/styles.xml", estilos!);
        }

        /// <summary>
        /// Grava as partes mudadas. A cadeia de cálculo (calcChain) sai, porque
        /// as fórmulas mudaram de sítio, e o Excel recalcula tudo ao abrir.
        /// </summary>
        public void Gravar()
        {
            JuntasSemLinhasPorDentro();
            foreach (var folha in _abertas.Values.Where(f => f.Mudou))
            {
                Escrever(folha.Caminho, folha.Documento);
            }

            foreach (var cadeia in _relacoes.Root!.Elements(Pr + "Relationship").Where(r => (string?)r.Attribute("Type") == TipoCalcChain).ToList())
            {
                _zip.GetEntry(Caminho((string)cadeia.Attribute("Target")!))?.Delete();
                cadeia.Remove();
            }

            _tipos.Root!.Elements(Ct + "Override").Where(o => ((string?)o.Attribute("PartName"))?.EndsWith("/calcChain.xml", StringComparison.OrdinalIgnoreCase) == true).Remove();

            var livro = _livro.Root!;
            var calculo = livro.Element(S + "calcPr");
            if (calculo is null)
            {
                calculo = new XElement(S + "calcPr");
                var antes = livro.Element(S + "definedNames") ?? livro.Element(S + "sheets")!;
                antes.AddAfterSelf(calculo);
            }

            calculo.SetAttributeValue("fullCalcOnLoad", "1");
            DatasPt();

            Escrever("xl/workbook.xml", _livro);
            Escrever("xl/_rels/workbook.xml.rels", _relacoes);
            Escrever("[Content_Types].xml", _tipos);
        }
    }
}
