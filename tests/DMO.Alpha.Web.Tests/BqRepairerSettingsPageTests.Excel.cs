using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class BqRepairerSettingsPageTests
{
    private static readonly XNamespace Sml = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public async Task ExcelDasBoquilhas_OCartao_AVersaoMudaComUmMovimento_EOFicheiroVoltaEscrito()
    {
        // O Excel das Boquilhas do chefe (Diogo, 21/10/2026: «cada registo
        // que faço passa guardar a informação nesse excel que está numa pasta
        // do pc»): o cartão nas Definições, a versão que o navegador compara
        // e o ficheiro devolvido com o movimento de hoje escrito no mês de hoje
        // (o separador do mês cria-se a partir de outro mês do livro).
        using var client = await LoginBoquilhasAsync("4741");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client));
        Assert.Contains("id=\"bqDefExcel\"", html);
        Assert.Contains("data-bq-excel-pasta>Escolher a pasta</button>", html);
        Assert.Contains("data-bq-excel-escolher>Escolher um Excel que já existe</button>", html);
        Assert.Contains("Nenhum Excel escolhido neste computador.", html);
        Assert.Contains("/js/dmo-excel-bq.js", html);

        var antes = await VersaoAsync(client);
        var hoje = DmoBusinessTime.Now;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "BQ-EXCEL", Type = ToolType.Bq, Reference = "T173", Lot = "89" });
            var trace = new BqRepairTrace { ToolId = "BQ-EXCEL" };
            db.BqRepairTraces.Add(trace);
            await db.SaveChangesAsync();
            db.BqMovements.Add(new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Saida,
                Quantity = 12,
                MachineCodeFrozen = "C3",
                OccurredAtUtc = DmoBusinessTime.ToUtc(hoje.Date.AddHours(12)),
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var depois = await VersaoAsync(client);
        Assert.NotEqual(antes, depois);

        using var resposta = await PostExcelAsync(client, LivroComJaneiro(hoje.Year), hoje.ToString("yyyy-MM-dd"));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal(depois, resposta.Headers.GetValues("X-Dmo-Excel-Versao").Single());

        using var zip = new ZipArchive(new MemoryStream(await resposta.Content.ReadAsByteArrayAsync()));
        var livro = XDocument.Load(zip.GetEntry("xl/workbook.xml")!.Open());
        var mes = new[] { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" }[hoje.Month - 1];
        Assert.Contains(livro.Descendants(Sml + "sheet"), s => (string)s.Attribute("name")! == mes);
        // O separador do mês pelo nome: o livro ganha os 12 meses (Diogo, 26/10/2026).
        var rId = (string)livro.Descendants(Sml + "sheet").Single(s => (string)s.Attribute("name")! == mes).Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))!;
        var alvo = (string)XDocument.Load(zip.GetEntry("xl/_rels/workbook.xml.rels")!.Open()).Root!.Elements().Single(r => (string?)r.Attribute("Id") == rId).Attribute("Target")!;
        var folha = XDocument.Load(zip.GetEntry(alvo.StartsWith('/') ? alvo[1..] : "xl/" + alvo)!.Open()).Root!;
        var linha = 4 + 5 * (hoje.Day - 1);
        var refCelula = folha.Descendants(Sml + "c").Single(c => (string)c.Attribute("r")! == $"V{linha}");
        Assert.Equal("T173/89", string.Concat(refCelula.Descendants(Sml + "t").Select(t => t.Value)));
        Assert.Equal("12", folha.Descendants(Sml + "c").Single(c => (string)c.Attribute("r")! == $"W{linha}").Element(Sml + "v")!.Value);
    }

    [Fact]
    public async Task ExcelDasBoquilhas_UmExcelNovo_TemOSeparadorDoMes()
    {
        // Diogo, 21/10/2026: «escolher dir do excel que vamos criar». A pasta
        // sem Excel recebe um livro novo, vazio, com o mês pedido.
        using var client = await LoginBoquilhasAsync("4743");
        using var resposta = await client.GetAsync("/Boquilhas/Definicoes?handler=ExcelNovo&dia=2026-10-21");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resposta.Content.Headers.ContentType?.MediaType);
        using var zip = new ZipArchive(new MemoryStream(await resposta.Content.ReadAsByteArrayAsync()));
        var livro = XDocument.Load(zip.GetEntry("xl/workbook.xml")!.Open());
        // Os 12 meses, cada um no seu separador (Diogo, 26/10/2026).
        Assert.Equal(
            ["Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"],
            livro.Descendants(Sml + "sheet").Select(s => (string)s.Attribute("name")!));
    }

    [Fact]
    public async Task ExcelDasBoquilhas_SemFicheiroOuSemSerExcel_RespondeComAMensagem()
    {
        using var client = await LoginBoquilhasAsync("4742");

        using var semFicheiro = await PostExcelAsync(client, null, "2026-10-21");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, semFicheiro.StatusCode);
        Assert.Equal("Escolha o Excel das Boquilhas (.xlsx) nas Definições das Boquilhas.", await semFicheiro.Content.ReadAsStringAsync());

        using var naoExcel = await PostExcelAsync(client, Encoding.UTF8.GetBytes("não é um Excel"), "2026-10-21");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, naoExcel.StatusCode);
        Assert.Equal("O ficheiro escolhido não é um Excel (.xlsx) que a app consiga ler.", await naoExcel.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ExcelDasBoquilhas_Atualizar_EscreveOntem_EDizQueDiasAtualizou()
    {
        // Diogo, 22/10/2026: «dia anterior está igual ao que a app tem? Se
        // sim não atualizamos, acabou. Se não, atualiza …». O botão está no
        // cartão; o handler devolve o livro e a mensagem com os dias.
        // O «hoje» do Excel fixo: 11/01/2030, o «ontem» é 10/01 (no «Janeiro»).
        var clock = new ExcelFixedClock(new DateTime(2030, 1, 11, 10, 0, 0, DateTimeKind.Utc));
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddKeyedSingleton<TimeProvider>(DMO.Alpha.Web.Pages.Boquilhas.DefinicoesModel.RelogioDoExcel, clock)));
        using var client = await LoginBoquilhasAsync(app, "4744");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client));
        Assert.Contains("data-bq-excel-atualizar data-bq-excel-ligado hidden>Atualizar</button>", html);
        Assert.DoesNotContain("Atualizar o Excel agora", html);

        var ontem = new DateTime(2030, 1, 10);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "BQ-EXCEL-ATU", Type = ToolType.Bq, Reference = "T201", Lot = "7" });
            var trace = new BqRepairTrace { ToolId = "BQ-EXCEL-ATU" };
            db.BqRepairTraces.Add(trace);
            await db.SaveChangesAsync();
            db.BqMovements.Add(new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Saida,
                Quantity = 5,
                MachineCodeFrozen = "C3",
                OccurredAtUtc = DmoBusinessTime.ToUtc(ontem.AddHours(12)),
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetPageHtmlAsync(client))!;
        var ficheiro = new ByteArrayContent(LivroComJaneiro(2030));
        ficheiro.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var corpo = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { ficheiro, "livro", "Saida Entrada BQ.xlsx" }
        };
        using var resposta = await client.PostAsync($"{PagePath}?handler=ExcelAtualizar", corpo);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resposta.Content.Headers.ContentType?.MediaType);
        var mensagem = Uri.UnescapeDataString(resposta.Headers.GetValues("X-Dmo-Excel-Mensagem").Single());
        Assert.Contains("2030-01-10", resposta.Headers.GetValues("X-Dmo-Excel-Atualizados").Single());
        Assert.Contains("10/01", mensagem);
        Assert.StartsWith("Atualizad", mensagem);

        // O livro devolvido tem o dia 10 escrito na C3 (Ref em V, Sai em W).
        using var zip = new ZipArchive(new MemoryStream(await resposta.Content.ReadAsByteArrayAsync()));
        var folha = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).Root!;
        var linha = 4 + 5 * (10 - 1);
        var refCelula = folha.Descendants(Sml + "c").Single(c => (string)c.Attribute("r")! == $"V{linha}");
        Assert.Equal("T201/7", string.Concat(refCelula.Descendants(Sml + "t").Select(t => t.Value)));
        Assert.Equal("5", folha.Descendants(Sml + "c").Single(c => (string)c.Attribute("r")! == $"W{linha}").Element(Sml + "v")!.Value);
    }

    private sealed class ExcelFixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private static async Task<string> VersaoAsync(HttpClient client)
    {
        using var resposta = await client.GetAsync($"{PagePath}?handler=ExcelVersao");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("versao").GetString()!;
    }

    private async Task<HttpResponseMessage> PostExcelAsync(HttpClient client, byte[]? livro, string desde)
    {
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetPageHtmlAsync(client))!;
        var corpo = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(desde), "desde" }
        };
        if (livro is not null)
        {
            var ficheiro = new ByteArrayContent(livro);
            ficheiro.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            corpo.Add(ficheiro, "livro", "Saida Entrada BQ.xlsx");
        }

        return await client.PostAsync($"{PagePath}?handler=Excel", corpo);
    }

    /// <summary>Um livro com o separador «Janeiro» no formato do Excel do Diogo (máquinas na linha 1, 31 dias de 5 linhas).</summary>
    private static byte[] LivroComJaneiro(int ano)
    {
        var linhas = new StringBuilder("<row r=\"1\">");
        string[] maquinas = ["B1", "B2", "B3", "C1", "C2", "C3"];
        string[] colunas = ["B", "F", "J", "N", "R", "V"];
        for (var m = 0; m < 6; m++) linhas.Append($"<c r=\"{colunas[m]}1\" t=\"inlineStr\"><is><t>{maquinas[m]}</t></is></c>");
        linhas.Append("</row>");
        for (var dia = 1; dia <= 31; dia++)
        {
            var r = 4 + 5 * (dia - 1);
            linhas.Append(dia == 1 ? $"<row r=\"4\"><c r=\"A4\"><f>DATE({ano},1,1)</f></c></row>" : $"<row r=\"{r}\"><c r=\"A{r}\"><f>A{r - 5}+1</f></c></row>");
        }

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Parte(string caminho, string xml)
            {
                using var escrita = new StreamWriter(zip.CreateEntry(caminho).Open(), new UTF8Encoding(false));
                escrita.Write(xml);
            }

            Parte("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Parte("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Parte("xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Janeiro\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Parte("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Parte("xl/worksheets/sheet1.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" + linhas + "</sheetData></worksheet>");
        }

        return memoria.ToArray();
    }
}
