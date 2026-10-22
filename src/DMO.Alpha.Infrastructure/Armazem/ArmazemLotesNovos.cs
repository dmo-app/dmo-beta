using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// Os lotes novos do Armazém sobre o <see cref="DmoDbContext"/>. O registo
/// guarda só o pedido («por criar»); a ferramenta cria-se nas Ferramentas, e
/// ao resolver a entrada fica registada pelo mesmo caminho dos movimentos
/// (<see cref="IArmazemMovimentos.EntradaAsync"/>), na posição guardada e à
/// data do registo: o último movimento decide a localização.
/// </summary>
public sealed class ArmazemLotesNovos(DmoDbContext db, IArmazemMovimentos movimentos, ICurrentAccountContext account) : IArmazemLotesNovos
{
    /// <summary>A observação da entrada que resolve um lote novo.</summary>
    public const string ObservacaoDaEntrada = "Lote novo registado no Armazém";

    public async Task<ArmazemLoteNovoSaida> RegistarAsync(ToolType tipo, string? referencia, string? lote, string? posicao, CancellationToken cancellationToken = default)
    {
        var refLimpa = referencia?.Trim().ToUpperInvariant();
        var loteLimpo = lote?.Trim();
        if (string.IsNullOrEmpty(refLimpa))
        {
            return new(ArmazemLoteNovoResultado.SemReferencia);
        }

        if (string.IsNullOrEmpty(loteLimpo))
        {
            return new(ArmazemLoteNovoResultado.SemLote);
        }

        if (refLimpa.Length > ArmazemLoteNovo.ReferenciaMaxLength)
        {
            return new(ArmazemLoteNovoResultado.ReferenciaLonga);
        }

        if (loteLimpo.Length > ArmazemLoteNovo.LoteMaxLength)
        {
            return new(ArmazemLoteNovoResultado.LoteLongo);
        }

        if (!PosicaoCodigo.TryParse(posicao, out var codigo))
        {
            return new(ArmazemLoteNovoResultado.PosicaoInvalida);
        }

        var avisos = new List<string>();
        if (await ExistenteAsync(tipo, refLimpa, loteLimpo, cancellationToken) is not null)
        {
            avisos.Add($"Já existe {ArmazemTextos.Ferramenta(tipo, refLimpa, loteLimpo)} nas Ferramentas: quem tem o módulo Ferramentas pode ligá-la a este registo.");
        }

        if (await db.ArmazemLotesNovos.AnyAsync(l => l.Estado == ArmazemLoteNovoEstado.PorCriar && l.Tipo == tipo && l.Referencia == refLimpa && l.Lote == loteLimpo, cancellationToken))
        {
            avisos.Add("Este lote já estava registado como por criar. Fica mais um registo.");
        }

        if (!await db.ArmazemPosicoes.AnyAsync(p => p.Codigo == codigo, cancellationToken))
        {
            avisos.Add($"A posição {codigo} ainda não existe: cria-se quando a ferramenta for criada.");
        }

        var registo = new ArmazemLoteNovo
        {
            Tipo = tipo,
            Referencia = refLimpa,
            Lote = loteLimpo,
            Posicao = codigo,
            RegistadoEmUtc = DateTime.UtcNow,
            RegistadoPor = Autor()
        };
        db.ArmazemLotesNovos.Add(registo);
        await db.SaveChangesAsync(cancellationToken);
        return new(ArmazemLoteNovoResultado.Gravado, registo.Id, avisos);
    }

    public async Task<IReadOnlyList<ArmazemLoteNovoItem>> PorCriarAsync(ToolType? tipo = null, CancellationToken cancellationToken = default)
    {
        var query = db.ArmazemLotesNovos.AsNoTracking().Where(l => l.Estado == ArmazemLoteNovoEstado.PorCriar);
        if (tipo is { } t)
        {
            query = query.Where(l => l.Tipo == t);
        }

        return await query.OrderBy(l => l.RegistadoEmUtc).ThenBy(l => l.Id)
            .Select(l => new ArmazemLoteNovoItem(l.Id, l.Tipo, l.Referencia, l.Lote, l.Posicao, l.RegistadoEmUtc, l.RegistadoPor, l.Estado, l.ResolvedToolId))
            .ToListAsync(cancellationToken);
    }

    public async Task<ArmazemLoteNovoItem?> ObterAsync(int id, CancellationToken cancellationToken = default) =>
        await db.ArmazemLotesNovos.AsNoTracking().Where(l => l.Id == id)
            .Select(l => new ArmazemLoteNovoItem(l.Id, l.Tipo, l.Referencia, l.Lote, l.Posicao, l.RegistadoEmUtc, l.RegistadoPor, l.Estado, l.ResolvedToolId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string?> FerramentaExistenteAsync(int id, CancellationToken cancellationToken = default) =>
        await ObterAsync(id, cancellationToken) is { } l ? await ExistenteAsync(l.Tipo, l.Referencia, l.Lote, cancellationToken) : null;

    public async Task<ArmazemLoteNovoResolucaoSaida> ResolverAsync(int id, string toolId, CancellationToken cancellationToken = default)
    {
        var registo = await db.ArmazemLotesNovos.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (registo is null)
        {
            return new(ArmazemLoteNovoResolucao.NaoExiste, []);
        }

        if (registo.Estado == ArmazemLoteNovoEstado.Resolvido)
        {
            return new(ArmazemLoteNovoResolucao.JaResolvido, []);
        }

        if (toolId == ToolRegistryConstants.RemovedToolId || !await db.Tools.AnyAsync(t => t.ToolId == toolId, cancellationToken))
        {
            return new(ArmazemLoteNovoResolucao.FerramentaNaoExiste, []);
        }

        // A entrada pelo caminho dos movimentos (os mesmos avisos e a mesma
        // ocupação). O identificador do envio vem do registo: resolver duas
        // vezes não dá duas entradas.
        var avisos = new List<string>();
        var observacao = registo.RegistadoPor is { Length: > 0 } por ? $"{ObservacaoDaEntrada} por {por}" : ObservacaoDaEntrada;
        if (observacao.Length > ArmazemMovimento.ObservacoesMaxLength)
        {
            observacao = ObservacaoDaEntrada;
        }

        var entrada = await movimentos.EntradaAsync(
            new PedidoDeEntrada(OperacaoDe(registo.Id), toolId, registo.Posicao, ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo,
                registo.RegistadoEmUtc, observacao, CriarPosicao: true),
            cancellationToken);
        avisos.AddRange(entrada.Avisos);
        if (!entrada.Gravou)
        {
            // A ferramenta já está numa posição: o registo resolve-se na mesma
            // (o último movimento continua a decidir) e fica o aviso.
            avisos.Add(entrada.Resultado == ArmazemMovimentoResultado.JaNoArmazem
                ? $"A ferramenta já estava no armazém, na posição {entrada.Posicao}: a entrada na posição {registo.Posicao} não foi registada."
                : $"A entrada na posição {registo.Posicao} não foi registada; registe-a no Armazém.");
        }

        // Os movimentos podem ter limpo o que a base seguia (um conflito volta a ler).
        registo = await db.ArmazemLotesNovos.FirstAsync(l => l.Id == id, cancellationToken);
        registo.Estado = ArmazemLoteNovoEstado.Resolvido;
        registo.ResolvedToolId = toolId;
        registo.MovimentoId = entrada.Gravou ? entrada.MovimentoId : null;
        registo.ResolvidoEmUtc = DateTime.UtcNow;
        registo.ResolvidoPor = Autor();
        await db.SaveChangesAsync(cancellationToken);
        return new(ArmazemLoteNovoResolucao.Resolvido, avisos);
    }

    /// <summary>O envio da entrada de um lote novo: sempre o mesmo para o mesmo registo.</summary>
    internal static Guid OperacaoDe(int id)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(id).CopyTo(bytes, 0);
        "LOTENOVO"u8.ToArray().CopyTo(bytes, 8);
        return new Guid(bytes);
    }

    private async Task<string?> ExistenteAsync(ToolType tipo, string referencia, string lote, CancellationToken cancellationToken) =>
        await db.Tools.AsNoTracking()
            .Where(t => t.Type == tipo && t.Reference.ToUpper() == referencia && t.Lot == lote && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .OrderBy(t => t.ToolId)
            .Select(t => t.ToolId)
            .FirstOrDefaultAsync(cancellationToken);

    private string? Autor()
    {
        var actor = account.Current;
        return actor.IsAuthenticated
            ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName)
            : null;
    }
}
