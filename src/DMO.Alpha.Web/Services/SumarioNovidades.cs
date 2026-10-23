using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Pages.Shared;
using DMO.Alpha.Web.Presentation;
using Microsoft.Extensions.Caching.Memory;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// OS IMPORTANTES DO SUMÁRIO (Diogo, 23/10/2026: «os notices importantes
/// entram no sumário. Criar uma Informação "Importante" e ficam sempre no
/// topo até resolver»): os por resolver da consulta, os lotes novos por criar
/// e os lembretes «Utilização por atualizar» da ficha, numa só lista, pelos
/// módulos da conta.
/// E O PONTO DO SUMÁRIO: um ponto pequeno, sem número, no separador
/// «Sumário» do Planeamento quando há um importante por resolver mais recente
/// do que a última vez que a conta abriu o Sumário; abri-lo apaga-o. O «visto em» é uma definição pessoal
/// (<see cref="UserSettingKeys.SumarioVistoEm"/>), sem tabela nova. A conta
/// mais pesada (a última linha importante dos últimos dias) fica em memória
/// um minuto por utilizador; cada página lê só o «visto em».
/// </summary>
public sealed class SumarioNovidades(
    ISumarioQuery sumario,
    IArmazemLotesNovos lotesNovos,
    IFerramentasUsoSap usoSap,
    IDefinicoesPessoais pessoais,
    ICurrentAccountContext account,
    CurrentUserCapabilities capabilities,
    IMemoryCache cache)
{
    /// <summary>Quantos lembretes «Utilização por atualizar» se leem.</summary>
    public const int LembretesMaximo = 20;

    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(1);

    private static string Chave(Guid userId) => $"dmo-sumario-ultimo:{userId:N}";

    /// <summary>True quando há uma linha importante mais recente do que a última vez que a conta abriu o Sumário.</summary>
    public async Task<bool> TemNovoAsync(CancellationToken cancellationToken = default)
    {
        if (account.Current.ActorId is not { } userId)
        {
            return false;
        }

        var ultimo = await cache.GetOrCreateAsync(Chave(userId), async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = Validade;
            return await UltimoImportanteAsync(cancellationToken);
        });
        if (ultimo is not { } emUtc)
        {
            return false;
        }

        var visto = await pessoais.LerAsync(userId, UserSettingKeys.SumarioVistoEm, cancellationToken);
        return !DateTime.TryParse(visto, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var vistoUtc)
            || emUtc > vistoUtc;
    }

    /// <summary>A conta abriu o Sumário: o ponto desaparece.</summary>
    public async Task MarcarVistoAsync(CancellationToken cancellationToken = default)
    {
        if (account.Current.ActorId is not { } userId)
        {
            return;
        }

        await pessoais.GravarAsync(userId, UserSettingKeys.SumarioVistoEm, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), cancellationToken);
        cache.Remove(Chave(userId));
    }

    private async Task<DateTime?> UltimoImportanteAsync(CancellationToken cancellationToken)
    {
        var modulos = await capabilities.GetAsync(cancellationToken);
        if (!modulos.Contains(DmoCapabilities.JobOnCreate, StringComparer.Ordinal))
        {
            return null;
        }

        var importantes = await ImportantesAsync(modulos, cancellationToken);
        return importantes.Count == 0 ? null : importantes.Max(i => i.InstanteUtc);
    }

    /// <summary>Os importantes por resolver que os módulos deixam ver, os mais recentes primeiro.</summary>
    public async Task<IReadOnlyList<SumarioItem>> ImportantesAsync(IReadOnlyCollection<string> modulos, CancellationToken cancellationToken = default)
    {
        var tipos = SumarioTipos.Visiveis(modulos).Where(SumarioTipos.Importante).ToList();
        var items = new List<SumarioItem>();
        var daConsulta = tipos.Where(SumarioTipos.DoPeriodo).ToList();
        if (daConsulta.Count > 0)
        {
            items.AddRange(await sumario.PorResolverAsync(DateOnly.FromDateTime(DmoBusinessTime.Now), daConsulta, cancellationToken));
        }

        // O lote novo sai quando a ferramenta é criada (ou ligada a uma existente).
        if (tipos.Contains(SumarioTipo.LoteNovoPorCriar))
        {
            foreach (var lote in await lotesNovos.PorCriarAsync(null, cancellationToken))
            {
                var tipo = ArmazemTextos.Tipo(lote.Tipo);
                var em = DateTime.SpecifyKind(lote.RegistadoEmUtc, DateTimeKind.Utc);
                items.Add(new SumarioItem(
                    SumarioTipo.LoteNovoPorCriar,
                    DateOnly.FromDateTime(DmoBusinessTime.ToLocal(em)),
                    em,
                    $"{tipo} {lote.Referencia} · Lote {LoteTexto.Mostrar(lote.Lote)}",
                    $"Ferramenta nova registada no Armazém, posição {lote.Posicao}",
                    lote.RegistadoPor)
                {
                    LoteNovoId = lote.Id,
                    Link = DmoAvisoLink.CriarLoteNovo(lote.Id, tipo, lote.Referencia, lote.Lote).Href
                });
            }
        }

        // O lembrete da ficha sai com a leitura nova; a mesma ferramenta já
        // na lista pelo fim de um Job On não se repete.
        if (tipos.Contains(SumarioTipo.UsoAtualizado))
        {
            var ja = items.Where(i => i.Tipo == SumarioTipo.UsoAtualizado).Select(i => i.ToolId).ToHashSet(StringComparer.Ordinal);
            foreach (var uso in await usoSap.PorAtualizarAsync(LembretesMaximo, cancellationToken))
            {
                if (ja.Contains(uso.ToolId))
                {
                    continue;
                }

                var desde = DateTime.SpecifyKind(uso.DesdeUtc, DateTimeKind.Utc);
                items.Add(new SumarioItem(
                    SumarioTipo.UsoAtualizado,
                    DateOnly.FromDateTime(DmoBusinessTime.ToLocal(desde)),
                    desde,
                    $"{uso.Tipo.ToString().ToUpperInvariant()} {uso.Referencia} · Lote {LoteTexto.Mostrar(uso.Lote)}",
                    "Utilização por atualizar (entrou no Armazém)",
                    null) { ToolId = uso.ToolId, Lembrete = true });
            }
        }

        return items.OrderByDescending(i => i.InstanteUtc).ThenBy(i => i.Tipo).ToList();
    }
}
