using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>DMO-663: comparar o retrato SAP e decidir sem aceitar valores do browser.</summary>
public sealed class FerramentasAvisosSap(DmoDbContext db, ICurrentAccountContext account) : IFerramentasAvisosSap
{
    public async Task<AvisosSap?> LerAsync(string toolId, CancellationToken cancellationToken = default)
    {
        var tool = await db.Tools.SingleOrDefaultAsync(t => t.ToolId == toolId && t.ToolId != ToolRegistryConstants.RemovedToolId, cancellationToken);
        if (tool is null) return null;
        var uso = await db.FerramentaUtilizacoes.AsNoTracking().Where(u => u.ToolId == toolId).OrderByDescending(u => u.Id).FirstOrDefaultAsync(cancellationToken);
        var quantidade = tool.Type == ToolType.Bq ? tool.BqAccountedQuantity : tool.AnQuantity;
        return new(toolId, db.Entry(tool).Property<uint>(DmoDbContext.RowVersion).CurrentValue, quantidade,
            tool.SapPecas != quantidade && tool.SapPecas != tool.SapPecasIgnoradas ? tool.SapPecas : null,
            uso?.Percentagem,
            uso?.Origem == FerramentaUtilizacao.OrigemManual && tool.SapPercentagem != uso.Percentagem && tool.SapPercentagem != tool.SapPercentagemIgnorada ? tool.SapPercentagem : null,
            tool.SapFornecedor, tool.SapDataAquisicao);
    }

    public async Task<DecisaoSapResultado> DecidirAsync(string toolId, uint versao, string campo, bool aceitar, CancellationToken cancellationToken = default)
    {
        if (campo is not ("pecas" or "uso")) return DecisaoSapResultado.Invalido;
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (db.Database.IsNpgsql())
            await db.Tools.FromSqlInterpolated($"SELECT *, xmin FROM \"Tools\" WHERE \"ToolId\" = {toolId} FOR UPDATE").LoadAsync(cancellationToken);
        var avisos = await LerAsync(toolId, cancellationToken);
        if (avisos is null) return DecisaoSapResultado.NaoEncontrado;
        if (avisos.Versao != versao || (campo == "pecas" ? avisos.Pecas is null : avisos.Uso is null)) return DecisaoSapResultado.Desatualizado;
        var tool = await db.Tools.SingleAsync(t => t.ToolId == toolId, cancellationToken);
        if (campo == "pecas")
        {
            if (aceitar)
            {
                if (tool.Type == ToolType.Bq) tool.BqAccountedQuantity = avisos.Pecas;
                else tool.AnQuantity = avisos.Pecas;
            }
            if (!aceitar) tool.SapPecasIgnoradas = avisos.Pecas;
        }
        else
        {
            if (aceitar)
            {
                db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao
                {
                    ToolId = toolId, Percentagem = avisos.Uso!.Value, Origem = FerramentaUtilizacao.OrigemSap,
                    RegistadaEmUtc = DateTime.UtcNow, RegistadoPor = account.Current.DisplayName
                });
            }
            if (!aceitar) tool.SapPercentagemIgnorada = avisos.Uso;
        }
        tool.SapAvisosAlteradosEmUtc = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
            return DecisaoSapResultado.Gravado;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return DecisaoSapResultado.Desatualizado;
        }
    }
}
