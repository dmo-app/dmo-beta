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

    public async Task<IReadOnlyList<Ficheiro>> FicheirosAsync(CancellationToken cancellationToken = default)
    {
        var dados = await definicoes.LerAsync(cancellationToken);
        var senha = await definicoes.SenhaAsync(cancellationToken) ?? string.Empty;
        var ficheiros = new List<Ficheiro>();
        foreach (var nome in SapScriptsRegras.Nomes)
        {
            var texto = Preencher(await TextoAsync(nome, cancellationToken), dados, senha).Replace("\n", "\r\n");
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
