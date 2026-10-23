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

    /// <summary>O email que envia (o «De»); vazio = a conta do Outlook do PC.</summary>
    public string? Remetente { get; set; }

    public string Assunto { get; set; } = "";

    public string Corpo { get; set; } = "";

    /// <summary>A lista proposta ao enviar com este modelo; null = nenhuma.</summary>
    public int? ListaId { get; set; }
}

/// <summary>
/// O registo de um envio do PDF do Peso: quem preparou o email, quando, para
/// quem e com que modelo. O email sai pelo Outlook do PC (Diogo, 21/10/2026:
/// «é enviado pelo outlook do pc da empresa»), por isso a app sabe que o
/// preparou, não que saiu. O PDF não fica no servidor (regra do Diogo, 19/10/2026): fica
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

/// <summary>Uma mensagem com o PDF em anexo, para abrir no Outlook do PC.</summary>
public sealed record EmailMensagem(
    string? Remetente,
    IReadOnlyList<string> Destinatarios,
    string Assunto,
    string Corpo,
    string AnexoNome,
    byte[] Anexo);

/// <summary>Uma pessoa de uma lista: o nome (opcional; as listas de antes não o têm) e o email.</summary>
public sealed record EmailPessoa(string? Nome, string Email);

/// <summary>As regras do texto dos modelos e das listas, num só sítio.</summary>
public static class EnvioEmailRegras
{
    /// <summary>A versão do conteúdo que a aba leu; não depende da posição da pessoa.</summary>
    public static string Versao(EmailLista lista) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"{lista.Id}\0{lista.Nome}\0{lista.Destinatarios}")));

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

    /// <summary>
    /// As pessoas de um texto livre, também colado do Outlook (Diogo, 23/10/2026):
    /// «"Leal, Sergio" &lt;sleal@x.pt&gt;; Carlos Marinho &lt;cm@x.pt&gt;; simples@x.pt».
    /// Separa em «;», «,» e linhas novas fora de aspas e de &lt;…&gt;; uma parte sem
    /// &lt;…&gt; separa-se também nos espaços (cada pedaço um email, como antes).
    /// Devolve as válidas, sem emails repetidos e por ordem, e o texto das que
    /// não são um email.
    /// </summary>
    public static (IReadOnlyList<EmailPessoa> Validas, IReadOnlyList<string> Invalidas) Pessoas(string? texto)
    {
        var validas = new List<EmailPessoa>();
        var invalidas = new List<string>();
        void Juntar(EmailPessoa pessoa)
        {
            if (!validas.Any(v => string.Equals(v.Email, pessoa.Email, StringComparison.OrdinalIgnoreCase))) validas.Add(pessoa);
        }

        foreach (var parte in Partes(texto ?? ""))
        {
            var abre = parte.LastIndexOf('<');
            if (abre >= 0)
            {
                var fecha = parte.IndexOf('>', abre);
                var email = (fecha > abre ? parte[(abre + 1)..fecha] : parte[(abre + 1)..]).Trim();
                var nome = LimparNome(parte[..abre]);
                if (EmailValido(email) && (fecha < 0 || parte[(fecha + 1)..].Trim().Length == 0)) Juntar(new EmailPessoa(nome, email));
                else invalidas.Add(parte.Trim());
                continue;
            }

            if (parte.Contains('"'))
            {
                invalidas.Add(parte.Trim());
                continue;
            }

            foreach (var pedaco in parte.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (EmailValido(pedaco)) Juntar(new EmailPessoa(null, pedaco));
                else invalidas.Add(pedaco);
            }
        }

        return (validas, invalidas);
    }

    /// <summary>Uma linha gravada da lista: «Nome &lt;email&gt;» ou só «email» (as listas de antes).</summary>
    public static EmailPessoa LerLinha(string linha)
    {
        var abre = linha.LastIndexOf('<');
        var fecha = linha.LastIndexOf('>');
        return abre >= 0 && fecha > abre
            ? new EmailPessoa(LimparNome(linha[..abre]), linha[(abre + 1)..fecha].Trim())
            : new EmailPessoa(null, linha.Trim());
    }

    /// <summary>As pessoas gravadas numa lista (uma por linha).</summary>
    public static IReadOnlyList<EmailPessoa> LerLista(string? destinatarios) =>
        (destinatarios ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(LerLinha).ToList();

    /// <summary>Como se grava uma pessoa: «Nome &lt;email&gt;», ou só o email sem nome.</summary>
    public static string Linha(EmailPessoa pessoa) =>
        string.IsNullOrEmpty(pessoa.Nome) ? pessoa.Email : $"{pessoa.Nome} <{pessoa.Email}>";

    /// <summary>
    /// As pessoas num texto que se volta a ler com <see cref="Pessoas"/>: o nome
    /// entre aspas quando tem vírgula ou ponto e vírgula («"Leal, Sergio" &lt;…&gt;»).
    /// </summary>
    public static string Texto(IEnumerable<EmailPessoa> pessoas) =>
        string.Join(", ", pessoas.Select(p => string.IsNullOrEmpty(p.Nome) ? p.Email
            : p.Nome.IndexOfAny([',', ';']) >= 0 ? $"\"{p.Nome}\" <{p.Email}>" : $"{p.Nome} <{p.Email}>"));

    /// <summary>O nome limpo: sem aspas, sem &lt; &gt;, sem quebras, até <see cref="NomeMax"/>; vazio = null.</summary>
    public static string? LimparNome(string? nome)
    {
        var limpo = new string((nome ?? "").Where(c => !char.IsControl(c) && c is not ('"' or '<' or '>')).ToArray()).Trim();
        if (limpo.Length > NomeMax) limpo = limpo[..NomeMax].Trim();
        return limpo.Length == 0 ? null : limpo;
    }

    private static IEnumerable<string> Partes(string texto)
    {
        var atual = new System.Text.StringBuilder();
        var aspas = false;
        var angulo = false;
        foreach (var c in texto)
        {
            if (c == '"' && !angulo) aspas = !aspas;
            else if (c == '<' && !aspas) angulo = true;
            else if (c == '>' && !aspas) angulo = false;
            if (!aspas && !angulo && c is ';' or ',' or '\n' or '\r')
            {
                if (atual.ToString().Trim().Length > 0) yield return atual.ToString();
                atual.Clear();
                continue;
            }

            atual.Append(c);
        }

        if (atual.ToString().Trim().Length > 0) yield return atual.ToString();
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
    /// {referencia}, {producao}, {maquina} e {peso} (Diogo, 21/10/2026: «no
    /// assunto meto Peso Referencia Maquina Producao e o peso que deu»).
    /// </summary>
    public static string Preencher(string texto, string referencia, string producao, string maquina, decimal? pesoGramas = null) =>
        texto.Replace("{referencia}", referencia, StringComparison.OrdinalIgnoreCase)
            .Replace("{producao}", producao, StringComparison.OrdinalIgnoreCase)
            .Replace("{maquina}", maquina, StringComparison.OrdinalIgnoreCase)
            .Replace("{peso}", FormatarPeso(pesoGramas), StringComparison.OrdinalIgnoreCase);

    /// <summary>O peso médio do vidro, como na folha: «245,30 g»; sem leituras, «—».</summary>
    public static string FormatarPeso(decimal? gramas) =>
        gramas is { } g ? g.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")) + " g" : "—";
}
