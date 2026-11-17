using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Uma chave do SAP recusada em /sap/enviar ou /sap/codigos fica no log
/// (audit externo, 16/11/2026, DMO-759), sem a chave escrita.
/// </summary>
public sealed class SapChaveRecusadaLogTests
{
    [Fact]
    public async Task ChaveErrada_FicaNoLog_SemAChave()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        var logs = new Captura();
        using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        using var anonimo = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string chaveErrada = "CHAVE-ERRADA-0123456789";

        foreach (var (metodo, caminho) in new[] { (HttpMethod.Post, "/sap/enviar?ficheiro=x.csv"), (HttpMethod.Get, "/sap/codigos") })
        {
            using var pedido = new HttpRequestMessage(metodo, caminho);
            if (metodo == HttpMethod.Post) pedido.Content = new StringContent("a,b\r\n");
            pedido.Headers.Add("X-DMO-SAP", chaveErrada);
            using var resposta = await anonimo.SendAsync(pedido);
            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        }

        var recusas = logs.Entradas.Where(e => e.Categoria == "DMO.Sap").Select(e => e.Mensagem).ToList();
        Assert.Contains(recusas, m => m.Contains("recusada em /sap/enviar", StringComparison.Ordinal));
        Assert.Contains(recusas, m => m.Contains("recusada em /sap/codigos", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entradas, e => e.Mensagem.Contains(chaveErrada, StringComparison.Ordinal));
    }

    private sealed class Captura : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Categoria, string Mensagem)> _entradas = new();
        public IReadOnlyList<(string Categoria, string Mensagem)> Entradas => _entradas.ToArray();
        public ILogger CreateLogger(string categoryName) => new Registo(categoryName, _entradas);
        public void Dispose() { }

        private sealed class Registo(string categoria, ConcurrentQueue<(string, string)> entradas) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entradas.Enqueue((categoria, formatter(state, exception)));
        }
    }
}
