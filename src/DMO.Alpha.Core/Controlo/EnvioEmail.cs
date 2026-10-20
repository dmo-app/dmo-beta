using System.Net.Mail;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Uma lista de destinatários do envio por email (Diogo, 19/10/2026, D9:
/// «criar listas de email para depois associar ao template»). Os endereços
/// ficam num só texto, um por linha, já validados e sem repetidos.
/// </summary>
public sealed class EmailLista
{
    public int Id { get; set; }

    public string Nome { get; set; } = "";

    public string Destinatarios { get; set; } = "";
}

/// <summary>
/// Um modelo de envio (D9: «um template para o envio: associar o email que
/// vai enviar, quem recebe»; «o template deixa criar um assunto e o corpo do
/// email reutilizáveis»). A lista do modelo é só a escolhida por omissão: ao
/// enviar escolhe-se a lista.
/// </summary>
public sealed class EmailModelo
{
    public int Id { get; set; }

    public string Nome { get; set; } = "";

    /// <summary>O email que envia (o «De»); vazio = o da conta do servidor.</summary>
    public string? Remetente { get; set; }

    public string Assunto { get; set; } = "";

    public string Corpo { get; set; } = "";

    /// <summary>A lista proposta ao enviar com este modelo; null = nenhuma.</summary>
    public int? ListaId { get; set; }
}

/// <summary>
/// O registo de um envio do PDF do Peso: quem enviou, quando, para quem e com
/// que modelo. O PDF não fica no servidor (regra do Diogo, 19/10/2026): fica
/// só o nome do ficheiro. Os nomes do modelo e da lista ficam copiados, para
/// o registo sobreviver a mudá-los ou apagá-los.
/// </summary>
public sealed class EmailEnvio
{
    public int Id { get; set; }

    public int? ControloId { get; set; }

    public int PesoId { get; set; }

    public string Ficheiro { get; set; } = "";

    public string ModeloNome { get; set; } = "";

    public string ListaNome { get; set; } = "";

    public string? Remetente { get; set; }

    public string Destinatarios { get; set; } = "";

    public string Assunto { get; set; } = "";

    public string EnviadoPor { get; set; } = "";


    /// <summary>O utilizador de <see cref="EnviadoPor"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>

    public Guid? EnviadoPorUserId { get; set; }

    public DateTime EnviadoEmUtc { get; set; }
}

/// <summary>Uma mensagem para o servidor de email, com o PDF em anexo.</summary>
public sealed record EmailMensagem(
    string? Remetente,
    IReadOnlyList<string> Destinatarios,
    string Assunto,
    string Corpo,
    string AnexoNome,
    byte[] Anexo);

/// <summary>
/// O envio de email da app. Sem servidor configurado, <see cref="Configurado"/>
/// é false e nada sai.
/// </summary>
public interface IEmailSender
{
    bool Configurado { get; }

    Task SendAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default);
}

/// <summary>As regras do texto dos modelos e das listas, num só sítio.</summary>
public static class EnvioEmailRegras
{
    public const int NomeMax = 100;
    public const int AssuntoMax = 200;
    public const int CorpoMax = 4000;
    public const int EmailMax = 254;
    public const int DestinatariosMax = 50;

    /// <summary>
    /// Os endereços de um texto livre (vírgula, ponto e vírgula, espaço ou
    /// linha nova entre eles): os válidos, sem repetidos e por ordem, e os
    /// que não são um email.
    /// </summary>
    public static (IReadOnlyList<string> Validos, IReadOnlyList<string> Invalidos) Enderecos(string? texto)
    {
        var validos = new List<string>();
        var invalidos = new List<string>();
        foreach (var parte in (texto ?? "").Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (EmailValido(parte))
            {
                if (!validos.Contains(parte, StringComparer.OrdinalIgnoreCase)) validos.Add(parte);
            }
            else
            {
                invalidos.Add(parte);
            }
        }

        return (validos, invalidos);
    }

    /// <summary>Um só endereço, sem nome à frente (ex. «producao@fabrica.pt»).</summary>
    public static bool EmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Length <= EmailMax
        && MailAddress.TryCreate(email, out var endereco)
        && endereco.Address == email
        && endereco.Host.Contains('.');

    /// <summary>
    /// O assunto ou o corpo do modelo com os dados da produção:
    /// {referencia}, {producao} e {maquina}.
    /// </summary>
    public static string Preencher(string texto, string referencia, string producao, string maquina) =>
        texto.Replace("{referencia}", referencia, StringComparison.OrdinalIgnoreCase)
            .Replace("{producao}", producao, StringComparison.OrdinalIgnoreCase)
            .Replace("{maquina}", maquina, StringComparison.OrdinalIgnoreCase);
}
