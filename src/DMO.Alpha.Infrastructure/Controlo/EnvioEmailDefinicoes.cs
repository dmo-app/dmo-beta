using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

public enum EnvioEmailEstado
{
    Gravado,
    NomeVazio,
    NomeRepetido,
    TextoLongo,
    SemEnderecos,
    DemasiadosEnderecos,
    RemetenteInvalido,
    NaoExiste
}

/// <summary>
/// O resultado de gravar uma lista ou um modelo. Os endereços que não são um
/// email ficam de fora e voltam em <see cref="Ignorados"/>, para o aviso: a
/// lista grava com os outros (regra de 19/10/2026, avisos em vez de recusas).
/// </summary>
public sealed record EnvioEmailGravacao(EnvioEmailEstado Estado, IReadOnlyList<string> Ignorados, string? Valor = null)
{
    public bool Gravado => Estado == EnvioEmailEstado.Gravado;

    public static EnvioEmailGravacao Recusa(EnvioEmailEstado estado) => new(estado, Array.Empty<string>());
}

/// <summary>
/// As listas de destinatários e os modelos do envio por email, nas Definições
/// do Controlo (Diogo, 19/10/2026, D9). O servidor valida tudo: o nome único,
/// os limites do texto e cada endereço.
/// </summary>
public sealed class EnvioEmailDefinicoes(DmoDbContext db)
{
    public async Task<IReadOnlyList<EmailLista>> ListasAsync(CancellationToken cancellationToken = default) =>
        await db.EmailListas.AsNoTracking().OrderBy(l => l.Nome).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EmailModelo>> ModelosAsync(CancellationToken cancellationToken = default) =>
        await db.EmailModelos.AsNoTracking().OrderBy(m => m.Nome).ToListAsync(cancellationToken);

    public async Task<EnvioEmailGravacao> CriarListaAsync(string? nome, string? destinatarios, CancellationToken cancellationToken = default)
    {
        var lista = new EmailLista();
        var nomeGravado = await NomeDaListaAsync(lista, nome, cancellationToken);
        if (nomeGravado is not null) return nomeGravado;
        var enderecos = Enderecos(lista, destinatarios);
        if (!enderecos.Gravado) return enderecos;
        db.EmailListas.Add(lista);
        return await GravarAsync(enderecos, cancellationToken);
    }

    /// <summary>Muda só o campo enviado (o nome ou os endereços), como na linha da tabela.</summary>
    public async Task<EnvioEmailGravacao> MudarListaAsync(int id, string? nome, string? destinatarios, CancellationToken cancellationToken = default)
    {
        var lista = await db.EmailListas.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lista is null) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NaoExiste);
        if (nome is not null)
        {
            var recusa = await NomeDaListaAsync(lista, nome, cancellationToken);
            if (recusa is not null) return recusa;
            return await GravarAsync(new EnvioEmailGravacao(EnvioEmailEstado.Gravado, Array.Empty<string>(), lista.Nome), cancellationToken);
        }

        var enderecos = Enderecos(lista, destinatarios);
        return enderecos.Gravado ? await GravarAsync(enderecos, cancellationToken) : enderecos;
    }

    public async Task<bool> ApagarListaAsync(int id, CancellationToken cancellationToken = default)
    {
        var lista = await db.EmailListas.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lista is null) return false;
        // Os modelos que a propunham ficam sem lista proposta (também na base, SetNull).
        await foreach (var modelo in db.EmailModelos.Where(m => m.ListaId == id).AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            modelo.ListaId = null;
        }

        db.EmailListas.Remove(lista);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Cria (id null) ou guarda o modelo inteiro, como na janela do modelo.</summary>
    public async Task<EnvioEmailGravacao> GuardarModeloAsync(
        int? id, string? nome, string? remetente, string? assunto, string? corpo, int? listaId,
        CancellationToken cancellationToken = default)
    {
        var modelo = id is int existente
            ? await db.EmailModelos.FirstOrDefaultAsync(m => m.Id == existente, cancellationToken)
            : new EmailModelo();
        if (modelo is null) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NaoExiste);

        var nomeLimpo = nome?.Trim() ?? "";
        if (nomeLimpo.Length == 0) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NomeVazio);
        var remetenteLimpo = string.IsNullOrWhiteSpace(remetente) ? null : remetente.Trim();
        var assuntoLimpo = assunto?.Trim() ?? "";
        var corpoLimpo = (corpo ?? "").Replace("\r\n", "\n").Trim();
        if (nomeLimpo.Length > EnvioEmailRegras.NomeMax
            || assuntoLimpo.Length > EnvioEmailRegras.AssuntoMax
            || corpoLimpo.Length > EnvioEmailRegras.CorpoMax)
        {
            return EnvioEmailGravacao.Recusa(EnvioEmailEstado.TextoLongo);
        }

        if (remetenteLimpo is not null && !EnvioEmailRegras.EmailValido(remetenteLimpo))
        {
            return EnvioEmailGravacao.Recusa(EnvioEmailEstado.RemetenteInvalido);
        }

        if (await db.EmailModelos.AnyAsync(m => m.Nome.ToLower() == nomeLimpo.ToLower() && m.Id != modelo.Id, cancellationToken))
        {
            return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NomeRepetido);
        }

        modelo.Nome = nomeLimpo;
        modelo.Remetente = remetenteLimpo;
        modelo.Assunto = assuntoLimpo;
        modelo.Corpo = corpoLimpo;
        // Uma lista que já não existe fica sem lista proposta.
        modelo.ListaId = listaId is int lista && await db.EmailListas.AnyAsync(l => l.Id == lista, cancellationToken) ? lista : null;
        if (modelo.Id == 0) db.EmailModelos.Add(modelo);
        return await GravarAsync(new EnvioEmailGravacao(EnvioEmailEstado.Gravado, Array.Empty<string>(), modelo.Nome), cancellationToken);
    }

    public async Task<bool> ApagarModeloAsync(int id, CancellationToken cancellationToken = default)
    {
        var modelo = await db.EmailModelos.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (modelo is null) return false;
        db.EmailModelos.Remove(modelo);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<EnvioEmailGravacao?> NomeDaListaAsync(EmailLista lista, string? nome, CancellationToken cancellationToken)
    {
        var limpo = nome?.Trim() ?? "";
        if (limpo.Length == 0) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NomeVazio);
        if (limpo.Length > EnvioEmailRegras.NomeMax) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.TextoLongo);
        if (await db.EmailListas.AnyAsync(l => l.Nome.ToLower() == limpo.ToLower() && l.Id != lista.Id, cancellationToken))
        {
            return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NomeRepetido);
        }

        lista.Nome = limpo;
        return null;
    }

    private static EnvioEmailGravacao Enderecos(EmailLista lista, string? destinatarios)
    {
        var (validos, invalidos) = EnvioEmailRegras.Enderecos(destinatarios);
        if (validos.Count == 0) return new EnvioEmailGravacao(EnvioEmailEstado.SemEnderecos, invalidos);
        if (validos.Count > EnvioEmailRegras.DestinatariosMax) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.DemasiadosEnderecos);
        lista.Destinatarios = string.Join("\n", validos);
        return new EnvioEmailGravacao(EnvioEmailEstado.Gravado, invalidos, string.Join(", ", validos));
    }

    /// <summary>Grava; dois nomes iguais ao mesmo tempo param no índice único e voltam como nome repetido.</summary>
    private async Task<EnvioEmailGravacao> GravarAsync(EnvioEmailGravacao resultado, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return resultado;
        }
        catch (DbUpdateException exception) when (UniqueViolationRetry.IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NomeRepetido);
        }
    }
}
