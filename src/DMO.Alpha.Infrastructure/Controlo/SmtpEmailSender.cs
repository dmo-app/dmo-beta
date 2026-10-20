using System.Net;
using System.Net.Mail;
using DMO.Alpha.Core.Controlo;
using Microsoft.Extensions.Configuration;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O envio por SMTP (Outlook, Gmail ou o servidor da fábrica). A conta vem
/// só da configuração do serviço, nunca do código nem da base:
/// Dmo__Email__Host, Dmo__Email__Port (587 por omissão, com STARTTLS),
/// Dmo__Email__User, Dmo__Email__Password e Dmo__Email__From (o «De» quando
/// o modelo não tem remetente; por omissão o utilizador).
/// </summary>
public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    private string? Host => configuration["Dmo:Email:Host"];

    private string? User => configuration["Dmo:Email:User"];

    private string? From => configuration["Dmo:Email:From"] is { Length: > 0 } from ? from : User;

    public bool Configurado => !string.IsNullOrWhiteSpace(Host) && EnvioEmailRegras.EmailValido(From);

    public async Task SendAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default)
    {
        if (!Configurado)
        {
            throw new InvalidOperationException("O servidor de email não está configurado (Dmo__Email__Host).");
        }

        var port = int.TryParse(configuration["Dmo:Email:Port"], out var configured) ? configured : 587;
        using var client = new SmtpClient(Host, port)
        {
            EnableSsl = !string.Equals(configuration["Dmo:Email:Ssl"], "false", StringComparison.OrdinalIgnoreCase),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30_000
        };
        if (!string.IsNullOrEmpty(User))
        {
            client.Credentials = new NetworkCredential(User, configuration["Dmo:Email:Password"]);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(mensagem.Remetente is { Length: > 0 } remetente ? remetente : From!),
            Subject = mensagem.Assunto,
            Body = mensagem.Corpo,
            IsBodyHtml = false
        };
        foreach (var destinatario in mensagem.Destinatarios)
        {
            message.To.Add(destinatario);
        }

        using var anexo = new MemoryStream(mensagem.Anexo, writable: false);
        message.Attachments.Add(new Attachment(anexo, mensagem.AnexoNome, "application/pdf"));
        await client.SendMailAsync(message, cancellationToken);
    }
}
