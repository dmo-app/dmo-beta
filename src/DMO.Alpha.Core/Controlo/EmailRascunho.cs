using System.Text;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O email pronto para o Outlook do PC (Diogo, 21/10/2026: «é enviado pelo
/// outlook do pc da empresa»): um ficheiro .eml com X-Unsent: 1, que o Outlook
/// abre como uma mensagem nova, com os destinatários, o assunto, o corpo e o
/// PDF em anexo; quem envia carrega em «Enviar» no Outlook. A app não precisa
/// de conta de email nem de palavra-passe.
/// </summary>
public static class EmailRascunho
{
    public static byte[] Gerar(EmailMensagem mensagem)
    {
        var fronteira = "dmo-" + Guid.NewGuid().ToString("N");
        var eml = new StringBuilder();
        eml.Append("X-Unsent: 1\r\n");
        if (mensagem.Remetente is { Length: > 0 } remetente)
        {
            eml.Append("From: ").Append(remetente).Append("\r\n");
        }

        eml.Append("To: ").Append(string.Join(", ", mensagem.Destinatarios)).Append("\r\n");
        eml.Append("Subject: ").Append(Cabecalho(mensagem.Assunto)).Append("\r\n");
        eml.Append("MIME-Version: 1.0\r\n");
        eml.Append("Content-Type: multipart/mixed; boundary=\"").Append(fronteira).Append("\"\r\n\r\n");

        eml.Append("--").Append(fronteira).Append("\r\n");
        eml.Append("Content-Type: text/plain; charset=utf-8\r\n");
        eml.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        eml.Append(Base64(Encoding.UTF8.GetBytes(mensagem.Corpo.Replace("\r\n", "\n").Replace("\n", "\r\n"))));

        var nome = Cabecalho(mensagem.AnexoNome.Replace("\"", ""));
        eml.Append("--").Append(fronteira).Append("\r\n");
        eml.Append("Content-Type: application/pdf; name=\"").Append(nome).Append("\"\r\n");
        eml.Append("Content-Disposition: attachment; filename=\"").Append(nome).Append("\"\r\n");
        eml.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        eml.Append(Base64(mensagem.Anexo));
        eml.Append("--").Append(fronteira).Append("--\r\n");
        return Encoding.ASCII.GetBytes(eml.ToString());
    }

    /// <summary>Um texto de cabeçalho: tal como está se é ASCII, senão em UTF-8 (RFC 2047).</summary>
    private static string Cabecalho(string texto)
    {
        var limpo = texto.Replace("\r", " ").Replace("\n", " ");
        return limpo.All(c => c >= ' ' && c < 127)
            ? limpo
            : "=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(limpo)) + "?=";
    }

    /// <summary>Base64 em linhas de 76 caracteres, como pede o MIME.</summary>
    private static string Base64(byte[] bytes)
    {
        var texto = Convert.ToBase64String(bytes);
        var linhas = new StringBuilder();
        for (var i = 0; i < texto.Length; i += 76)
        {
            linhas.Append(texto, i, Math.Min(76, texto.Length - i)).Append("\r\n");
        }

        return linhas.ToString();
    }
}
