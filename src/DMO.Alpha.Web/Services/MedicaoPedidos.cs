using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// PERF-01: a medição de cada pedido (o tempo total, quantas consultas à base
/// e quanto tempo levaram, e o tempo a abrir a ligação). Vive nas Features do
/// pedido; o interceptor do EF soma-lhe cada comando. Só guarda números,
/// nunca o SQL, os parâmetros ou os dados.
/// </summary>
public sealed class MedicaoPedido
{
    private long _consultas;
    private long _bdTicks;
    private long _ligacaoTicks;

    public long Consultas => Interlocked.Read(ref _consultas);
    public TimeSpan TempoBd => TimeSpan.FromTicks(Interlocked.Read(ref _bdTicks));
    public TimeSpan TempoLigacao => TimeSpan.FromTicks(Interlocked.Read(ref _ligacaoTicks));

    public void Comando(TimeSpan duracao)
    {
        Interlocked.Increment(ref _consultas);
        Interlocked.Add(ref _bdTicks, duracao.Ticks);
    }

    public void Ligacao(TimeSpan duracao) => Interlocked.Add(ref _ligacaoTicks, duracao.Ticks);
}

/// <summary>
/// Soma cada comando do EF (e cada abertura de ligação) à medição do pedido
/// atual. Fora de um pedido (arranque, tarefas) não faz nada.
/// </summary>
public sealed class MedicaoPedidoInterceptor(IHttpContextAccessor accessor) : IDbCommandInterceptor, IDbConnectionInterceptor
{
    private MedicaoPedido? Atual => accessor.HttpContext?.Features.Get<MedicaoPedido>();

    private T Comando<T>(CommandEndEventData data, T result)
    {
        Atual?.Comando(data.Duration);
        return result;
    }

    private ValueTask<T> ComandoAsync<T>(CommandEndEventData data, T result)
    {
        Atual?.Comando(data.Duration);
        return ValueTask.FromResult(result);
    }

    public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) => Comando(eventData, result);
    public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) => ComandoAsync(eventData, result);
    public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result) => Comando(eventData, result);
    public ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default) => ComandoAsync(eventData, result);
    public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result) => Comando(eventData, result);
    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default) => ComandoAsync(eventData, result);
    public void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Atual?.Comando(eventData.Duration);

    public Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Atual?.Comando(eventData.Duration);
        return Task.CompletedTask;
    }

    public void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Atual?.Ligacao(eventData.Duration);

    public Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Atual?.Ligacao(eventData.Duration);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mede o pedido completo: põe o cabeçalho Server-Timing (db, ligação, app e
/// total) e escreve uma linha no log por pedido, com a rota (o modelo da
/// rota, nunca o caminho com valores), o estado, os tempos e o número de
/// consultas. Desliga-se com Dmo:MedicaoPedidos:Ligada=false. Os ficheiros
/// estáticos são servidos antes e não passam por aqui.
/// </summary>
public static partial class MedicaoPedidos
{
    public const string ChaveLigada = "Dmo:MedicaoPedidos:Ligada";

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,39}$")]
    private static partial Regex NomeDoHandler();

    public static IApplicationBuilder UseMedicaoPedidos(this IApplicationBuilder app)
    {
        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
        if (!config.GetValue(ChaveLigada, true))
            return app;

        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("DMO.Medicao");
        return app.Use(async (context, next) =>
        {
            var medicao = new MedicaoPedido();
            context.Features.Set(medicao);
            var inicio = Stopwatch.GetTimestamp();

            context.Response.OnStarting(() =>
            {
                var total = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
                var bd = medicao.TempoBd.TotalMilliseconds;
                var ligacao = medicao.TempoLigacao.TotalMilliseconds;
                var appMs = Math.Max(0, total - bd - ligacao);
                context.Response.Headers.Append("Server-Timing", string.Join(", ",
                    $"db;dur={Ms(bd)};desc=\"{medicao.Consultas} consultas\"",
                    $"conn;dur={Ms(ligacao)};desc=\"abrir ligacao\"",
                    $"app;dur={Ms(appMs)}",
                    $"total;dur={Ms(total)}"));
                return Task.CompletedTask;
            });

            var falhou = false;
            try
            {
                await next();
            }
            catch
            {
                // A exceção segue para o tratamento de erros; a linha diz 500.
                falhou = true;
                throw;
            }
            finally
            {
                var total = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
                logger.LogInformation(
                    "Pedido {Metodo} {Rota} handler={Handler} -> {Estado} em {TotalMs} ms; {Consultas} consultas, bd {BdMs} ms, ligação {LigacaoMs} ms [{CorrelationId}]",
                    context.Request.Method,
                    Rota(context),
                    Handler(context),
                    falhou ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                    Math.Round(total, 1),
                    medicao.Consultas,
                    Math.Round(medicao.TempoBd.TotalMilliseconds, 1),
                    Math.Round(medicao.TempoLigacao.TotalMilliseconds, 1),
                    // O trace id: a parte do meio da «Referência do erro» (00-<trace id>-…).
                    Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier);
            }
        });
    }

    // Só ASCII nos valores do cabeçalho: o Kestrel recusa outros caracteres.
    private static string Ms(double ms) => ms.ToString("0.0", CultureInfo.InvariantCulture);

    // O modelo da rota (ex.: «/Ferramentas/Ficha/{id?}»), nunca o caminho
    // pedido: o caminho pode levar referências e ids.
    private static string Rota(HttpContext context) => context.GetEndpoint() switch
    {
        RouteEndpoint { RoutePattern.RawText: { } modelo } => "/" + modelo.TrimStart('/'),
        { } endpoint when endpoint.Metadata.GetMetadata<CompiledPageActionDescriptor>() is { } page => page.ViewEnginePath,
        { DisplayName: { } nome } => nome,
        _ => "(sem rota)"
    };

    // Só um nome de handler simples; outro valor escrito no pedido não vai para o log.
    private static string Handler(HttpContext context)
    {
        var valor = context.GetRouteValue("handler")?.ToString() ?? context.Request.Query["handler"].ToString();
        if (string.IsNullOrEmpty(valor))
            return "-";
        return NomeDoHandler().IsMatch(valor) ? valor : "(outro)";
    }
}
