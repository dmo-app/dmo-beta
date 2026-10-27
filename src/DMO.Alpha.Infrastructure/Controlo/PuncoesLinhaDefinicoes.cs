using System.Globalization;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// As secções de cada linha para as punções PU NNPB (Diogo, 22/10/2026),
/// nas Definições do Controlo. Guardam-se nas ModuleSettings
/// («Controlo:PU:&lt;máquina&gt;»), sem tabela nova; sem valor vale
/// <see cref="PuncoesPosicoes.SeccoesPorOmissao"/>. Cada secção tem uma
/// posição por gota (A, B, C…); as gotas guardam-se em
/// «Controlo:PU:Gotas:&lt;máquina&gt;» (Diogo, 27/10/2026: «B1 tem 4 gotas,
/// C3 tem 3»; sem valor, a C3 vale 3 e as outras 4).
/// </summary>
public sealed class PuncoesLinhaDefinicoes(DmoDbContext db)
{
    public const string Prefixo = "Controlo:PU:";

    public const string PrefixoGotas = Prefixo + "Gotas:";

    public static string Chave(string maquina) => Prefixo + maquina;

    public static string ChaveGotas(string maquina) => PrefixoGotas + maquina;

    /// <summary>As gotas de uma linha sem valor guardado: a C3 tem 3, as outras 4 (Diogo, 27/10/2026).</summary>
    public static int GotasPorOmissao(string? maquina) =>
        string.Equals(maquina?.Trim(), "C3", StringComparison.OrdinalIgnoreCase) ? 3 : PuncoesPosicoes.PosicoesPorSeccao;

    /// <summary>As secções da linha; o valor por omissão sem linha ou sem valor guardado.</summary>
    public async Task<int> SeccoesAsync(string? maquina, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(maquina))
        {
            return PuncoesPosicoes.SeccoesPorOmissao;
        }

        var chave = Chave(maquina);
        var valor = await db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == chave)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return Ler(valor) ?? PuncoesPosicoes.SeccoesPorOmissao;
    }

    /// <summary>As secções e as gotas da linha; os valores por omissão sem linha ou sem valor guardado.</summary>
    public async Task<PuncoesLinha> LinhaAsync(string? maquina, CancellationToken cancellationToken = default)
    {
        var codigo = maquina?.Trim() ?? string.Empty;
        if (codigo.Length == 0)
        {
            return Montar(codigo, new Dictionary<string, string?>());
        }

        var chaves = new[] { Chave(codigo), ChaveGotas(codigo) };
        var guardadas = await db.ModuleSettings.AsNoTracking()
            .Where(s => chaves.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, s => (string?)s.SettingValue, cancellationToken);
        return Montar(codigo, guardadas);
    }

    /// <summary>Todas as linhas (máquinas) com as suas secções e gotas, pelo código.</summary>
    public async Task<IReadOnlyList<PuncoesLinha>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var maquinas = await db.Machines.AsNoTracking().OrderBy(m => m.Code).Select(m => m.Code).ToListAsync(cancellationToken);
        var guardadas = await db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey.StartsWith(Prefixo))
            .ToDictionaryAsync(s => s.SettingKey, s => (string?)s.SettingValue, cancellationToken);
        return maquinas.Select(m => Montar(m, guardadas)).ToArray();
    }

    private static PuncoesLinha Montar(string maquina, IReadOnlyDictionary<string, string?> guardadas)
    {
        var seccoes = guardadas.TryGetValue(Chave(maquina), out var v) ? Ler(v) : null;
        var gotas = guardadas.TryGetValue(ChaveGotas(maquina), out var g) ? LerGotas(g) : null;
        return new PuncoesLinha(
            maquina,
            seccoes ?? PuncoesPosicoes.SeccoesPorOmissao,
            seccoes is null,
            gotas ?? GotasPorOmissao(maquina),
            gotas is null);
    }

    /// <summary>Grava as secções de uma linha; recusa o que não é um inteiro entre o mínimo e o máximo.</summary>
    public async Task<PuncoesSeccoesGravacao> GuardarAsync(string? maquina, string? texto, CancellationToken cancellationToken = default)
    {
        if (Ler(texto) is not int seccoes)
        {
            return PuncoesSeccoesGravacao.Invalido;
        }

        var codigo = maquina?.Trim();
        if (string.IsNullOrEmpty(codigo) || !await db.Machines.AnyAsync(m => m.Code == codigo, cancellationToken))
        {
            return PuncoesSeccoesGravacao.LinhaNaoExiste;
        }

        var chave = Chave(codigo);
        var valor = seccoes.ToString(CultureInfo.InvariantCulture);
        var existente = await db.ModuleSettings.FirstOrDefaultAsync(s => s.SettingKey == chave, cancellationToken);
        if (existente is null)
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = chave, SettingValue = valor });
        }
        else
        {
            existente.SettingValue = valor;
        }

        await db.SaveChangesAsync(cancellationToken);
        return PuncoesSeccoesGravacao.Gravado;
    }

    /// <summary>Grava as gotas de uma linha; recusa o que não é um inteiro de 1 a 4.</summary>
    public async Task<PuncoesSeccoesGravacao> GuardarGotasAsync(string? maquina, string? texto, CancellationToken cancellationToken = default)
    {
        if (LerGotas(texto) is not int gotas)
        {
            return PuncoesSeccoesGravacao.GotasInvalidas;
        }

        var codigo = maquina?.Trim();
        if (string.IsNullOrEmpty(codigo) || !await db.Machines.AnyAsync(m => m.Code == codigo, cancellationToken))
        {
            return PuncoesSeccoesGravacao.LinhaNaoExiste;
        }

        var chave = ChaveGotas(codigo);
        var valor = gotas.ToString(CultureInfo.InvariantCulture);
        var existente = await db.ModuleSettings.FirstOrDefaultAsync(s => s.SettingKey == chave, cancellationToken);
        if (existente is null)
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = chave, SettingValue = valor });
        }
        else
        {
            existente.SettingValue = valor;
        }

        await db.SaveChangesAsync(cancellationToken);
        return PuncoesSeccoesGravacao.Gravado;
    }

    private static int? LerGotas(string? texto) =>
        int.TryParse(texto?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
        && n >= PuncoesPosicoes.GotasMinimo && n <= PuncoesPosicoes.GotasMaximo
            ? n
            : null;

    private static int? Ler(string? texto) =>
        int.TryParse(texto?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
        && n >= PuncoesPosicoes.SeccoesMinimo && n <= PuncoesPosicoes.SeccoesMaximo
            ? n
            : null;
}

/// <summary>Uma linha com as secções e as gotas; Punções = secções × gotas.</summary>
public sealed record PuncoesLinha(string Maquina, int Seccoes, bool PorOmissao, int Gotas, bool GotasPorOmissao)
{
    public int Puncoes => Seccoes * Gotas;
}

public enum PuncoesSeccoesGravacao
{
    Gravado = 0,
    Invalido,
    LinhaNaoExiste,
    GotasInvalidas
}
