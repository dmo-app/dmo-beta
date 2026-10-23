using System.IO.Compression;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Boquilhas;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

public sealed class BqExcelValidationTests
{
    [Fact]
    public void ExcelSemEstrutura_RecusaSemErroInterno()
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            foreach (var name in new[] { "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "[Content_Types].xml" })
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write("<root/>");
            }
        }
        Assert.Throws<BqExcelInvalidoException>(() => BqExcelLivro.Escrever(memory.ToArray(), []));
    }

    [Fact]
    public void ExcelComXmlDemasiadoGrande_RecusaAntesDeExpandir()
    {
        using var memory = new MemoryStream();
        memory.Write(BqExcelLivro.Novo(2026, 10));
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("xl/extra.xml").Open());
            writer.Write("<root>");
            writer.Write(new string('a', 17 * 1024 * 1024));
            writer.Write("</root>");
        }
        Assert.Throws<BqExcelInvalidoException>(() => BqExcelLivro.Escrever(memory.ToArray(), []));
    }
}
