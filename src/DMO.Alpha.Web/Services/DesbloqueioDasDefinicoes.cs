using System.Security.Cryptography;
using DMO.Alpha.Core.Admin;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// O CADEADO das Definições gerais (Diogo, 22/10/2026): o PIN criado pelo
/// ADMIN abre a parte Geral para este utilizador até sair. Fica um cookie de
/// sessão cifrado (o utilizador, a versão da sessão e a marca do PIN): ao
/// sair, ao mudar a palavra-passe ou ao mudar o PIN, volta a fechar. O ADMIN
/// está sempre desbloqueado. Depois de 5 PINs errados, espera-se 30 segundos
/// entre tentativas. O PIN nunca se escreve nos registos.
/// </summary>
public sealed class DesbloqueioDasDefinicoes(
    IPinDasDefinicoes pin,
    IDataProtectionProvider dataProtection,
    IMemoryCache cache,
    TimeProvider? relogio = null)
{
    public const string Cookie = "dmo-def-pin";
    public const int TentativasAntesDaEspera = 5;
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(30);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("DMO.Definicoes.Desbloqueio.v1");
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;

    public enum Resultado { Aberto, Errado, Espera, SemPin }

    private sealed class Falhas
    {
        public int Contagem;
        public DateTimeOffset Ultima;
    }

    public Task<bool> TemPinAsync(CancellationToken cancellationToken) =>
        pin.HashAsync(cancellationToken).ContinueWith(t => t.Result is not null, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    public async Task<bool> AbertoAsync(HttpContext context)
    {
        if (context.User.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString))
        {
            return true;
        }

        if (!context.Request.Cookies.TryGetValue(Cookie, out var valor) || string.IsNullOrEmpty(valor))
        {
            return false;
        }

        var hash = await pin.HashAsync(context.RequestAborted);
        if (hash is null)
        {
            return false;
        }

        try
        {
            return string.Equals(_protector.Unprotect(valor), Marca(context, hash), StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public async Task<Resultado> AbrirAsync(HttpContext context, string? pinEscrito)
    {
        var hash = await pin.HashAsync(context.RequestAborted);
        if (hash is null)
        {
            return Resultado.SemPin;
        }

        var chave = "dmo-def-pin-falhas:" + (context.User.FindFirst(DmoClaimTypes.ActorId)?.Value ?? "?");
        var falhas = cache.GetOrCreate(chave, entrada =>
        {
            entrada.SlidingExpiration = TimeSpan.FromHours(1);
            return new Falhas();
        })!;
        var agora = _relogio.GetUtcNow();
        lock (falhas)
        {
            if (falhas.Contagem >= TentativasAntesDaEspera && agora - falhas.Ultima < Espera)
            {
                return Resultado.Espera;
            }
        }

        if (!await pin.ConfereAsync(pinEscrito, context.RequestAborted))
        {
            lock (falhas)
            {
                falhas.Contagem++;
                falhas.Ultima = agora;
            }

            return Resultado.Errado;
        }

        cache.Remove(chave);
        context.Response.Cookies.Append(Cookie, _protector.Protect(Marca(context, hash)), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });
        return Resultado.Aberto;
    }

    /// <summary>Ao sair: o cadeado volta a fechar.</summary>
    public static void Fechar(HttpContext context) => context.Response.Cookies.Delete(Cookie);

    private static string Marca(HttpContext context, string hash)
    {
        var actor = context.User.FindFirst(DmoClaimTypes.ActorId)?.Value ?? string.Empty;
        var versao = context.User.FindFirst(DmoClaimTypes.SessionVersion)?.Value ?? string.Empty;
        var pinMarca = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hash)))[..16];
        return $"{actor}|{versao}|{pinMarca}";
    }
}
