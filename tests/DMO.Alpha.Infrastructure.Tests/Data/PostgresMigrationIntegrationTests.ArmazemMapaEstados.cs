using System.Data.Common;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // PERF-06: o mapa do Armazém lia todos os estados das ferramentas ocupantes
    // e escolhia o último (maior Id) em memória. Agora a base escolhe o último
    // de cada ferramenta (índice ToolId+Id): as linhas lidas seguem as
    // ferramentas presentes, não a sua história. O mapa sai igual.
    [SkippableFact]
    public async Task ArmazemMapa_UltimoEstadoNaBase_IgualAoDeMemoria_ELinhasLidasNaoDependemDaHistoria()
    {
        SkipWithoutConnection();
        var marker = "P6" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // Ferramentas com 0, 1 e muitos estados (o último nem sempre é o de maior RegistadoEmUtc).
        var historias = new (string Sufixo, FerramentaEstadoTipo[] Estados)[]
        {
            ("zero", []),
            ("um", [FerramentaEstadoTipo.PorReparar]),
            ("muitos", [FerramentaEstadoTipo.Novo, FerramentaEstadoTipo.PorReparar, FerramentaEstadoTipo.Reparado, FerramentaEstadoTipo.PorReparar, FerramentaEstadoTipo.Sucatado, FerramentaEstadoTipo.Reparado]),
            ("outra", [FerramentaEstadoTipo.Reparado, FerramentaEstadoTipo.PorReparar])
        };
        var existentes = (await db.ArmazemPosicoes.Select(p => p.Codigo).ToListAsync()).ToHashSet();
        var codigos = Enumerable.Range(9000, 1000).Select(n => n.ToString()).Where(c => !existentes.Contains(c)).Take(5).ToList();
        var posicoes = codigos.Select((c, i) => new ArmazemPosicao { Codigo = c, Ativa = i != 4, CriadaEmUtc = DateTime.UtcNow }).ToList();
        db.ArmazemPosicoes.AddRange(posicoes);
        var ferramentas = historias.Select((h, i) => new Tool
        {
            ToolId = $"{marker}-{h.Sufixo}",
            Type = i % 2 == 0 ? ToolType.Cm : ToolType.Mf,
            Reference = marker + "R",
            Lot = i.ToString()
        }).ToList();
        // Uma ferramenta fora das posições, com estados: não deve aparecer nem ser lida.
        var fora = new Tool { ToolId = marker + "-fora", Type = ToolType.Bq, Reference = marker + "F", Lot = "0" };
        db.Tools.AddRange(ferramentas.Append(fora));
        await db.SaveChangesAsync();

        var agora = DateTime.UtcNow;
        var movimentos = new List<ArmazemMovimento>();
        foreach (var t in ferramentas)
        {
            var m = new ArmazemMovimento
            {
                Tipo = ArmazemMovimentoTipo.Entrada, ToolId = t.ToolId, OcorridoEmUtc = agora,
                RegistadoEmUtc = agora, OperacaoId = Guid.NewGuid()
            };
            movimentos.Add(m);
        }

        db.ArmazemMovimentos.AddRange(movimentos);
        await db.SaveChangesAsync();
        // zero e um na posição 0 (ativa); muitos na 4 (arquivada, ainda ocupada); outra na 1.
        var onde = new[] { 0, 0, 4, 1 };
        for (var i = 0; i < ferramentas.Count; i++)
        {
            db.ArmazemOcupacoes.Add(new ArmazemOcupacao
            {
                ToolId = ferramentas[i].ToolId, PosicaoId = posicoes[onde[i]].Id,
                MovimentoId = movimentos[i].Id, DesdeUtc = agora
            });
        }

        for (var i = 0; i < historias.Length; i++)
        {
            foreach (var e in historias[i].Estados)
            {
                db.FerramentaEstados.Add(new FerramentaEstado { ToolId = ferramentas[i].ToolId, Estado = e, RegistadoEmUtc = agora });
            }
        }

        db.FerramentaEstados.AddRange(Enumerable.Range(0, 5).Select(_ =>
            new FerramentaEstado { ToolId = fora.ToolId, Estado = FerramentaEstadoTipo.Novo, RegistadoEmUtc = agora }));
        await db.SaveChangesAsync();

        async Task<(ArmazemMapaModelo Mapa, long Linhas)> Corre()
        {
            var espia = new EspiaEstados();
            await using var ctx = CreateContextWith(espia);
            var mapa = await new ArmazemMapa(ctx).MapaAsync(ArmazemArmarios.Todos, ArmazemMapaFiltro.Todas);
            return (mapa, await espia.LinhasLidasAsync(Connection!));
        }

        // Esperado: o último (maior Id) de cada ferramenta, calculado em memória.
        var esperado = new Dictionary<string, FerramentaEstadoTipo?>();
        foreach (var t in ferramentas)
        {
            esperado[t.ToolId] = (await db.FerramentaEstados.AsNoTracking().Where(e => e.ToolId == t.ToolId).ToListAsync())
                .MaxBy(e => e.Id)?.Estado;
        }

        var (mapa, linhas) = await Corre();
        var nossas = mapa.Lista.Where(p => codigos.Contains(p.Codigo)).ToList();
        Assert.Equal(codigos.Count, nossas.Count); // a arquivada continua, por estar ocupada
        var lidas = nossas.SelectMany(p => p.Ferramentas).Where(f => f.ToolId.StartsWith(marker, StringComparison.Ordinal)).ToDictionary(f => f.ToolId);
        Assert.Equal(ferramentas.Count, lidas.Count);
        Assert.DoesNotContain(marker + "-fora", lidas.Keys);
        foreach (var t in ferramentas)
        {
            Assert.Equal(esperado[t.ToolId], lidas[t.ToolId].Estado);
        }

        Assert.Null(lidas[marker + "-zero"].Estado);
        Assert.Equal(FerramentaEstadoTipo.PorReparar, lidas[marker + "-um"].Estado);
        Assert.Equal(FerramentaEstadoTipo.Reparado, lidas[marker + "-muitos"].Estado);
        Assert.Equal(FerramentaEstadoTipo.PorReparar, lidas[marker + "-outra"].Estado);
        Assert.Contains(nossas, p => p.Codigo == codigos[3] && p.Livre && p.Ativa);
        Assert.Single(nossas, p => !p.Ativa && !p.Livre);

        // Filtros e contagens iguais: Ocupadas / PorReparar.
        await using (var ctx = CreateContext())
        {
            var por = await new ArmazemMapa(ctx).MapaAsync(ArmazemArmarios.Todos, ArmazemMapaFiltro.PorReparar);
            var codigosPor = por.Lista.Select(p => p.Codigo).Where(codigos.Contains).OrderBy(c => c, StringComparer.Ordinal).ToList();
            Assert.Equal(mapa.Lista.Where(p => codigos.Contains(p.Codigo) && p.PorReparar).Select(p => p.Codigo).OrderBy(c => c, StringComparer.Ordinal), codigosPor);
        }

        // Linhas lidas dos estados: as de cada ferramenta ocupante, uma. Mais
        // 100 estados na história de «muitos» não mudam o número lido.
        Assert.Equal(await ContaOcupantes(), linhas);
        db.FerramentaEstados.AddRange(Enumerable.Range(0, 100).Select(i => new FerramentaEstado
        {
            ToolId = ferramentas[2].ToolId, Estado = i % 2 == 0 ? FerramentaEstadoTipo.Novo : FerramentaEstadoTipo.PorReparar, RegistadoEmUtc = agora
        }));
        db.FerramentaEstados.Add(new FerramentaEstado { ToolId = ferramentas[2].ToolId, Estado = FerramentaEstadoTipo.Sucatado, RegistadoEmUtc = agora });
        await db.SaveChangesAsync();
        var (mapa2, linhas2) = await Corre();
        Assert.Equal(linhas, linhas2);
        Assert.Equal(
            FerramentaEstadoTipo.Sucatado,
            mapa2.Lista.SelectMany(p => p.Ferramentas).Single(f => f.ToolId == marker + "-muitos").Estado);

        async Task<long> ContaOcupantes() =>
            await db.ArmazemOcupacoes.CountAsync(o => o.LibertadaEmUtc == null);
    }

    /// <summary>
    /// Apanha o comando que lê <c>FerramentaEstados</c> e conta, a seguir, as
    /// linhas que ele devolve (a consulta repetida dentro de um count).
    /// </summary>
    private sealed class EspiaEstados : DbCommandInterceptor
    {
        private string? _sql;
        private readonly List<(string Nome, object? Valor)> _parametros = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Apanha(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Apanha(command);
            return ValueTask.FromResult(result);
        }

        private void Apanha(DbCommand command)
        {
            if (!command.CommandText.Contains("\"FerramentaEstados\"", StringComparison.Ordinal))
            {
                return;
            }

            _sql = command.CommandText;
            _parametros.Clear();
            foreach (DbParameter p in command.Parameters)
            {
                _parametros.Add((p.ParameterName, p.Value));
            }
        }

        public async Task<long> LinhasLidasAsync(string connection)
        {
            Assert.NotNull(_sql);
            await using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM ({_sql.TrimEnd().TrimEnd(';')}) AS q", conn);
            foreach (var (nome, valor) in _parametros)
            {
                cmd.Parameters.Add(new NpgsqlParameter(nome, valor));
            }

            return (long)(await cmd.ExecuteScalarAsync())!;
        }
    }
}
