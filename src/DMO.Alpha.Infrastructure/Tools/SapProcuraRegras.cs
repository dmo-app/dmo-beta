using System.Text.Json;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>A tabela dos códigos de procura no SAP em ModuleSettings (Sap:Procura:CM:B1 → JSON).</summary>
public sealed class SapProcuraRegras(DmoDbContext db) : ISapProcuraRegras
{
    private sealed record Guardada(string Antes, SapProcuraMeio Meio, string Depois, bool ComLote);

    public async Task<IReadOnlyList<SapProcuraRegra>> LerAsync(CancellationToken cancellationToken = default) =>
        await LerDeAsync(db, cancellationToken);

    internal static async Task<List<SapProcuraRegra>> LerDeAsync(DmoDbContext db, CancellationToken cancellationToken)
    {
        var linhas = await db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey.StartsWith(SapProcura.Chave))
            .Select(s => new { s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);
        var regras = new List<SapProcuraRegra>();
        foreach (var l in linhas)
        {
            var partes = l.SettingKey[SapProcura.Chave.Length..].Split(':', 2);
            if (partes.Length != 2 || ToolTypeDe(partes[0]) is not { } tipo)
            {
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize<Guardada>(l.SettingValue) is { } g)
                {
                    regras.Add(new SapProcuraRegra(tipo, partes[1], g.Antes ?? "", g.Meio, g.Depois ?? "", g.ComLote));
                }
            }
            catch (JsonException)
            {
                // Uma linha estragada fica de fora; vale a regra geral.
            }
        }

        foreach (var tipo in new[] { ToolType.Cm, ToolType.Mf, ToolType.Bq })
        {
            if (!regras.Any(r => r.Tipo == tipo && r.Linha.Length == 0))
            {
                regras.Add(SapProcuraRegra.PorOmissao(tipo));
            }
        }

        return regras
            .OrderBy(r => r.Tipo)
            .ThenBy(r => r.Linha.Length == 0 ? 0 : 1)
            .ThenBy(r => r.Linha, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<SapProcuraGravacao> GravarAsync(SapProcuraRegra regra, CancellationToken cancellationToken = default)
    {
        var linha = regra.Linha.Trim().ToUpperInvariant();
        if (linha.Length > 20 || linha.Any(c => !char.IsAsciiLetterOrDigit(c)))
        {
            return SapProcuraGravacao.LinhaInvalida;
        }

        if (!TextoValido(regra.Antes) || !TextoValido(regra.Depois))
        {
            return SapProcuraGravacao.TextoInvalido;
        }

        var chave = SapProcura.Chave + ToolTypeTokens.ToStorage(regra.Tipo) + ":" + linha;
        var valor = JsonSerializer.Serialize(new Guardada(regra.Antes.Trim().ToUpperInvariant(), regra.Meio, regra.Depois.Trim().ToUpperInvariant(), regra.ComLote));
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
        return SapProcuraGravacao.Gravado;
    }

    public async Task ApagarAsync(ToolType tipo, string linha, CancellationToken cancellationToken = default)
    {
        var chave = SapProcura.Chave + ToolTypeTokens.ToStorage(tipo) + ":" + (linha ?? "").Trim().ToUpperInvariant();
        var existente = await db.ModuleSettings.FirstOrDefaultAsync(s => s.SettingKey == chave, cancellationToken);
        if (existente is not null)
        {
            db.ModuleSettings.Remove(existente);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<(string? Codigo, string? AMao)> DaFerramentaAsync(string toolId, CancellationToken cancellationToken = default)
    {
        var tool = await db.Tools.AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new { t.Type, t.TechnicalName, t.Reference, t.Lot, t.SapCodigo })
            .FirstOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            return (null, null);
        }

        var regras = await LerDeAsync(db, cancellationToken);
        var linhas = await db.ToolMachineLinks.AsNoTracking().Where(l => l.ToolId == toolId).Select(l => l.Machine.Code).ToListAsync(cancellationToken);
        return (SapProcura.Codigo(regras, tool.Type, linhas, tool.SapCodigo, tool.TechnicalName, tool.Reference, tool.Lot), tool.SapCodigo);
    }

    public async Task<bool> GravarCodigoAsync(string toolId, string? codigo, CancellationToken cancellationToken = default)
    {
        var limpo = (codigo ?? "").Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);
        if (limpo.Length > FerramentaUtilizacao.CodigoMaxLength || !limpo.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            return false;
        }

        var tool = await db.Tools.FirstOrDefaultAsync(t => t.ToolId == toolId, cancellationToken);
        if (tool is null)
        {
            return false;
        }

        tool.SapCodigo = limpo.Length == 0 ? null : limpo;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Letras, algarismos, «-» e «_»; o «*» bloqueia a ZPPG (Diogo), nada de aspas nem espaços.
    private static bool TextoValido(string? texto) =>
        (texto ?? "").Trim().Length <= SapProcuraRegra.TextoMaximo
        && (texto ?? "").Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static ToolType? ToolTypeDe(string token)
    {
        try
        {
            return ToolTypeTokens.FromStorage(token);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
