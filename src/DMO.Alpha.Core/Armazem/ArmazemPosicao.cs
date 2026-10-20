namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// Uma posição do Armazém (plano do full, peça E; blueprint armazem/OVERVIEW §3):
/// um código plano, sem zonas, corredores nem paletes. Não se apaga nem muda de
/// código; arquiva-se e sai das escolhas novas, e reativa-se.
/// <para>
/// O código aceita as três formas das bases Access (1001…, 7001a…, A001…;
/// recomendação da AR-03, perguntas-full Q9): letras e algarismos, guardado em
/// maiúsculas, único. A app full antiga só aceitava quatro algarismos.
/// </para>
/// </summary>
public sealed class ArmazemPosicao
{
    /// <summary>O tamanho máximo do código.</summary>
    public const int CodigoMaxLength = 16;

    public int Id { get; set; }

    /// <summary>O código, em maiúsculas (ex.: «0042», «7001A», «A001»).</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Ativa por omissão; arquivada sai das escolhas novas.</summary>
    public bool Ativa { get; set; } = true;

    /// <summary>Quando a posição foi criada (UTC).</summary>
    public DateTime CriadaEmUtc { get; set; }

    /// <summary>Quem criou a posição (o nome, senão o nº de operador, do momento).</summary>
    public string? CriadaPor { get; set; }

    /// <summary>O utilizador de <see cref="CriadaPor"/> (<c>Users.Id</c>); NULL no ADMIN e quando o utilizador foi eliminado.</summary>
    public Guid? CriadaPorUserId { get; set; }
}

/// <summary>O código de uma posição: letras e algarismos, sem espaços, até 16, em maiúsculas.</summary>
public static class PosicaoCodigo
{
    /// <summary>
    /// true quando o texto, sem os espaços das pontas, tem só letras (A–Z) e
    /// algarismos, até <see cref="ArmazemPosicao.CodigoMaxLength"/>; o código sai em maiúsculas.
    /// </summary>
    public static bool TryParse(string? texto, out string codigo)
    {
        codigo = (texto ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length is 0 or > ArmazemPosicao.CodigoMaxLength || !codigo.All(c => c is (>= '0' and <= '9') or (>= 'A' and <= 'Z')))
        {
            codigo = string.Empty;
            return false;
        }

        return true;
    }
}

/// <summary>Uma posição da lista: o código e se está ativa.</summary>
public sealed record ArmazemPosicaoItem(int Id, string Codigo, bool Ativa);

/// <summary>Como acabou criar, arquivar ou reativar uma posição.</summary>
public enum ArmazemPosicaoResultado
{
    Gravado,
    CodigoInvalido,
    JaExiste,
    NaoExiste
}

/// <summary>Os filtros da lista das posições.</summary>
public enum ArmazemPosicaoFiltro
{
    Todas,
    Ativas,
    Arquivadas
}

/// <summary>As posições do Armazém: listar, criar, arquivar e reativar.</summary>
public interface IArmazemPosicoes
{
    /// <summary>Uma página das posições cujo código começa pelo texto, pela ordem do código.</summary>
    Task<DMO.Alpha.Core.Paging.PagedList<ArmazemPosicaoItem>> ListarAsync(
        string? comecaPor, ArmazemPosicaoFiltro filtro, int pagina, CancellationToken cancellationToken = default);

    /// <summary>Cria a posição. O mesmo código outra vez (também com outras maiúsculas) é <see cref="ArmazemPosicaoResultado.JaExiste"/>.</summary>
    Task<(ArmazemPosicaoResultado Resultado, string Codigo)> CriarAsync(string? codigo, CancellationToken cancellationToken = default);

    /// <summary>Arquiva ou reativa. Repetir o mesmo pedido não muda nada e é <see cref="ArmazemPosicaoResultado.Gravado"/>.</summary>
    Task<ArmazemPosicaoResultado> MudarAtivaAsync(int id, bool ativa, CancellationToken cancellationToken = default);
}
