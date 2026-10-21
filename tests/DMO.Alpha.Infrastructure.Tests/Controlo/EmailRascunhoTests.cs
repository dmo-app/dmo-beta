using System.Text;
using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>O .eml que o Outlook do PC abre pronto a enviar (Diogo, 21/10/2026).</summary>
public sealed class EmailRascunhoTests
{
    private static string Gerar(string? remetente, string assunto, string anexo) =>
        Encoding.ASCII.GetString(EmailRascunho.Gerar(new EmailMensagem(
            remetente, new[] { "a@fabrica.pt", "b@fabrica.pt" }, assunto, "Olá", anexo, new byte[] { 1, 2, 3 })));

    [Fact]
    public void Gerar_AbreComoMensagemNova_SemFrom_QuandoOModeloNaoTemRemetente()
    {
        var eml = Gerar(null, "Peso 202605", "PESO-1.pdf");

        Assert.StartsWith("X-Unsent: 1\r\n", eml);
        Assert.DoesNotContain("From:", eml);
        Assert.Contains("\r\nTo: a@fabrica.pt, b@fabrica.pt\r\n", eml);
        Assert.Contains("\r\nSubject: Peso 202605\r\n", eml);
        Assert.Contains("filename=\"PESO-1.pdf\"", eml);
        Assert.Contains(Convert.ToBase64String(new byte[] { 1, 2, 3 }), eml);
        Assert.EndsWith("--\r\n", eml);
    }

    [Fact]
    public void Gerar_AcentosNoAssuntoENoAnexo_VaoEmUtf8_EUmaLinhaNovaNaoPartiOCabecalho()
    {
        var eml = Gerar("controlo@fabrica.pt", "Produção\r\nBcc: x@y.pt", "PESO-Ação.pdf");

        Assert.Contains("\r\nFrom: controlo@fabrica.pt\r\n", eml);
        Assert.DoesNotContain("\r\nBcc:", eml);
        Assert.Contains("Subject: =?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Produção  Bcc: x@y.pt")) + "?=", eml);
        Assert.Contains("filename=\"=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("PESO-Ação.pdf")) + "?=\"", eml);
        Assert.True(eml.All(c => c < 128));
    }
}
