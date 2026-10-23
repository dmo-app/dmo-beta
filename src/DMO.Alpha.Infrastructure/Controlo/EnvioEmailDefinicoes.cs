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
    NaoExiste,
    EmailRepetido
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

    /// <summary>
    /// Criar uma lista, ou juntar pessoas a uma que já existe com o mesmo nome
    /// (Diogo, 23/10/2026: um só botão no cartão). <paramref name="pessoa"/> é o
    /// nome de quem se junta, quando se escreve um só email sem nome.
    /// </summary>
    public async Task<EnvioEmailGravacao> CriarListaAsync(string? nome, string? destinatarios, CancellationToken cancellationToken = default, string? pessoa = null)
    {
        var limpo = nome?.Trim() ?? "";
        var existente = limpo.Length == 0 ? null
            : await db.EmailListas.FirstOrDefaultAsync(l => l.Nome.ToLower() == limpo.ToLower(), cancellationToken);
        if (existente is not null)
        {
            var juntar = Enderecos(existente, destinatarios, pessoa, EnvioEmailRegras.LerLista(existente.Destinatarios));
            return juntar.Gravado ? await GravarAsync(juntar, cancellationToken) : juntar;
        }

        var lista = new EmailLista();
        var nomeGravado = await NomeDaListaAsync(lista, nome, cancellationToken);
        if (nomeGravado is not null) return nomeGravado;
        var enderecos = Enderecos(lista, destinatarios, pessoa);
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

    /// <summary>Mudar o nome ou o email de uma pessoa da lista (a posição na lista), no próprio valor da linha.</summary>
    public async Task<EnvioEmailGravacao> MudarPessoaAsync(int id, int indice, string? nome, string? email, CancellationToken cancellationToken = default)
    {
        var lista = await db.EmailListas.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        var pessoas = lista is null ? [] : EnvioEmailRegras.LerLista(lista.Destinatarios).ToList();
        if (lista is null || indice < 0 || indice >= pessoas.Count) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NaoExiste);
        if (email is not null)
        {
            var limpo = email.Trim();
            if (!EnvioEmailRegras.EmailValido(limpo)) return new EnvioEmailGravacao(EnvioEmailEstado.SemEnderecos, [limpo]);
            if (pessoas.Where((_, i) => i != indice).Any(p => string.Equals(p.Email, limpo, StringComparison.OrdinalIgnoreCase)))
                return EnvioEmailGravacao.Recusa(EnvioEmailEstado.EmailRepetido);
            pessoas[indice] = pessoas[indice] with { Email = limpo };
            lista.Destinatarios = string.Join("\n", pessoas.Select(EnvioEmailRegras.Linha));
            return await GravarAsync(new EnvioEmailGravacao(EnvioEmailEstado.Gravado, [], limpo), cancellationToken);
        }

        var nomeLimpo = EnvioEmailRegras.LimparNome(nome);
        pessoas[indice] = pessoas[indice] with { Nome = nomeLimpo };
        lista.Destinatarios = string.Join("\n", pessoas.Select(EnvioEmailRegras.Linha));
        if (lista.Destinatarios.Length > DestinatariosTextoMax) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.TextoLongo);
        return await GravarAsync(new EnvioEmailGravacao(EnvioEmailEstado.Gravado, [], nomeLimpo ?? ""), cancellationToken);
    }

    /// <summary>Tirar uma pessoa da lista (a posição); a última não sai (a lista ficava sem emails).</summary>
    public async Task<EnvioEmailGravacao> RemoverPessoaAsync(int id, int indice, CancellationToken cancellationToken = default)
    {
        var lista = await db.EmailListas.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        var pessoas = lista is null ? [] : EnvioEmailRegras.LerLista(lista.Destinatarios).ToList();
        if (lista is null || indice < 0 || indice >= pessoas.Count) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.NaoExiste);
        if (pessoas.Count == 1) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.SemEnderecos);
        pessoas.RemoveAt(indice);
        lista.Destinatarios = string.Join("\n", pessoas.Select(EnvioEmailRegras.Linha));
        return await GravarAsync(new EnvioEmailGravacao(EnvioEmailEstado.Gravado, []), cancellationToken);
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

    /// <summary>O tamanho da coluna dos destinatários (HasMaxLength na base).</summary>
    private const int DestinatariosTextoMax = 13000;

    /// <summary>
    /// As pessoas escritas (também coladas do Outlook, com os nomes) gravam-se
    /// uma por linha, «Nome &lt;email&gt;» ou só o email; com <paramref name="antes"/>
    /// juntam-se às que a lista já tem (os emails repetidos não entram outra vez).
    /// </summary>
    private static EnvioEmailGravacao Enderecos(EmailLista lista, string? destinatarios, string? pessoa = null, IReadOnlyList<EmailPessoa>? antes = null)
    {
        var (novas, invalidos) = EnvioEmailRegras.Pessoas(destinatarios);
        if (novas.Count == 1 && novas[0].Nome is null && EnvioEmailRegras.LimparNome(pessoa) is { } nomePessoa)
            novas = [novas[0] with { Nome = nomePessoa }];
        if (novas.Count == 0) return new EnvioEmailGravacao(EnvioEmailEstado.SemEnderecos, invalidos);
        var todas = (antes ?? []).ToList();
        var juntas = 0;
        foreach (var nova in novas)
            if (!todas.Any(p => string.Equals(p.Email, nova.Email, StringComparison.OrdinalIgnoreCase))) { todas.Add(nova); juntas++; }
        // Só emails que a lista já tem: nada a juntar, avisa.
        if (antes is not null && juntas == 0) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.EmailRepetido);
        if (todas.Count > EnvioEmailRegras.DestinatariosMax) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.DemasiadosEnderecos);
        var texto = string.Join("\n", todas.Select(EnvioEmailRegras.Linha));
        if (texto.Length > DestinatariosTextoMax) return EnvioEmailGravacao.Recusa(EnvioEmailEstado.TextoLongo);
        lista.Destinatarios = texto;
        return new EnvioEmailGravacao(EnvioEmailEstado.Gravado, invalidos, EnvioEmailRegras.Texto(todas));
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
