using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Boquilhas;
using Xunit;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// O Excel das Boquilhas do chefe (Diogo, 21/10/2026: «cada registo que faço
/// passa guardar a informação nesse excel»). O livro dos testes tem o formato
/// do ficheiro dele: um separador por mês, os códigos das máquinas na linha 1
/// (B1 em B, B2 em F … C3 em V, 4 colunas cada: Ref, Sai, Ent, N.Fat) e cada
/// dia em 5 linhas a partir da linha 4 (=DATE(2026,7,1), =A4+1, …).
/// </summary>
public sealed class BqExcelLivroTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly string[] Maquinas = ["B1", "B2", "B3", "C1", "C2", "C3"];

    [Fact]
    public void Escreve_ARefEAsQuantidades_NaMaquinaENoDia_ESomaComoODiogo()
    {
        var livro = LivroDeJulho();
        var movimentos = new[]
        {
            Movimento(1, "Saida", 23, 2026, 7, 3, "C3", "T173", "89"),
            Movimento(2, "Saida", 10, 2026, 7, 3, "C3", "T173", "89"),
            Movimento(3, "Entrada", 30, 2026, 7, 3, "C3", "T173", "89"),
            Movimento(4, "EntradaSemReparacao", 2, 2026, 7, 3, "B1", "T194", "04"),
        };

        var resultado = BqExcelLivro.Escrever(livro, BqExcelPlano.From(movimentos, new(2026, 7, 3), new(2026, 7, 3)));

        Assert.Empty(resultado.Avisos);
        var julho = Folha(resultado.Livro, "xl/worksheets/sheet1.xml");
        // O dia 3 começa na linha 14 (4 + 5 × 2). C3 é V..Y; B1 é B..E.
        Assert.Equal("T173/89", Texto(julho, "V14"));
        Assert.Equal("23+10", Celula(julho, "W14")!.Element(S + "f")!.Value);
        Assert.Equal("30", Celula(julho, "X14")!.Element(S + "v")!.Value);
        Assert.Equal("T194/04", Texto(julho, "B14"));
        Assert.Null(Celula(julho, "C14")!.Element(S + "v"));
        Assert.Equal("2", Celula(julho, "E14")!.Element(S + "v")!.Value);
        // O estilo das células fica.
        Assert.Equal("7", (string?)Celula(julho, "V14")!.Attribute("s"));
        // O dia anterior, escrito à mão, fica como estava.
        Assert.Equal("7", Celula(julho, "B9")!.Element(S + "v")!.Value);
        Assert.Equal("s", (string?)Celula(julho, "B9")!.Attribute("t"));
        Assert.Equal("24", Celula(julho, "C9")!.Element(S + "v")!.Value);

        // A cadeia de cálculo sai e o Excel recalcula ao abrir.
        using var zip = new ZipArchive(new MemoryStream(resultado.Livro));
        Assert.Null(zip.GetEntry("xl/calcChain.xml"));
        Assert.DoesNotContain("calcChain", Ler(zip, "xl/_rels/workbook.xml.rels"));
        Assert.DoesNotContain("calcChain", Ler(zip, "[Content_Types].xml"));
        Assert.Contains("fullCalcOnLoad=\"1\"", Ler(zip, "xl/workbook.xml"));
    }

    [Fact]
    public void UmExcelNovo_TemOMesPedido_ERecebeOsMovimentos_EOsMesesSeguintes()
    {
        var livro = BqExcelLivro.Novo(2026, 10);

        var outubro = Folha(livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("DATE(2026,10,1)", Celula(outubro, "A4")!.Element(S + "f")!.Value);
        Assert.Equal("A9+1", Celula(outubro, "A14")!.Element(S + "f")!.Value);
        Assert.Equal("Quinta", Texto(outubro, "A5"));
        Assert.Equal("C3", Texto(outubro, "V1"));
        Assert.Equal("N.Fat", Texto(outubro, "Y3"));

        var resultado = BqExcelLivro.Escrever(livro, BqExcelPlano.From(
            [Movimento(1, "Saida", 4, 2026, 10, 21, "B3", "T173", "89"), Movimento(2, "Entrada", 3, 2026, 11, 2, "C1", "T194", "04")],
            new(2026, 10, 21), new(2026, 11, 2)));

        Assert.Empty(resultado.Avisos);
        var depois = Folha(resultado.Livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("T173/89", Texto(depois, $"J{BqExcelLivro.LinhaDoDia(21)}"));
        Assert.Equal("4", Celula(depois, $"K{BqExcelLivro.LinhaDoDia(21)}")!.Element(S + "v")!.Value);
        using var zip = new ZipArchive(new MemoryStream(resultado.Livro));
        Assert.Contains("Novembro", Ler(zip, "xl/workbook.xml"));
    }

    /// <summary>
    /// Diogo, 22/10/2026: «o excel que criaram está sem as cores do original
    /// e não tem merges de células». O livro novo tem as junções, as cores
    /// (do tema do Office, como no dele), as larguras e as alturas do original.
    /// </summary>
    [Fact]
    public void UmExcelNovo_TemAsJuncoesAsCoresEAsLargurasDoOriginal()
    {
        var livro = BqExcelLivro.Novo(2026, 10);
        var folha = Folha(livro, "xl/worksheets/sheet1.xml");

        var juntas = folha.Descendants(S + "mergeCell").Select(m => (string)m.Attribute("ref")!).ToList();
        Assert.Equal(7 + 31, juntas.Count);
        Assert.Contains("A1:A3", juntas);
        foreach (var junta in new[] { "B1:E2", "F1:I2", "J1:M2", "N1:Q2", "R1:U2", "V1:Y2", "A5:A8", "A10:A13", "A155:A158" })
        {
            Assert.Contains(junta, juntas);
        }

        Assert.Equal("A1:Y158", (string?)folha.Element(S + "dimension")!.Attribute("ref"));
        Assert.Equal("20.28515625", (string?)folha.Descendants(S + "col").First(c => (string?)c.Attribute("min") == "1").Attribute("width"));
        Assert.Equal("15.7109375", (string?)folha.Descendants(S + "col").First(c => (string?)c.Attribute("min") == "2").Attribute("width"));
        Assert.Equal("21", (string?)Linha(folha, 4).Attribute("ht"));
        Assert.Equal("1", (string?)Linha(folha, 8).Attribute("thickBot"));

        var estilos = new Estilos(livro);
        // «Data» e o cabeçalho: cinza escuro com letra branca; os códigos das máquinas a 26.
        Assert.Equal(("0", "-0.499984740745262"), estilos.Fundo(folha, "A1"));
        Assert.Equal("0", estilos.CorDaLetra(folha, "A1"));
        Assert.Equal(("3", "0.39997558519241921"), estilos.Fundo(folha, "B1"));
        Assert.Equal("26", estilos.Letra(folha, "B1"));
        Assert.Equal(("3", "0.59999389629810485"), estilos.Fundo(folha, "B3"));
        // Cada máquina com a sua cor na Ref; Sai a cinza claro, Ent sem cor de tema, N.Fat a cinza.
        Assert.Equal(("4", "0.79998168889431442"), estilos.Fundo(folha, "B4"));
        Assert.Equal(("9", "0.79998168889431442"), estilos.Fundo(folha, "F4"));
        Assert.Equal(("5", "0.79998168889431442"), estilos.Fundo(folha, "N9"));
        Assert.Equal(("7", "0.79998168889431442"), estilos.Fundo(folha, "V158"));
        Assert.Equal(("0", "-4.9989318521683403E-2"), estilos.Fundo(folha, "C5"));
        Assert.Equal(("0", "-0.249977111117893"), estilos.Fundo(folha, "Y6"));
        // A data a 16 e o dia da semana a 20, ao centro; as bordas grossas a fechar o dia.
        Assert.Equal("16", estilos.Letra(folha, "A4"));
        Assert.Equal("20", estilos.Letra(folha, "A5"));
        Assert.Equal("medium", estilos.Borda(folha, "B8", "bottom"));
        Assert.Equal("medium", estilos.Borda(folha, "F5", "left"));

        using var zip = new ZipArchive(new MemoryStream(livro));
        Assert.Contains("<a:accent1><a:srgbClr val=\"4F81BD\"/>", Ler(zip, "xl/theme/theme1.xml"));
        Assert.Contains("theme/theme1.xml", Ler(zip, "xl/_rels/workbook.xml.rels"));
    }

    [Fact]
    public void UmExcelNovo_SoTemOsDiasDoMes_EOMesSeguinteGanhaOsQueFaltam()
    {
        var livro = BqExcelLivro.Novo(2026, 2);
        var fevereiro = Folha(livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("A1:Y143", (string?)fevereiro.Element(S + "dimension")!.Attribute("ref"));
        Assert.Equal(7 + 28, fevereiro.Descendants(S + "mergeCell").Count());
        Assert.Null(Celula(fevereiro, "A144"));

        var resultado = BqExcelLivro.Escrever(livro, BqExcelPlano.From(
            [Movimento(1, "Saida", 5, 2026, 3, 31, "C2", "T282", "76")], new(2026, 3, 1), new(2026, 3, 31)));

        Assert.Empty(resultado.Avisos);
        var marco = Folha(resultado.Livro, "xl/worksheets/sheet2.xml");
        Assert.Equal("A149+1", Celula(marco, "A154")!.Element(S + "f")!.Value);
        Assert.Equal("Terça", Texto(marco, "A155"));
        Assert.Equal("T282/76", Texto(marco, "R154"));
        Assert.Contains(marco.Descendants(S + "mergeCell"), m => (string?)m.Attribute("ref") == "A155:A158");
        Assert.Equal(7 + 31, marco.Descendants(S + "mergeCell").Count());
        Assert.Equal((string?)Celula(fevereiro, "R139")!.Attribute("s"), (string?)Celula(marco, "R154")!.Attribute("s"));
        Assert.Equal((string?)Celula(fevereiro, "Y143")!.Attribute("s"), (string?)Celula(marco, "Y158")!.Attribute("s"));
    }

    [Fact]
    public void Escrever_NumExcelNovo_GuardaOsEstilosEAsJuncoes()
    {
        var livro = BqExcelLivro.Novo(2026, 10);
        var antes = Folha(livro, "xl/worksheets/sheet1.xml");

        var resultado = BqExcelLivro.Escrever(livro, BqExcelPlano.From(
            [Movimento(1, "Saida", 4, 2026, 10, 22, "B1", "T173", "89")], new(2026, 10, 22), new(2026, 10, 22)));

        var depois = Folha(resultado.Livro, "xl/worksheets/sheet1.xml");
        var linha = BqExcelLivro.LinhaDoDia(22);
        Assert.Equal("T173/89", Texto(depois, $"B{linha}"));
        Assert.Equal(
            antes.Descendants(S + "mergeCell").Select(m => (string)m.Attribute("ref")!),
            depois.Descendants(S + "mergeCell").Select(m => (string)m.Attribute("ref")!));
        Assert.Equal(
            antes.Descendants(S + "c").Select(c => ((string)c.Attribute("r")!, (string?)c.Attribute("s"))),
            depois.Descendants(S + "c").Select(c => ((string)c.Attribute("r")!, (string?)c.Attribute("s"))));
        Assert.Equal(antes.Element(S + "cols")!.ToString(), depois.Element(S + "cols")!.ToString());
        using var zip = new ZipArchive(new MemoryStream(resultado.Livro));
        using var original = new ZipArchive(new MemoryStream(livro));
        Assert.Equal(Ler(original, "xl/styles.xml"), Ler(zip, "xl/styles.xml"));
    }

    [Fact]
    public void UmDiaReescrito_SemOMovimentoAnulado_FicaLimpo()
    {
        var primeira = BqExcelLivro.Escrever(LivroDeJulho(), BqExcelPlano.From(
            [Movimento(1, "Saida", 5, 2026, 7, 3, "B2", "T282", "76"), Movimento(2, "Saida", 6, 2026, 7, 3, "B2", "T282", "77")],
            new(2026, 7, 3), new(2026, 7, 3)));
        var antes = primeira.Livro;
        Assert.Equal("T282/77", Texto(Folha(antes, "xl/worksheets/sheet1.xml"), "F15"));
        Assert.Equal(
            [new BqExcelEscrita(new(2026, 7, 3), "B2", 14, "T282/76"), new BqExcelEscrita(new(2026, 7, 3), "B2", 15, "T282/77")],
            primeira.Escritas!);

        var depois = BqExcelLivro.Escrever(antes, BqExcelPlano.From(
            [Movimento(1, "Saida", 5, 2026, 7, 3, "B2", "T282", "76")], new(2026, 7, 3), new(2026, 7, 3)), primeira.Escritas).Livro;

        var julho = Folha(depois, "xl/worksheets/sheet1.xml");
        Assert.Equal("T282/76", Texto(julho, "F14"));
        Assert.Null(Texto(julho, "F15"));
        Assert.Null(Celula(julho, "G15")!.Element(S + "v"));
    }

    [Fact]
    public void AsLinhasEscritasAMao_NoMesmoDia_FicamIntactas_AoEscreverEAoAnular()
    {
        // O ficheiro é do chefe: no dia 2 (linha 9) o B1 tem uma linha escrita
        // à mão (S022/76, Sai 24). A app escreve na linha livre seguinte e,
        // anulado o movimento, só limpa a linha dela.
        var escrito = BqExcelLivro.Escrever(LivroDeJulho(), BqExcelPlano.From(
            [Movimento(1, "Saida", 8, 2026, 7, 2, "B1", "T194", "04")], new(2026, 7, 2), new(2026, 7, 2)));

        var julho = Folha(escrito.Livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("7", Celula(julho, "B9")!.Element(S + "v")!.Value);
        Assert.Equal("24", Celula(julho, "C9")!.Element(S + "v")!.Value);
        Assert.Equal("T194/04", Texto(julho, "B10"));
        Assert.Equal("8", Celula(julho, "C10")!.Element(S + "v")!.Value);

        var anulado = BqExcelLivro.Escrever(escrito.Livro, BqExcelPlano.From([], new(2026, 7, 2), new(2026, 7, 2)), escrito.Escritas);

        julho = Folha(anulado.Livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("7", Celula(julho, "B9")!.Element(S + "v")!.Value);
        Assert.Equal("24", Celula(julho, "C9")!.Element(S + "v")!.Value);
        Assert.Null(Texto(julho, "B10"));
        Assert.Null(Celula(julho, "C10")!.Element(S + "v"));
        Assert.Empty(anulado.Escritas!);

        // Sem as escritas de antes (outro computador), nada do que lá está é apagado.
        var semRasto = BqExcelLivro.Escrever(escrito.Livro, BqExcelPlano.From([], new(2026, 7, 2), new(2026, 7, 2)));
        Assert.Equal("T194/04", Texto(Folha(semRasto.Livro, "xl/worksheets/sheet1.xml"), "B10"));
    }

    [Fact]
    public void UmMesSemSeparador_CriaOSeparador_ComAsDatasEOsDiasDaSemana()
    {
        var resultado = BqExcelLivro.Escrever(LivroDeJulho(), BqExcelPlano.From(
            [Movimento(1, "Saida", 7, 2026, 10, 21, "B2", "T282", "76")], new(2026, 10, 1), new(2026, 10, 21)));

        Assert.Empty(resultado.Avisos);
        using var zip = new ZipArchive(new MemoryStream(resultado.Livro));
        var livro = XDocument.Parse(Ler(zip, "xl/workbook.xml"));
        var outubro = livro.Descendants(S + "sheet").Single(s => (string)s.Attribute("name")! == "Outubro");
        var id = (string)outubro.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))!;
        Assert.Contains($"Id=\"{id}\"", Ler(zip, "xl/_rels/workbook.xml.rels"));
        Assert.Contains("/xl/worksheets/sheet2.xml", Ler(zip, "[Content_Types].xml"));

        var folha = Folha(resultado.Livro, "xl/worksheets/sheet2.xml");
        Assert.Equal("DATE(2026,10,1)", Celula(folha, "A4")!.Element(S + "f")!.Value);
        Assert.Null(Celula(folha, "A4")!.Element(S + "v"));
        Assert.Equal("Quinta", Texto(folha, "A5"));
        Assert.Equal("A99+1", Celula(folha, "A104")!.Element(S + "f")!.Value);
        Assert.Equal("Quarta", Texto(folha, "A105"));
        Assert.Equal("T282/76", Texto(folha, "F104"));
        Assert.Equal("7", Celula(folha, "G104")!.Element(S + "v")!.Value);
        // Os valores do molde (julho) não passam para outubro, e só um separador fica selecionado.
        Assert.Null(Texto(folha, "B9"));
        Assert.DoesNotContain(folha.Descendants(S + "sheetView"), v => v.Attribute("tabSelected") is not null);
        // A folha nova não aponta para partes do molde (a impressora do julho).
        Assert.DoesNotContain(folha.DescendantsAndSelf().Attributes(),
            a => a.Name.Namespace == "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
    }

    [Fact]
    public void UmMesSemSeparador_ESemMovimentos_NaoCriaNada()
    {
        var resultado = BqExcelLivro.Escrever(LivroDeJulho(), BqExcelPlano.From([], new(2026, 9, 1), new(2026, 9, 30)));

        using var zip = new ZipArchive(new MemoryStream(resultado.Livro));
        Assert.DoesNotContain("Setembro", Ler(zip, "xl/workbook.xml"));
        Assert.Null(zip.GetEntry("xl/worksheets/sheet2.xml"));
    }

    [Fact]
    public void OutroAno_EMaisDeCincoBoquilhasNoDia_FicamNosAvisos()
    {
        var seis = Enumerable.Range(1, 6).Select(i => Movimento(i, "Saida", 1, 2026, 7, 3, "B1", "T" + i, "1"));
        var resultado = BqExcelLivro.Escrever(LivroDeJulho(), BqExcelPlano.From(
            seis.Append(Movimento(9, "Saida", 1, 2027, 1, 2, "B1", "T9", "1")), new(2026, 7, 3), new(2026, 7, 3))
            .Concat(BqExcelPlano.From([Movimento(9, "Saida", 1, 2027, 1, 2, "B1", "T9", "1")], new(2027, 1, 2), new(2027, 1, 2)))
            .ToList());

        Assert.Contains(resultado.Avisos, a => a.Contains("03/07 · B1: 6 boquilhas no dia, só há 5 linhas livres", StringComparison.Ordinal) && a.EndsWith("T6/1.", StringComparison.Ordinal));
        Assert.Contains(resultado.Avisos, a => a.StartsWith("O Excel é de 2026", StringComparison.Ordinal));
        var julho = Folha(resultado.Livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("T5/1", Texto(julho, "B18"));
    }

    [Fact]
    public void UmFicheiroQueNaoEExcel_ERecusado()
    {
        Assert.Throws<BqExcelInvalidoException>(() => BqExcelLivro.Escrever(Encoding.UTF8.GetBytes("não é um zip"), []));
    }

    [Fact]
    public void Atualizar_OntemIgual_ParaLogo_ESoEscreveHoje()
    {
        // Diogo, 22/10/2026: «dia anterior está igual ao que a app tem? Se
        // sim não atualizamos, acabou».
        var movimentos = new[]
        {
            Movimento(1, "Saida", 5, 2026, 7, 20, "B1", "T1", "1"),
            Movimento(2, "Saida", 7, 2026, 7, 21, "B2", "T2", "2"),
            Movimento(3, "Entrada", 4, 2026, 7, 22, "C3", "T3", "3"),
        };
        var escrito = BqExcelLivro.Escrever(LivroDeJulho(), BqExcelPlano.From(movimentos, new(2026, 7, 20), new(2026, 7, 21)));
        // O dia 20 fica diferente, mas não se chega lá: o 21 já está igual.
        var plano = BqExcelPlano.From(movimentos.Append(Movimento(4, "Saida", 9, 2026, 7, 20, "B3", "T4", "4")), new(2026, 7, 1), new(2026, 7, 22));

        var atualizacao = BqExcelLivro.Atualizar(escrito.Livro, plano, new(2026, 7, 21), escrito.Escritas);

        Assert.Empty(atualizacao.Atualizados);
        Assert.Equal("O Excel já estava em dia.", atualizacao.Mensagem);
        var julho = Folha(atualizacao.Resultado.Livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("T3/3", Texto(julho, $"V{BqExcelLivro.LinhaDoDia(22)}"));
        Assert.Null(Texto(julho, $"J{BqExcelLivro.LinhaDoDia(20)}"));
    }

    [Fact]
    public void Atualizar_DoisDiasDiferentes_EDepoisUmIgual_AtualizaExatamenteDois()
    {
        var antes = new[]
        {
            Movimento(1, "Saida", 5, 2026, 7, 18, "B1", "T1", "1"),
            Movimento(2, "Saida", 7, 2026, 7, 19, "B2", "T2", "2"),
            Movimento(3, "Saida", 8, 2026, 7, 20, "B2", "T2", "2"),
        };
        var escrito = BqExcelLivro.Escrever(LivroDeJulho(), BqExcelPlano.From(antes, new(2026, 7, 18), new(2026, 7, 20)));
        // Na app: o 19 foi corrigido (7 → 6), o 20 ganhou uma entrada e o 17 tem um movimento que nunca foi escrito.
        var agora = new[]
        {
            Movimento(9, "Saida", 3, 2026, 7, 17, "C1", "T9", "9"),
            antes[0],
            Movimento(2, "Saida", 6, 2026, 7, 19, "B2", "T2", "2"),
            antes[2],
            Movimento(4, "Entrada", 8, 2026, 7, 20, "B2", "T2", "2"),
        };

        var atualizacao = BqExcelLivro.Atualizar(escrito.Livro, BqExcelPlano.From(agora, new(2026, 7, 17), new(2026, 7, 21)), new(2026, 7, 20), escrito.Escritas);

        Assert.Equal([new DateOnly(2026, 7, 19), new DateOnly(2026, 7, 20)], atualizacao.Atualizados);
        Assert.Equal("Atualizados 2 dias: 19/07, 20/07.", atualizacao.Mensagem);
        var julho = Folha(atualizacao.Resultado.Livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("6", Celula(julho, $"G{BqExcelLivro.LinhaDoDia(19)}")!.Element(S + "v")!.Value);
        Assert.Equal("8", Celula(julho, $"H{BqExcelLivro.LinhaDoDia(20)}")!.Element(S + "v")!.Value);
        Assert.Null(Celula(julho, $"G{BqExcelLivro.LinhaDoDia(19) + 1}")!.Element(S + "v"));
        // Parou no 18 (igual): o 17 ficou por escrever.
        Assert.Null(Texto(julho, $"N{BqExcelLivro.LinhaDoDia(17)}"));
    }

    [Fact]
    public void Atualizar_PassaParaOMesAnterior_EPara_NoPrimeiroDiaDaApp()
    {
        // Ontem é 2 de agosto: o livro só tem «Julho». O 2 e o 1 de agosto e
        // o 31 de julho estão por escrever; o plano começa no 31 (o primeiro
        // dia com dados na app), por isso pára aí.
        var movimentos = new[]
        {
            Movimento(1, "Saida", 1, 2026, 7, 31, "B1", "T1", "1"),
            Movimento(2, "Saida", 2, 2026, 8, 1, "B1", "T2", "2"),
            Movimento(3, "Saida", 3, 2026, 8, 2, "B1", "T3", "3"),
        };

        var atualizacao = BqExcelLivro.Atualizar(LivroDeJulho(), BqExcelPlano.From(movimentos, new(2026, 7, 31), new(2026, 8, 3)), new(2026, 8, 2));

        Assert.Equal([new DateOnly(2026, 7, 31), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2)], atualizacao.Atualizados);
        Assert.Equal("Atualizados 3 dias: 31/07, 01/08, 02/08.", atualizacao.Mensagem);
        Assert.Equal("T1/1", Texto(Folha(atualizacao.Resultado.Livro, "xl/worksheets/sheet1.xml"), $"B{BqExcelLivro.LinhaDoDia(31)}"));

        // Com tudo escrito, um novo «Atualizar» pára logo no 2 de agosto.
        var outra = BqExcelLivro.Atualizar(atualizacao.Resultado.Livro, BqExcelPlano.From(movimentos, new(2026, 7, 31), new(2026, 8, 3)), new(2026, 8, 2), atualizacao.Resultado.Escritas);
        Assert.Empty(outra.Atualizados);

        // E no 30 de julho, igual (sem nada na app nem no Excel), também pára.
        var comMais = BqExcelLivro.Atualizar(LivroDeJulho(), BqExcelPlano.From(movimentos.Skip(1), new(2026, 7, 29), new(2026, 8, 3)), new(2026, 8, 2));
        Assert.Equal([new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2)], comMais.Atualizados);
    }

    [Fact]
    public void Atualizar_AsLinhasEscritasAMao_FicamIntactas_ENaoContamComoDiferenca()
    {
        // O dia 2 do livro tem uma linha à mão em B1 (S022/76, 24).
        var movimentos = new[] { Movimento(1, "Saida", 4, 2026, 7, 2, "B1", "T1", "1") };

        var atualizacao = BqExcelLivro.Atualizar(LivroDeJulho(), BqExcelPlano.From(movimentos, new(2026, 7, 1), new(2026, 7, 3)), new(2026, 7, 2));

        Assert.Equal([new DateOnly(2026, 7, 2)], atualizacao.Atualizados);
        var julho = Folha(atualizacao.Resultado.Livro, "xl/worksheets/sheet1.xml");
        Assert.Equal("7", Celula(julho, "B9")!.Element(S + "v")!.Value);
        Assert.Equal("24", Celula(julho, "C9")!.Element(S + "v")!.Value);
        Assert.Equal("T1/1", Texto(julho, "B10"));

        // Depois de escrito o dia está igual, com a linha à mão ainda lá.
        var outra = BqExcelLivro.Atualizar(atualizacao.Resultado.Livro, BqExcelPlano.From(movimentos, new(2026, 7, 1), new(2026, 7, 3)), new(2026, 7, 2), atualizacao.Resultado.Escritas);
        Assert.Empty(outra.Atualizados);
        Assert.Equal("7", Celula(Folha(outra.Resultado.Livro, "xl/worksheets/sheet1.xml"), "B9")!.Element(S + "v")!.Value);
    }

    private static BqHistoryMovementItem Movimento(int id, string tipo, int quantidade, int ano, int mes, int dia, string maquina, string referencia, string lote) =>
        new(id, id, tipo, quantidade, null, new DateTime(ano, mes, dia, 9, 0, 0, DateTimeKind.Utc).AddMinutes(id), null, referencia, lote, maquina, null, null);

    private static XElement Folha(byte[] livro, string caminho)
    {
        using var zip = new ZipArchive(new MemoryStream(livro));
        return XDocument.Parse(Ler(zip, caminho)).Root!;
    }

    private static string Ler(ZipArchive zip, string caminho)
    {
        using var leitor = new StreamReader(zip.GetEntry(caminho)!.Open());
        return leitor.ReadToEnd();
    }

    private static XElement? Celula(XElement folha, string referencia) =>
        folha.Descendants(S + "c").SingleOrDefault(c => (string?)c.Attribute("r") == referencia);

    private static XElement Linha(XElement folha, int numero) =>
        folha.Descendants(S + "row").Single(r => (string?)r.Attribute("r") == numero.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>O formato de uma célula, lido do styles.xml do livro.</summary>
    private sealed class Estilos
    {
        private readonly List<XElement> _xfs;
        private readonly List<XElement> _fontes;
        private readonly List<XElement> _fundos;
        private readonly List<XElement> _bordas;

        public Estilos(byte[] livro)
        {
            using var zip = new ZipArchive(new MemoryStream(livro));
            var estilos = XDocument.Parse(Ler(zip, "xl/styles.xml")).Root!;
            _xfs = estilos.Element(S + "cellXfs")!.Elements(S + "xf").ToList();
            _fontes = estilos.Element(S + "fonts")!.Elements(S + "font").ToList();
            _fundos = estilos.Element(S + "fills")!.Elements(S + "fill").ToList();
            _bordas = estilos.Element(S + "borders")!.Elements(S + "border").ToList();
        }

        private XElement Xf(XElement folha, string celula) => _xfs[int.Parse((string)Celula(folha, celula)!.Attribute("s")!)];

        private XElement Parte(List<XElement> lista, XElement folha, string celula, string atributo) =>
            lista[int.Parse((string)Xf(folha, celula).Attribute(atributo)!)];

        public (string? Tema, string? Tom) Fundo(XElement folha, string celula)
        {
            var padrao = Parte(_fundos, folha, celula, "fillId").Element(S + "patternFill")!;
            Assert.Equal("solid", (string?)padrao.Attribute("patternType"));
            var cor = padrao.Element(S + "fgColor")!;
            return ((string?)cor.Attribute("theme"), (string?)cor.Attribute("tint"));
        }

        public string? Letra(XElement folha, string celula) =>
            (string?)Parte(_fontes, folha, celula, "fontId").Element(S + "sz")!.Attribute("val");

        public string? CorDaLetra(XElement folha, string celula) =>
            (string?)Parte(_fontes, folha, celula, "fontId").Element(S + "color")?.Attribute("theme");

        public string? Borda(XElement folha, string celula, string lado) =>
            (string?)Parte(_bordas, folha, celula, "borderId").Element(S + lado)?.Attribute("style");
    }

    private static string? Texto(XElement folha, string referencia)
    {
        var c = Celula(folha, referencia);
        return c is null || (string?)c.Attribute("t") != "inlineStr" ? null : string.Concat(c.Descendants(S + "t").Select(t => t.Value));
    }

    /// <summary>
    /// Um livro com o separador «Julho» no formato do ficheiro do Diogo: os
    /// códigos das máquinas na linha 1 (texto partilhado), 31 dias de 5
    /// linhas com todas as células de B a Y já com estilo, um dia escrito à mão
    /// e a cadeia de cálculo (calcChain) que o Excel guarda.
    /// </summary>
    private static byte[] LivroDeJulho()
    {
        var linhas = new StringBuilder();
        linhas.Append("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>6</v></c>");
        for (var m = 0; m < Maquinas.Length; m++)
        {
            linhas.Append($"<c r=\"{BqExcelLivro.NomeDaColuna(2 + 4 * m)}1\" t=\"s\"><v>{m}</v></c>");
        }

        linhas.Append("</row><row r=\"3\"><c r=\"B3\" t=\"inlineStr\"><is><t>Ref</t></is></c></row>");
        for (var dia = 1; dia <= 31; dia++)
        {
            var primeira = BqExcelLivro.LinhaDoDia(dia);
            for (var r = primeira; r < primeira + BqExcelLivro.LinhasPorDia; r++)
            {
                linhas.Append($"<row r=\"{r}\">");
                if (r == primeira)
                {
                    linhas.Append(dia == 1 ? "<c r=\"A4\" s=\"3\"><f>DATE(2026,7,1)</f><v>46204</v></c>" : $"<c r=\"A{r}\" s=\"3\"><f>A{r - 5}+1</f><v>{46203 + dia}</v></c>");
                }
                else if (r == primeira + 1)
                {
                    linhas.Append($"<c r=\"A{r}\" t=\"inlineStr\"><is><t>Dia{dia}</t></is></c>");
                }

                for (var c = 2; c <= 25; c++)
                {
                    var nome = BqExcelLivro.NomeDaColuna(c) + r;
                    linhas.Append(dia == 2 && r == primeira && c == 2 ? $"<c r=\"{nome}\" s=\"7\" t=\"s\"><v>7</v></c>"
                        : dia == 2 && r == primeira && c == 3 ? $"<c r=\"{nome}\" s=\"7\"><v>24</v></c>"
                        : $"<c r=\"{nome}\" s=\"7\"/>");
                }

                linhas.Append("</row>");
            }
        }

        var folha = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><dimension ref=\"A1:Y158\"/><sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\"/></sheetViews><sheetData>"
            + linhas + "</sheetData><mergeCells count=\"2\"><mergeCell ref=\"A5:A8\"/><mergeCell ref=\"A155:A158\"/></mergeCells><pageSetup r:id=\"rId1\"/></worksheet>";
        var textos = string.Concat(Maquinas.Select(m => $"<si><t>{m}</t></si>")) + "<si><t>Data</t></si><si><t>S022/76</t></si>";

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Parte(string caminho, string xml)
            {
                using var escrita = new StreamWriter(zip.CreateEntry(caminho).Open(), new UTF8Encoding(false));
                escrita.Write(xml);
            }

            Parte("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/><Override PartName=\"/xl/calcChain.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.calcChain+xml\"/></Types>");
            Parte("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Parte("xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Julho\" sheetId=\"8\" r:id=\"rId7\"/></sheets><calcPr calcId=\"125725\"/></workbook>");
            Parte("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId7\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId8\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/><Relationship Id=\"rId9\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/calcChain\" Target=\"calcChain.xml\"/></Relationships>");
            Parte("xl/worksheets/sheet1.xml", folha);
            Parte("xl/sharedStrings.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">{textos}</sst>");
            Parte("xl/calcChain.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><calcChain xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><c r=\"A4\" i=\"1\"/></calcChain>");
        }

        return memoria.ToArray();
    }
}
