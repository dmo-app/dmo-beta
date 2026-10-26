using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>Ler, guardar (ou trocar) e retirar a imagem do artigo de uma referência completa.</summary>
public sealed class JobOnArtigoImagemHandler(DmoDbContext db)
{
    public Task<JobOnArtigoImagem?> LerAsync(string? referencia, CancellationToken ct = default)
    {
        var chave = JobOnArtigoImagem.Normalizar(referencia);
        return chave.Length == 0
            ? Task.FromResult<JobOnArtigoImagem?>(null)
            : db.JobOnArtigoImagens.AsNoTracking().FirstOrDefaultAsync(i => i.ReferenciaTotal == chave, ct);
    }

    /// <summary>Guarda a imagem; false quando a referência, o tipo ou o tamanho não servem.</summary>
    public async Task<bool> GuardarAsync(
        string? referencia, string? contentType, byte[] dados, string? ator, CancellationToken ct = default)
    {
        var chave = JobOnArtigoImagem.Normalizar(referencia);
        var tipo = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (chave.Length == 0 || chave.Length > JobOnArtigoImagem.ReferenciaMaxLength
            || !JobOnArtigoImagem.TiposAceites.Contains(tipo)
            || dados.Length == 0 || dados.Length > JobOnArtigoImagem.TamanhoMaximo)
        {
            return false;
        }

        var atual = await db.JobOnArtigoImagens.FirstOrDefaultAsync(i => i.ReferenciaTotal == chave, ct);
        if (atual is null)
        {
            atual = new JobOnArtigoImagem { ReferenciaTotal = chave };
            db.JobOnArtigoImagens.Add(atual);
        }
        atual.ContentType = tipo;
        atual.Dados = dados;
        atual.AtualizadoEmUtc = DateTime.UtcNow;
        atual.AtualizadoPor = ator;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RetirarAsync(string? referencia, CancellationToken ct = default)
    {
        var chave = JobOnArtigoImagem.Normalizar(referencia);
        var atual = await db.JobOnArtigoImagens.FirstOrDefaultAsync(i => i.ReferenciaTotal == chave, ct);
        if (atual is not null)
        {
            db.JobOnArtigoImagens.Remove(atual);
            await db.SaveChangesAsync(ct);
        }
    }
}
