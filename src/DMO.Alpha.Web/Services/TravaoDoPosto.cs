using Microsoft.Extensions.Caching.Memory;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// O travão do posto da Reparação Interna (vistoria ao backend, 27/10/2026):
/// o posto abre sem sessão e o PIN pode ter só 4 algarismos. A conta nunca
/// fica bloqueada (Diogo, 26/10/2026: o PIN certo entra sempre); o que se
/// trava é a origem do pedido. Cada origem confere um PIN de cada vez (os
/// pedidos ao mesmo tempo esperam pela vez) e, a partir da 5.ª falha, cada
/// tentativa espera antes de conferir: 2 s, 4 s, 8 s, 16 s e depois 30 s.
/// As falhas esquecem-se 15 minutos depois da última tentativa.
/// </summary>
public sealed class TravaoDoPosto
{
    public const int FalhasSemEspera = 5;

    public static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Memoria = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;
    private readonly Func<TimeSpan, CancellationToken, Task> _esperar;

    public TravaoDoPosto(IMemoryCache cache)
        : this(cache, Task.Delay)
    {
    }

    /// <summary>Com outra forma de esperar (os testes não esperam de verdade).</summary>
    public TravaoDoPosto(IMemoryCache cache, Func<TimeSpan, CancellationToken, Task> esperar)
    {
        _cache = cache;
        _esperar = esperar;
    }

    /// <summary>A espera antes de conferir, pelo número de falhas da origem.</summary>
    public static TimeSpan Espera(int falhas) =>
        falhas < FalhasSemEspera
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(Math.Min(EsperaMaxima.TotalSeconds, Math.Pow(2, falhas - FalhasSemEspera + 1)));

    /// <summary>
    /// Espera pela vez da origem e pela espera das falhas. A vez acaba no
    /// <see cref="Vez.Dispose"/>; um PIN errado conta com <see cref="Vez.Falhou"/>.
    /// </summary>
    public async Task<Vez> EsperarVezAsync(string origem, CancellationToken cancellationToken)
    {
        var estado = _cache.GetOrCreate("dmo-posto-origem:" + origem, entrada =>
        {
            entrada.SlidingExpiration = Memoria;
            return new Estado();
        })!;

        await estado.Trinco.WaitAsync(cancellationToken);
        try
        {
            var espera = Espera(Volatile.Read(ref estado.Falhas));
            if (espera > TimeSpan.Zero)
            {
                await _esperar(espera, cancellationToken);
            }
        }
        catch
        {
            estado.Trinco.Release();
            throw;
        }

        return new Vez(estado);
    }

    public sealed class Vez : IDisposable
    {
        private Estado? _estado;

        internal Vez(Estado estado) => _estado = estado;

        public void Falhou()
        {
            if (_estado is { } estado)
            {
                Interlocked.Increment(ref estado.Falhas);
            }
        }

        public void Dispose() => Interlocked.Exchange(ref _estado, null)?.Trinco.Release();
    }

    internal sealed class Estado
    {
        public readonly SemaphoreSlim Trinco = new(1, 1);

        public int Falhas;
    }
}
