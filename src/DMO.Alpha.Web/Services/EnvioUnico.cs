using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// UM ENVIO GRAVA UMA VEZ (deep sweep, 18/10/2026: B-boquilhas-2,
/// B-pegamentos-3, B-peso-3, B-peso-4). A ligação caía depois de o servidor
/// gravar, o ecrã ficava com o valor escrito e um novo clique gravava outra
/// vez. O componente das páginas (DmoTroca) dá a cada envio uma chave, no
/// cabeçalho <c>X-Dmo-Envio</c>, e o mesmo envio outra vez (o operador carrega
/// outra vez no botão depois de a ligação falhar) leva a mesma chave. Num
/// handler marcado com este atributo, a primeira vez grava e o redirect da
/// resposta (PRG) fica lembrado durante <see cref="EnviosGravados.Janela"/>:
/// um POST com uma chave já gravada não corre o handler e recebe o mesmo
/// redirect; um que chega enquanto o primeiro ainda grava espera por ele.
/// Sem o cabeçalho (o envio nativo do browser) nada muda. Ver
/// <see cref="EnvioUnicoFilter"/> e docs/ui-reference/atualizar-so-o-que-muda.md.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EnvioUnicoAttribute : Attribute;

/// <summary>
/// O filtro de <see cref="EnvioUnicoAttribute"/>, registado para todas as
/// páginas: só atua num POST com <c>X-Dmo-Envio</c> a um handler marcado.
/// A chave conta por pessoa, página e handler. Corre depois da entrada no
/// módulo (<c>ModuleEntry</c>, ordem 0): quem não entra não grava nem repete.
/// Só se lembra de um redirect: uma resposta desenhada (uma recusa com
/// <c>Page()</c>) ou um erro não ficam, e o mesmo envio outra vez corre o
/// handler como da primeira vez.
/// </summary>
public sealed partial class EnvioUnicoFilter : IAsyncPageFilter, IOrderedFilter
{
    /// <summary>O cabeçalho com a chave do envio (DmoTroca).</summary>
    public const string Cabecalho = "X-Dmo-Envio";

    public int Order => 1;

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var chave = request.Headers[Cabecalho].ToString();
        if (!HttpMethods.IsPost(request.Method)
            || context.HandlerMethod is not { } handler
            || !handler.MethodInfo.IsDefined(typeof(EnvioUnicoAttribute), inherit: false)
            || !ChaveValida().IsMatch(chave))
        {
            await next();
            return;
        }

        var quem = context.HttpContext.User.FindFirst(DmoClaimTypes.ProviderUserId)?.Value ?? string.Empty;
        var envios = context.HttpContext.RequestServices.GetRequiredService<EnviosGravados>();
        var envio = envios.De($"{quem}\n{context.ActionDescriptor.ViewEnginePath}\n{handler.Name}\n{chave}");
        await envio.Vez.WaitAsync(context.HttpContext.RequestAborted);
        try
        {
            if (envio.Resposta is { } primeira)
            {
                context.Result = primeira;
                return;
            }

            var executado = await next();
            if (executado.Exception is null
                && executado.Result is RedirectToPageResult or RedirectResult or LocalRedirectResult or RedirectToRouteResult or RedirectToActionResult)
            {
                envio.Resposta = executado.Result;
            }
        }
        finally
        {
            envio.Vez.Release();
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    private static partial Regex ChaveValida();
}

/// <summary>
/// Os envios gravados (ver <see cref="EnvioUnicoAttribute"/>), em memória: a
/// aplicação corre numa só instância e não é preciso uma tabela. Cada chave
/// fica <see cref="Janela"/> depois do primeiro envio; um reinício esquece-as.
/// </summary>
public sealed class EnviosGravados
{
    /// <summary>
    /// Quanto tempo uma chave fica lembrada: o operador pode carregar outra vez
    /// no botão bastante depois, quando a ligação volta.
    /// </summary>
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, Envio> _envios = new(StringComparer.Ordinal);
    private long _proximaLimpeza;

    /// <summary>Um envio: um de cada vez (Vez) e a resposta do primeiro que gravou.</summary>
    public sealed class Envio
    {
        internal Envio(DateTime desde) => Desde = desde;

        internal DateTime Desde { get; }

        public SemaphoreSlim Vez { get; } = new(1, 1);

        public IActionResult? Resposta { get; set; }
    }

    /// <summary>O envio desta chave (novo, se ainda não existe).</summary>
    public Envio De(string chave)
    {
        var agora = DateTime.UtcNow;
        Limpar(agora);
        return _envios.GetOrAdd(chave, _ => new Envio(agora));
    }

    // Uma vez por minuto, saem as chaves com mais de Janela que ninguém está a usar.
    private void Limpar(DateTime agora)
    {
        var proxima = Interlocked.Read(ref _proximaLimpeza);
        if (agora.Ticks < proxima
            || Interlocked.CompareExchange(ref _proximaLimpeza, agora.AddMinutes(1).Ticks, proxima) != proxima)
        {
            return;
        }

        foreach (var (chave, envio) in _envios)
        {
            if (envio.Desde < agora - Janela && envio.Vez.CurrentCount == 1)
            {
                _envios.TryRemove(chave, out _);
            }
        }
    }
}
