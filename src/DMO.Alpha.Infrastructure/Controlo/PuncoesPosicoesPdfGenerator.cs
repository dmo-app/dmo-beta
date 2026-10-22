using System.Globalization;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O PDF das punções por posição da linha (a tabela aprovada pelo Diogo a
/// 22/10/2026): A4 deitado, o título «Registo de punções PU · NNPB», a
/// grelha das secções com as posições A a D, a reserva, os totais e os
/// punções repetidos com o lote. Gerado em memória e descarregado pelo
/// browser; nunca grava no servidor.
/// </summary>
public sealed class PuncoesPosicoesPdfGenerator
{
    static PuncoesPosicoesPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly CultureInfo Pt = DmoNumberFormat.Culture;

    /// <summary>Quantos punções da reserva cabem numa linha da tabela da reserva.</summary>
    private const int ReservaPorLinha = 12;

    public sealed record Input(
        string Reference,
        string Linha,
        string ProductionNumber,
        DateOnly? InicioEm,
        DateOnly? FimEm,
        string? RegistadoPor,
        PuncoesNaLinha Puncoes);

    public byte[] GenerateBytes(Input input) =>
        Document.Create(container => Compose(container, input)).GeneratePdf();

    /// <summary>O subtítulo: Referência · Linha · Produção · datas · Registado por.</summary>
    public static string Subtitulo(Input input)
    {
        var partes = new List<string>
        {
            $"Referência {input.Reference}",
            $"Linha {input.Linha}",
            $"Produção {input.ProductionNumber}"
        };
        var datas = (input.InicioEm, input.FimEm) switch
        {
            ({ } inicio, { } fim) when inicio != fim => $"{inicio.ToString("dd/MM/yyyy", Pt)} a {fim.ToString("dd/MM/yyyy", Pt)}",
            ({ } inicio, _) => inicio.ToString("dd/MM/yyyy", Pt),
            (null, { } fim) => fim.ToString("dd/MM/yyyy", Pt),
            _ => null
        };
        if (datas is not null) partes.Add(datas);
        if (!string.IsNullOrWhiteSpace(input.RegistadoPor)) partes.Add($"Registado por {input.RegistadoPor}");
        return string.Join(" · ", partes);
    }

    private static void Compose(IDocumentContainer container, Input input)
    {
        var grelha = input.Puncoes;
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(12, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(DmoPdfPalette.Ink));

            page.Header().Column(header =>
            {
                header.Item().Text("Registo de punções PU · NNPB").FontSize(16).SemiBold();
                header.Item().PaddingTop(2).Text(Subtitulo(input)).FontSize(9.5f).FontColor(DmoPdfPalette.Muted);
            });

            page.Content().PaddingTop(12).Column(content =>
            {
                content.Item().Table(t =>
                {
                    t.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(62);
                        for (var s = 0; s < grelha.Seccoes; s++) columns.RelativeColumn();
                    });

                    t.Header(h =>
                    {
                        h.Cell().RowSpan(2).Element(Cabecalho).Text("Posição").SemiBold().FontColor(DmoPdfPalette.White);
                        h.Cell().ColumnSpan((uint)grelha.Seccoes).Element(Cabecalho).Text("Secção").SemiBold().FontColor(DmoPdfPalette.White);
                        for (var s = 1; s <= grelha.Seccoes; s++)
                        {
                            h.Cell().Element(Cabecalho).Text(s.ToString(Pt)).SemiBold().FontColor(DmoPdfPalette.White);
                        }
                    });

                    var altura = grelha.Seccoes > 16 ? 34 : 46;
                    for (var p = 0; p < PuncoesPosicoes.PosicoesPorSeccao; p++)
                    {
                        t.Cell().Element(c => Celula(c, DmoPdfPalette.Tint, altura)).Text(PuncoesPosicoes.Letra(p)).SemiBold();
                        for (var s = 1; s <= grelha.Seccoes; s++)
                        {
                            var puncao = grelha.Em(s, p);
                            t.Cell().Element(c => Celula(c, DmoPdfPalette.White, altura)).Column(c => Numero(c, puncao, grelha.Seccoes > 16 ? 14 : 18));
                        }
                    }
                });

                content.Item().PaddingTop(16).Row(row =>
                {
                    row.Spacing(20);
                    row.AutoItem().Element(c => Reserva(c, grelha));
                    row.AutoItem().Element(c => Totais(c, input));
                    row.AutoItem().Element(c => Repetidos(c, grelha));
                });
            });

            page.Footer().Row(footer =>
            {
                footer.RelativeItem().Text($"Portal DMO · Gerado em {DmoBusinessTime.Now.ToString("g", Pt)} · {input.ProductionNumber}").FontSize(7).Light();
                footer.RelativeItem().AlignRight().Text("Desenvolvido por Diogo Oliveira").FontSize(7).Light();
            });
        });
    }

    private static IContainer Cabecalho(IContainer c) =>
        c.Border(0.6f).BorderColor(DmoPdfPalette.Line).Background(DmoPdfPalette.Deep)
            .PaddingVertical(5).PaddingHorizontal(4).AlignCenter().AlignMiddle();

    private static IContainer Celula(IContainer c, string fundo, float altura) =>
        c.Border(0.6f).BorderColor(DmoPdfPalette.Line).Background(fundo).MinHeight(altura)
            .Padding(3).AlignCenter().AlignMiddle();

    private static IContainer Simples(IContainer c, string fundo = DmoPdfPalette.White) =>
        c.Border(0.6f).BorderColor(DmoPdfPalette.Line).Background(fundo)
            .PaddingVertical(5).PaddingHorizontal(10).AlignCenter().AlignMiddle();

    private static void Numero(ColumnDescriptor c, PuncaoNaPosicao? puncao, float tamanho)
    {
        if (puncao is null)
        {
            c.Item().AlignCenter().Text(string.Empty);
            return;
        }

        var numero = c.Item().AlignCenter().Text(puncao.Number).FontSize(tamanho);
        if (puncao.Repetido)
        {
            numero.Bold();
            c.Item().AlignCenter().Text($"Lote {puncao.Lot}").FontSize(7).SemiBold().FontColor(DmoPdfPalette.Muted);
        }
    }

    private static void Reserva(IContainer container, PuncoesNaLinha grelha)
    {
        var colunas = Math.Clamp(grelha.Reserva.Count, 1, ReservaPorLinha);
        container.Table(t =>
        {
            t.ColumnsDefinition(columns =>
            {
                for (var i = 0; i < colunas; i++) columns.ConstantColumn(38);
            });
            t.Header(h => h.Cell().ColumnSpan((uint)colunas).Element(c => Simples(c, DmoPdfPalette.WarningBackground)).Text("Reserva").SemiBold());
            if (grelha.Reserva.Count == 0)
            {
                t.Cell().Element(c => Simples(c)).Text("—").FontColor(DmoPdfPalette.Muted);
                return;
            }

            foreach (var puncao in grelha.Reserva)
            {
                t.Cell().Element(c => Simples(c)).Column(c => Numero(c, puncao, 12));
            }

            for (var resto = grelha.Reserva.Count % colunas; resto != 0 && resto < colunas; resto++)
            {
                t.Cell().Element(c => Simples(c)).Text(string.Empty);
            }
        });
    }

    private static void Totais(IContainer container, Input input)
    {
        var grelha = input.Puncoes;
        container.Table(t =>
        {
            t.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.ConstantColumn(40);
            });
            foreach (var (rotulo, valor) in new[]
                     {
                         ("Punções verificados", grelha.Total),
                         ($"Linha {input.Linha}", grelha.Linha.Count),
                         ("Reserva", grelha.Reserva.Count)
                     })
            {
                t.Cell().PaddingVertical(2).Text(rotulo);
                t.Cell().PaddingVertical(2).Text(valor.ToString(Pt)).Bold();
            }
        });
    }

    private static void Repetidos(IContainer container, PuncoesNaLinha grelha)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(64);
                columns.ConstantColumn(60);
                columns.ConstantColumn(50);
            });
            t.Header(h =>
            {
                h.Cell().ColumnSpan(3).Element(c => Simples(c, DmoPdfPalette.Tint)).Text("Punções repetidos").SemiBold();
                h.Cell().Element(c => Simples(c, DmoPdfPalette.Tint)).Text("Posição").SemiBold();
                h.Cell().Element(c => Simples(c, DmoPdfPalette.Tint)).Text("Punção").SemiBold();
                h.Cell().Element(c => Simples(c, DmoPdfPalette.Tint)).Text("Lote").SemiBold();
            });
            if (grelha.Repetidos.Count == 0)
            {
                t.Cell().ColumnSpan(3).Element(c => Simples(c)).Text("Sem punções repetidos").FontColor(DmoPdfPalette.Muted);
                return;
            }

            foreach (var puncao in grelha.Repetidos)
            {
                t.Cell().Element(c => Simples(c)).Text(puncao.Etiqueta);
                t.Cell().Element(c => Simples(c)).Text(puncao.Number).Bold();
                t.Cell().Element(c => Simples(c)).Text(puncao.Lot);
            }
        });
    }
}
