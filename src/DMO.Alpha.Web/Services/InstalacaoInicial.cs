using System.Security.Cryptography;
using System.Text;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Authentication.Local;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// A primeira instalação (Diogo, 26/10/2026: «agora seria uma boa altura para
/// inserir o setup de novo»). Só existe com o login próprio (Local) e só
/// enquanto a base não tem ADMIN nem nenhuma palavra-passe: cria o ADMIN
/// (o email das Definições e a sua palavra-passe) e desaparece. Os
/// utilizadores criam-se depois em Admin › Utilizadores, como sempre.
/// Quem a abre tem de escrever o código de instalação, que só se vê no
/// servidor (no log do arranque, ou o que se pôs em Dmo__CodigoDeInstalacao):
/// a app pode estar aberta à internet antes de ter ADMIN.
/// </summary>
public sealed class InstalacaoInicial(
    DmoDbContext db,
    IAdminEmailSettings adminEmailSettings,
    CodigoDeInstalacao codigo,
    LoginProprio loginProprio,
    ILogger<InstalacaoInicial> logger)
{
    public const string JaFeita = "A instalação já foi feita. Entre com o email e a palavra-passe do ADMIN.";
    public const string CodigoErrado = "O código de instalação não está certo.";

    /// <summary>
    /// Login próprio e a base ainda sem ADMIN nem nenhuma palavra-passe. Sem
    /// base (em baixo, ou por configurar) não há instalação: o login abre na mesma.
    /// </summary>
    public async Task<bool> PendenteAsync(CancellationToken cancellationToken = default)
    {
        if (!loginProprio.Ativo)
        {
            return false;
        }

        try
        {
            var pendente = await adminEmailSettings.GetAdminEmailAsync(cancellationToken) is null
                && !await db.LocalCredentials.AnyAsync(cancellationToken);
            if (pendente)
            {
                codigo.Anunciar();
            }

            return pendente;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Primeira instalação: não foi possível ver se a base já tem ADMIN.");
            return false;
        }
    }

    /// <summary>Cria o ADMIN. Devolve o erro, ou null quando ficou feito.</summary>
    public async Task<string?> InstalarAsync(
        string? codigoEscrito, string? email, string? palavraPasse, string? confirmar,
        CancellationToken cancellationToken = default)
    {
        if (!await PendenteAsync(cancellationToken))
        {
            return JaFeita;
        }

        if (!codigo.Confere(codigoEscrito))
        {
            return CodigoErrado;
        }

        var adminEmail = email?.Trim() ?? string.Empty;
        if (!AdminEmailSettings.IsUsableEmail(adminEmail))
        {
            return "O email indicado não é utilizável.";
        }

        if (string.IsNullOrWhiteSpace(palavraPasse) || string.IsNullOrWhiteSpace(confirmar))
        {
            return "Indique e confirme a palavra-passe.";
        }

        if (!string.Equals(palavraPasse, confirmar, StringComparison.Ordinal))
        {
            return "As palavras-passe não coincidem.";
        }

        if (palavraPasse.Length < MudancaDePalavraPasse.MinimumLength)
        {
            return $"A palavra-passe deve ter pelo menos {MudancaDePalavraPasse.MinimumLength} caracteres.";
        }

        // O email do ADMIN e a sua palavra-passe entram juntos ou não entram.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (!await adminEmailSettings.TryClaimAdminEmailAsync(adminEmail, cancellationToken))
        {
            return JaFeita;
        }

        db.LocalCredentials.Add(new LocalCredential
        {
            ProviderUserId = Guid.NewGuid().ToString(),
            Email = adminEmail.ToLowerInvariant(),
            PasswordHash = LocalPasswordHasher.Hash(palavraPasse),
            IsTemporaryPassword = false,
            UpdatedAt = TimeProvider.System.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        codigo.Gastar();
        return null;
    }
}

/// <summary>O login é o da própria app (Local): só assim há primeira instalação.</summary>
public sealed record LoginProprio(bool Ativo);

/// <summary>
/// O código que a primeira instalação pede. Vem de Dmo:CodigoDeInstalacao ou
/// é sorteado no arranque, e escreve-se no log da app da primeira vez que a
/// instalação aparece (é só o servidor que o vê); depois de 5 enganos
/// sorteia-se outro (e escreve-se também), para não se poder adivinhar.
/// </summary>
public sealed class CodigoDeInstalacao
{
    // Sem 0/O, 1/I/L: lê-se do log sem dúvidas.
    private const string Letras = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int EnganosAteTrocar = 5;

    private readonly ILogger<CodigoDeInstalacao> _logger;
    private readonly bool _fixo;
    private readonly object _trinco = new();
    private string _valor;
    private int _enganos;
    private bool _gasto;
    private bool _anunciado;

    public CodigoDeInstalacao(IConfiguration configuration, ILogger<CodigoDeInstalacao> logger)
    {
        _logger = logger;
        var configurado = configuration["Dmo:CodigoDeInstalacao"];
        _fixo = !string.IsNullOrWhiteSpace(configurado);
        _valor = _fixo ? Normalizar(configurado) : Sortear();
    }

    /// <summary>O código como se mostra (XXXX-XXXX quando sorteado).</summary>
    public string Valor
    {
        get
        {
            lock (_trinco)
            {
                return _fixo || _valor.Length != 8 ? _valor : $"{_valor[..4]}-{_valor[4..]}";
            }
        }
    }

    public bool Confere(string? escrito)
    {
        lock (_trinco)
        {
            if (_gasto)
            {
                return false;
            }

            var a = Encoding.UTF8.GetBytes(Normalizar(escrito));
            var b = Encoding.UTF8.GetBytes(_valor);
            if (a.Length > 0 && CryptographicOperations.FixedTimeEquals(a, b))
            {
                return true;
            }

            if (++_enganos >= EnganosAteTrocar && !_fixo)
            {
                _enganos = 0;
                _valor = Sortear();
                _logger.LogWarning(
                    "Primeira instalação: {Enganos} códigos errados, o código mudou. Novo código de instalação: {Codigo}",
                    EnganosAteTrocar, Valor);
            }

            return false;
        }
    }

    /// <summary>Escreve o código no log, uma vez (o código configurado não se escreve).</summary>
    public void Anunciar()
    {
        lock (_trinco)
        {
            if (_anunciado || _gasto)
            {
                return;
            }

            _anunciado = true;
        }

        if (_fixo)
        {
            _logger.LogWarning("Primeira instalação por fazer: abra /instalar e use o código de Dmo__CodigoDeInstalacao.");
        }
        else
        {
            _logger.LogWarning("Primeira instalação por fazer: abra /instalar e use o código de instalação {Codigo}", Valor);
        }
    }

    public void Gastar()
    {
        lock (_trinco)
        {
            _gasto = true;
        }
    }

    private static string Normalizar(string? texto) =>
        new((texto ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());

    private static string Sortear() =>
        new(Enumerable.Range(0, 8).Select(_ => Letras[RandomNumberGenerator.GetInt32(Letras.Length)]).ToArray());
}
