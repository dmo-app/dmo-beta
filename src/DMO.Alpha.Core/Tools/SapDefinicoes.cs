namespace DMO.Alpha.Core.Tools;

/// <summary>
/// A LEITURA DO SAP DENTRO DA APP (Diogo, 28/10/2026, «meter o script dentro
/// da app», «um actualizar deve ir tudo ao sap e meter nas ferramentas»): as
/// definições do Admin › SAP. Os scripts vivem na app e a app escreve-os na
/// pasta ligada do PC do SAP com estes valores lá dentro; o utilizador e a
/// senha do SAP, a hora da leitura diária e quantos dias de produções se leem.
/// </summary>
public sealed record SapDefinicoesDados(string Utilizador, bool TemSenha, int Hora, int Dias);

public static class SapDefinicoesRegras
{
    public const string ChaveUtilizador = "Sap:Utilizador";
    public const string ChaveSenha = "Sap:Senha";
    public const string ChaveHora = "Sap:Hora";
    public const string ChaveDias = "Sap:Dias";

    /// <summary>O prefixo do texto de um script editado no Admin (Sap:Script:DMO_USO_SAP.vbs).</summary>
    public const string ChaveScript = "Sap:Script:";

    /// <summary>A chave com que o script do SAP envia o que leu (cifrada, DMO-582).</summary>
    public const string ChaveEnvio = "Sap:ChaveEnvio";

    /// <summary>O utilizador do «Moldes Existentes.SAP» (Diogo, 28/10/2026).</summary>
    public const string UtilizadorPorOmissao = "USERREQMG";

    public const int HoraPorOmissao = 18;

    /// <summary>As produções dos últimos 120 dias (a ZFMD, desde 26/10/2026).</summary>
    public const int DiasPorOmissao = 120;

    public const int DiasMinimo = 1;

    public const int DiasMaximo = 9999;

    public const int UtilizadorMaximo = 40;

    public const int SenhaMaxima = 40;

    /// <summary>O maior texto de um script editado (os da app têm ~25 KB).</summary>
    public const int ScriptMaximo = 200_000;

    public static bool HoraValida(int hora) => hora is >= 0 and <= 23;

    public static bool DiasValidos(int dias) => dias is >= DiasMinimo and <= DiasMaximo;
}

public enum SapGravacao
{
    Gravado,
    UtilizadorInvalido,
    SenhaInvalida,
    HoraInvalida,
    DiasInvalidos,
    ScriptDesconhecido,
    ScriptGrande
}

/// <summary>Uma leitura do SAP gravada na app: a % uso (ZPPG) ou as produções (ZFMD).</summary>
public sealed record SapLeituraFeita(DateTime LidoEmUtc, string Tipo, string? Ficheiro, int Linhas);

public interface ISapDefinicoes
{
    Task<SapDefinicoesDados> LerAsync(CancellationToken cancellationToken = default);

    /// <summary>A senha do SAP; null quando ainda não foi escrita no Admin.</summary>
    Task<string?> SenhaAsync(CancellationToken cancellationToken = default);

    /// <summary>Grava as definições; <paramref name="senhaNova"/> vazia mantém a senha guardada.</summary>
    Task<SapGravacao> GravarAsync(string? utilizador, string? senhaNova, int hora, int dias, CancellationToken cancellationToken = default);

    /// <summary>O texto do script mudado no Admin; null quando é o da app.</summary>
    Task<string?> ScriptEditadoAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>Grava o texto de um script; null ou vazio volta ao da app.</summary>
    Task<SapGravacao> GravarScriptAsync(string nome, string? texto, CancellationToken cancellationToken = default);

    /// <summary>
    /// A chave do envio direto (o script do SAP manda os ficheiros para
    /// /sap/enviar com ela, sem ninguém abrir a app; DMO-582). Criada na
    /// primeira vez.
    /// </summary>
    Task<string> ChaveDeEnvioAsync(CancellationToken cancellationToken = default);

    /// <summary>TRUE quando <paramref name="chave"/> é a chave do envio.</summary>
    Task<bool> ConfereChaveAsync(string? chave, CancellationToken cancellationToken = default);

    /// <summary>As últimas leituras gravadas (a % uso e as produções), as mais recentes primeiro.</summary>
    Task<IReadOnlyList<SapLeituraFeita>> LeiturasAsync(int maximo, CancellationToken cancellationToken = default);
}

/// <summary>Os ficheiros que a app escreve na pasta do SAP.</summary>
public static class SapScriptsRegras
{
    public const string Diario = "DMO_SAP_DIARIO.vbs";
    public const string Fabrico = "DMO_FABRICO_SAP.vbs";
    public const string Uso = "DMO_USO_SAP.vbs";
    public const string Instalar = "INSTALAR.cmd";
    public const string Atalho = "Moldes Existentes.SAP";

    public static readonly IReadOnlyList<string> Nomes = [Diario, Fabrico, Uso, Instalar, Atalho];

    public static bool Conhecido(string? nome) => nome is not null && Nomes.Contains(nome);

    /// <summary>Os que se abrem e mudam no Admin › SAP: todos menos o INSTALAR.cmd, que só instala.</summary>
    public static bool Editavel(string? nome) => Conhecido(nome) && nome != Instalar;
}
