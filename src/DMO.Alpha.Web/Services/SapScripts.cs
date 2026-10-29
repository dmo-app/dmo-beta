using System.Globalization;
using System.Text;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// OS SCRIPTS DO SAP DENTRO DA APP (Diogo, 28/10/2026, «em vez de alterar um
/// ficheiro fora altera-se o script já na app»): o texto vem da app (Sap/*) ou
/// do Admin › SAP quando foi mudado lá; os {{UTILIZADOR}}, {{SENHA}}, {{HORA}}
/// e {{DIAS}} levam as definições. O browser grava-os na pasta ligada.
/// </summary>
public sealed class SapScripts(ISapDefinicoes definicoes)
{
    public sealed record Ficheiro(string Nome, byte[] Conteudo);

    /// <summary>O texto da app, com os {{...}} por preencher.</summary>
    public static string Original(string nome)
    {
        using var stream = typeof(SapScripts).Assembly.GetManifestResourceStream("Sap." + nome)
            ?? throw new InvalidOperationException($"Falta o script {nome} na app.");
        using var leitor = new StreamReader(stream, Encoding.UTF8);
        return leitor.ReadToEnd().Replace("\r\n", "\n");
    }

    /// <summary>O texto a editar no Admin: o mudado lá ou o da app.</summary>
    public async Task<string> TextoAsync(string nome, CancellationToken cancellationToken = default) =>
        await definicoes.ScriptEditadoAsync(nome, cancellationToken) ?? Original(nome);

    /// <summary>Sem senha gravada no Admin › SAP os scripts não se dão: ficavam presos na janela da senha (DMO-661).</summary>
    public async Task<bool> TemSenhaAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrEmpty(await definicoes.SenhaAsync(cancellationToken));

    /// <param name="urlBase">O endereço da app como o browser a vê: o script envia para lá o que leu (DMO-582).</param>
    public async Task<IReadOnlyList<Ficheiro>> FicheirosAsync(string urlBase, CancellationToken cancellationToken = default)
    {
        var dados = await definicoes.LerAsync(cancellationToken);
        var senha = await definicoes.SenhaAsync(cancellationToken) ?? string.Empty;
        var chave = await definicoes.ChaveDeEnvioAsync(cancellationToken);
        var ficheiros = new List<Ficheiro>();
        foreach (var nome in SapScriptsRegras.Nomes)
        {
            var texto = Preencher(await TextoAsync(nome, cancellationToken), dados, senha)
                .Replace("{{URL}}", urlBase.Replace("\"", string.Empty), StringComparison.Ordinal)
                .Replace("{{CHAVE}}", chave, StringComparison.Ordinal)
                .Replace("\n", "\r\n");
            ficheiros.Add(new Ficheiro(nome, Codificar(nome, texto)));
        }

        return ficheiros;
    }

    public static string Preencher(string texto, SapDefinicoesDados dados, string senha) =>
        texto
            .Replace("{{UTILIZADOR}}", dados.Utilizador.Replace("\"", "\"\""), StringComparison.Ordinal)
            .Replace("{{SENHA}}", senha.Replace("\"", "\"\""), StringComparison.Ordinal)
            .Replace("{{HORA}}", dados.Hora.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{DIAS}}", dados.Dias.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// O texto de um ficheiro enviado pelo script: UTF-16 ou UTF-8 com BOM,
    /// UTF-8 sem BOM, ou o Windows-1252 do CreateTextFile do VBScript.
    /// </summary>
    public static string Texto(byte[] bytes)
    {
        if (bytes is [0xFF, 0xFE, ..])
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes is [0xEF, 0xBB, 0xBF, ..])
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>O .vbs em Unicode (o WScript lê os acentos), o resto em Windows-1252/ASCII.</summary>
    private static byte[] Codificar(string nome, string texto)
    {
        if (nome.EndsWith(".vbs", StringComparison.OrdinalIgnoreCase))
        {
            return [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(texto)];
        }

        return Encoding.Latin1.GetBytes(texto);
    }
}
