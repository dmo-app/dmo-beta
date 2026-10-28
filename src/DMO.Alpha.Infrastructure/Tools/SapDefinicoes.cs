using System.Security.Cryptography;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>As definições do Admin › SAP em ModuleSettings; a senha cifrada com o DataProtection.</summary>
public sealed class SapDefinicoes(DmoDbContext db, IDataProtectionProvider protecao) : ISapDefinicoes
{
    private readonly IDataProtector protetor = protecao.CreateProtector("DMO.Sap.Senha.v1");

    public async Task<SapDefinicoesDados> LerAsync(CancellationToken cancellationToken = default)
    {
        var valores = await ValoresAsync(cancellationToken);
        var utilizador = valores.GetValueOrDefault(SapDefinicoesRegras.ChaveUtilizador);
        var hora = int.TryParse(valores.GetValueOrDefault(SapDefinicoesRegras.ChaveHora), out var h) && SapDefinicoesRegras.HoraValida(h)
            ? h
            : SapDefinicoesRegras.HoraPorOmissao;
        var dias = int.TryParse(valores.GetValueOrDefault(SapDefinicoesRegras.ChaveDias), out var d) && SapDefinicoesRegras.DiasValidos(d)
            ? d
            : SapDefinicoesRegras.DiasPorOmissao;
        return new SapDefinicoesDados(
            string.IsNullOrWhiteSpace(utilizador) ? SapDefinicoesRegras.UtilizadorPorOmissao : utilizador,
            !string.IsNullOrEmpty(valores.GetValueOrDefault(SapDefinicoesRegras.ChaveSenha)),
            hora,
            dias);
    }

    public async Task<string?> SenhaAsync(CancellationToken cancellationToken = default)
    {
        var cifrada = await ValorAsync(SapDefinicoesRegras.ChaveSenha, cancellationToken);
        if (string.IsNullOrEmpty(cifrada))
        {
            return null;
        }

        try
        {
            return protetor.Unprotect(cifrada);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task<SapGravacao> GravarAsync(string? utilizador, string? senhaNova, int hora, int dias, CancellationToken cancellationToken = default)
    {
        utilizador = utilizador?.Trim();
        if (string.IsNullOrEmpty(utilizador) || utilizador.Length > SapDefinicoesRegras.UtilizadorMaximo || utilizador.Any(c => char.IsControl(c) || c == '"'))
        {
            return SapGravacao.UtilizadorInvalido;
        }

        if (!string.IsNullOrEmpty(senhaNova) && (senhaNova.Length > SapDefinicoesRegras.SenhaMaxima || senhaNova.Any(char.IsControl)))
        {
            return SapGravacao.SenhaInvalida;
        }

        if (!SapDefinicoesRegras.HoraValida(hora))
        {
            return SapGravacao.HoraInvalida;
        }

        if (!SapDefinicoesRegras.DiasValidos(dias))
        {
            return SapGravacao.DiasInvalidos;
        }

        await PorAsync(SapDefinicoesRegras.ChaveUtilizador, utilizador, cancellationToken);
        await PorAsync(SapDefinicoesRegras.ChaveHora, hora.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        await PorAsync(SapDefinicoesRegras.ChaveDias, dias.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        if (!string.IsNullOrEmpty(senhaNova))
        {
            await PorAsync(SapDefinicoesRegras.ChaveSenha, protetor.Protect(senhaNova), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return SapGravacao.Gravado;
    }

    public Task<string?> ScriptEditadoAsync(string nome, CancellationToken cancellationToken = default) =>
        SapScriptsRegras.Editavel(nome)
            ? ValorAsync(SapDefinicoesRegras.ChaveScript + nome, cancellationToken)
            : Task.FromResult<string?>(null);

    public async Task<SapGravacao> GravarScriptAsync(string nome, string? texto, CancellationToken cancellationToken = default)
    {
        if (!SapScriptsRegras.Editavel(nome))
        {
            return SapGravacao.ScriptDesconhecido;
        }

        if (texto is { Length: > SapDefinicoesRegras.ScriptMaximo })
        {
            return SapGravacao.ScriptGrande;
        }

        var chave = SapDefinicoesRegras.ChaveScript + nome;
        if (string.IsNullOrWhiteSpace(texto))
        {
            var linha = await db.ModuleSettings.FirstOrDefaultAsync(s => s.SettingKey == chave, cancellationToken);
            if (linha is not null)
            {
                db.ModuleSettings.Remove(linha);
            }
        }
        else
        {
            await PorAsync(chave, texto.Replace("\r\n", "\n"), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return SapGravacao.Gravado;
    }

    public async Task<string> ChaveDeEnvioAsync(CancellationToken cancellationToken = default)
    {
        if (await ChaveGuardadaAsync(cancellationToken) is { } guardada)
        {
            return guardada;
        }

        var nova = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await PorAsync(SapDefinicoesRegras.ChaveEnvio, protetor.Protect(nova), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return nova;
    }

    public async Task<bool> ConfereChaveAsync(string? chave, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(chave) || await ChaveGuardadaAsync(cancellationToken) is not { } guardada)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(chave),
            System.Text.Encoding.ASCII.GetBytes(guardada));
    }

    private async Task<string?> ChaveGuardadaAsync(CancellationToken cancellationToken)
    {
        var cifrada = await ValorAsync(SapDefinicoesRegras.ChaveEnvio, cancellationToken);
        if (string.IsNullOrEmpty(cifrada))
        {
            return null;
        }

        try
        {
            return protetor.Unprotect(cifrada);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SapLeituraFeita>> LeiturasAsync(int maximo, CancellationToken cancellationToken = default)
    {
        var usos = await db.FerramentaUtilizacoes.AsNoTracking()
            .Where(u => u.Origem == FerramentaUtilizacao.OrigemSap)
            .GroupBy(u => new { u.Ficheiro, u.FicheiroModificadoEmUtc })
            .Select(g => new { g.Key.Ficheiro, Lido = g.Max(u => u.RegistadaEmUtc), Linhas = g.Count() })
            .OrderByDescending(g => g.Lido)
            .Take(maximo)
            .ToListAsync(cancellationToken);
        var fabricos = await db.SapFabricos.AsNoTracking()
            .GroupBy(f => new { f.Ficheiro, f.FicheiroModificadoEmUtc })
            .Select(g => new { g.Key.Ficheiro, Lido = g.Max(f => f.LidoEmUtc), Linhas = g.Count() })
            .OrderByDescending(g => g.Lido)
            .Take(maximo)
            .ToListAsync(cancellationToken);
        return usos.Select(u => new SapLeituraFeita(u.Lido, "% uso", u.Ficheiro, u.Linhas))
            .Concat(fabricos.Select(f => new SapLeituraFeita(f.Lido, "Produções", f.Ficheiro, f.Linhas)))
            .OrderByDescending(l => l.LidoEmUtc)
            .Take(maximo)
            .ToList();
    }

    private Task<Dictionary<string, string>> ValoresAsync(CancellationToken cancellationToken) =>
        db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == SapDefinicoesRegras.ChaveUtilizador
                || s.SettingKey == SapDefinicoesRegras.ChaveSenha
                || s.SettingKey == SapDefinicoesRegras.ChaveHora
                || s.SettingKey == SapDefinicoesRegras.ChaveDias)
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? string.Empty, cancellationToken);

    private Task<string?> ValorAsync(string chave, CancellationToken cancellationToken) =>
        db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == chave)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task PorAsync(string chave, string valor, CancellationToken cancellationToken)
    {
        var linha = await db.ModuleSettings.FirstOrDefaultAsync(s => s.SettingKey == chave, cancellationToken);
        if (linha is null)
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = chave, SettingValue = valor });
        }
        else
        {
            linha.SettingValue = valor;
        }
    }
}
