using System.Globalization;
using System.Reflection;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// Os movimentos do Armazém sobre o <see cref="DmoDbContext"/> (blueprint
/// armazem/OVERVIEW §§1–3). Cada movimento grava, na mesma transação, o
/// movimento, a ocupação, a localização, o estado escolhido e a operação
/// registada (envio único). Só se recusa o impossível (entrar o que já está,
/// tirar o que não está) e o que falta; a data no futuro ou anterior ao
/// último movimento, a posição arquivada ou com outra ferramenta e a entrada
/// depois da Sucata gravam e avisam (regras da beta).
/// <para>
/// Dois movimentos da mesma ferramenta ao mesmo tempo: a ocupação ativa é
/// única por ferramenta e a ocupação e a localização têm versão (xmin), por
/// isso o segundo falha na base, volta a ler e recebe a recusa certa (ou «já
/// registado», quando é o mesmo envio).
/// </para>
/// </summary>
public sealed class ArmazemMovimentos(DmoDbContext db, IOperacoesRegistadas operacoes, ICurrentAccountContext account) : IArmazemMovimentos
{
    public const string OperacaoTipo = "armazem.movimento";

    public const string ResultadoTipo = "ArmazemMovimento";

    /// <summary>Quantas ferramentas a procura mostra de cada vez.</summary>
    public const int ProcuraLimite = 10;

    /// <summary>Quantos movimentos a ficha do Armazém mostra.</summary>
    public const int UltimosLimite = 5;

    // Uma data até uns minutos à frente é o relógio do PC, não um engano.
    private static readonly TimeSpan FolgaDoFuturo = TimeSpan.FromMinutes(5);

    private static readonly string? Versao = VersaoDaApp();

    public async Task<IReadOnlyList<ArmazemProcuraItem>> ProcurarAsync(string? texto, CancellationToken cancellationToken = default)
    {
        if (ReferenceMatch.Normalize(texto) is not { } wanted)
        {
            return [];
        }

        var naPosicao = db.ArmazemOcupacoes
            .Where(o => o.LibertadaEmUtc == null && db.ArmazemPosicoes.Any(p => p.Id == o.PosicaoId && p.Codigo == wanted))
            .Select(o => o.ToolId);
        var tools = await db.Tools.AsNoTracking()
            .Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Where(ReferenceMatch.Predicate<Tool>(t => t.Reference, wanted)
                .Or(t => t.Lot.ToUpper().Contains(wanted) || naPosicao.Contains(t.ToolId)))
            .OrderBy(t => t.Reference).ThenBy(t => t.Lot).ThenBy(t => t.Type)
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .Take(ProcuraLimite)
            .ToListAsync(cancellationToken);
        var ids = tools.Select(t => t.ToolId).ToList();
        var posicoes = await PosicoesAtuaisAsync(ids, cancellationToken);
        var localizacoes = await db.FerramentaLocalizacoes.AsNoTracking()
            .Where(l => ids.Contains(l.ToolId))
            .ToDictionaryAsync(l => l.ToolId, cancellationToken);
        var estados = (await db.FerramentaEstados.AsNoTracking()
                .Where(e => ids.Contains(e.ToolId))
                .Select(e => new { e.Id, e.ToolId, e.Estado })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.ToolId)
            .ToDictionary(g => g.Key, g => g.MaxBy(e => e.Id)!.Estado);
        var maquinas = (await db.ToolMachineLinks.AsNoTracking()
                .Where(m => ids.Contains(m.ToolId))
                .Select(m => new { m.ToolId, m.Machine.Code })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.ToolId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(m => m.Code).Order(StringComparer.OrdinalIgnoreCase).ToList());
        var usos = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => ids.Contains(u.ToolId))
                .Select(u => new { u.Id, u.ToolId, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => g.MaxBy(u => u.Id)!.Percentagem);

        return tools.Select(t =>
        {
            var noArmazem = posicoes.TryGetValue(t.ToolId, out var posicao);
            var ondeEsta = noArmazem
                ? $"Armazém · posição {posicao.Codigo}"
                : localizacoes.TryGetValue(t.ToolId, out var l)
                    ? ArmazemTextos.Localizacao(l.Localizacao, l.ReparadorNome)
                    : ArmazemTextos.Localizacao(FerramentaLocalizacaoTipo.Fabrico, null);
            return new ArmazemProcuraItem(t.ToolId, t.Type, t.Reference, t.Lot, ondeEsta, noArmazem,
                estados.TryGetValue(t.ToolId, out var estado) ? estado : null,
                maquinas.TryGetValue(t.ToolId, out var m) ? m : [],
                usos.TryGetValue(t.ToolId, out var uso) ? uso : null);
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, ArmazemLinhasEUso>> LinhasEUsoAsync(IReadOnlyCollection<string> toolIds, CancellationToken cancellationToken = default)
    {
        var ids = toolIds.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, ArmazemLinhasEUso>();
        }

        var maquinas = (await db.ToolMachineLinks.AsNoTracking()
                .Where(m => ids.Contains(m.ToolId))
                .Select(m => new { m.ToolId, m.Machine.Code })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.ToolId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(m => m.Code).Order(StringComparer.OrdinalIgnoreCase).ToList());
        var usos = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => ids.Contains(u.ToolId))
                .Select(u => new { u.Id, u.ToolId, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => g.MaxBy(u => u.Id)!.Percentagem);
        return ids.ToDictionary(id => id, id => new ArmazemLinhasEUso(
            maquinas.TryGetValue(id, out var m) ? m : [],
            usos.TryGetValue(id, out var u) ? u : null), StringComparer.Ordinal);
    }

    public async Task<ArmazemSituacao?> SituacaoAsync(string? toolId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId) || toolId == ToolRegistryConstants.RemovedToolId)
        {
            return null;
        }

        var tool = await db.Tools.AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .FirstOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            return null;
        }

        var posicoes = await PosicoesAtuaisAsync([tool.ToolId], cancellationToken);
        (string Codigo, DateTime DesdeUtc)? posicao = posicoes.TryGetValue(tool.ToolId, out var p) ? p : null;
        var localizacao = await db.FerramentaLocalizacoes.AsNoTracking()
            .FirstOrDefaultAsync(l => l.ToolId == tool.ToolId, cancellationToken);
        var estado = await db.FerramentaEstados.AsNoTracking()
            .Where(e => e.ToolId == tool.ToolId)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var ultimos = await db.ArmazemMovimentos.AsNoTracking()
            .Where(m => m.ToolId == tool.ToolId)
            .OrderByDescending(m => m.Id)
            .Take(UltimosLimite)
            .ToListAsync(cancellationToken);
        var codigos = await CodigosAsync(ultimos.SelectMany(m => new[] { m.PosicaoId, m.PosicaoAnteriorId }), cancellationToken);

        // A Entrada propõe pela última Saída, quando a ferramenta está fora
        // por uma Saída (e não por um Corrigir): a origem e o estado (D11).
        var ultimo = ultimos.FirstOrDefault();
        var saida = posicao is null && ultimo is { Tipo: ArmazemMovimentoTipo.Saida } ? ultimo : null;
        var origem = saida?.Destino switch
        {
            ArmazemDestino.Producao => ArmazemOrigem.Producao,
            ArmazemDestino.Reparacao => ArmazemOrigem.Reparacao,
            ArmazemDestino.Emprestado => ArmazemOrigem.Emprestimo,
            _ => (ArmazemOrigem?)null
        };
        var estadoProposto = saida?.Destino switch
        {
            ArmazemDestino.Producao => FerramentaEstadoTipo.PorReparar,
            ArmazemDestino.Reparacao => FerramentaEstadoTipo.Reparado,
            _ => (FerramentaEstadoTipo?)null
        };

        // Numa lista ativa da Reparação Programada, a Entrada propõe «Reparado».
        var listaProgramada = posicao is null
            ? await DMO.Alpha.Infrastructure.Programada.ReparacaoProgramadaMovimentos.ListaAtivaAsync(db, tool.ToolId, cancellationToken)
            : null;
        if (listaProgramada is not null)
        {
            estadoProposto = FerramentaEstadoTipo.Reparado;
        }

        var par = await ParAsync(tool.ToolId, tool.Type, tool.Reference, tool.Lot, cancellationToken);
        var parPosicao = par is null ? null : (await PosicoesAtuaisAsync([par.ToolId], cancellationToken)).GetValueOrDefault(par.ToolId).Codigo;

        return new ArmazemSituacao(
            tool.ToolId,
            tool.Type,
            tool.Reference,
            tool.Lot,
            posicao?.Codigo,
            posicao?.DesdeUtc,
            localizacao?.Localizacao ?? FerramentaLocalizacaoTipo.Fabrico,
            localizacao?.ReparadorNome,
            localizacao?.DesdeUtc,
            estado?.Estado,
            estado?.RegistadoEmUtc,
            saida?.Destino,
            saida?.ReparadorNome,
            origem,
            estadoProposto,
            await ReparadoresAsync(tool.Type, cancellationToken),
            ultimos.Select(m => new ArmazemMovimentoLinha(
                m.Id, m.Tipo, m.OcorridoEmUtc, Descricao(m, codigos), m.Observacoes, m.RegistadoPor)).ToList())
        {
            Par = par is null ? null : new ArmazemPar(par.ToolId, par.Type, parPosicao),
            ListaProgramada = listaProgramada,
            Linhas = await LinhasAsync(cancellationToken),
            LinhasDaFerramenta = await db.ToolMachineLinks.AsNoTracking()
                .Where(l => l.ToolId == tool.ToolId).Select(l => l.MachineId).ToListAsync(cancellationToken),
            VindaLinhaId = saida?.MachineId,
            VindaReparador = saida?.ReparadorNome
        };
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<ArmazemOcupante>>> OcupantesAsync(
        IReadOnlyCollection<int> posicaoIds, CancellationToken cancellationToken = default)
    {
        if (posicaoIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<ArmazemOcupante>>();
        }

        var linhas = await (
                from o in db.ArmazemOcupacoes.AsNoTracking()
                join t in db.Tools.AsNoTracking() on o.ToolId equals t.ToolId
                where o.LibertadaEmUtc == null && posicaoIds.Contains(o.PosicaoId)
                orderby o.DesdeUtc, o.Id
                select new { o.PosicaoId, t.ToolId, t.Type, t.Reference, t.Lot, o.DesdeUtc })
            .ToListAsync(cancellationToken);
        return linhas
            .GroupBy(l => l.PosicaoId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ArmazemOcupante>)g.Select(l => new ArmazemOcupante(l.ToolId, l.Type, l.Reference, l.Lot, l.DesdeUtc)).ToList());
    }

    public Task<ArmazemMovimentoSaida> EntradaAsync(PedidoDeEntrada pedido, CancellationToken cancellationToken = default) =>
        ComRepeticaoAsync(pedido.OperacaoId, () => EntradaUmaVezAsync(pedido, cancellationToken), cancellationToken);

    public Task<ArmazemMovimentoSaida> SaidaAsync(PedidoDeSaida pedido, CancellationToken cancellationToken = default) =>
        ComRepeticaoAsync(pedido.OperacaoId, () => SaidaUmaVezAsync(pedido, cancellationToken), cancellationToken);

    public Task<ArmazemMovimentoSaida> CorrigirAsync(PedidoDeCorrecao pedido, CancellationToken cancellationToken = default) =>
        ComRepeticaoAsync(pedido.OperacaoId, () => CorrigirUmaVezAsync(pedido, cancellationToken), cancellationToken);

    private async Task<ArmazemMovimentoSaida> EntradaUmaVezAsync(PedidoDeEntrada pedido, CancellationToken cancellationToken)
    {
        if (await JaRegistadoAsync(pedido.OperacaoId, cancellationToken) is { } repetido)
        {
            return repetido;
        }

        var tool = await FerramentaAsync(pedido.ToolId, cancellationToken);
        if (tool is null)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.FerramentaNaoExiste);
        }

        var atual = await OcupacaoAtivaAsync(tool.ToolId, cancellationToken);
        if (atual is not null)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.JaNoArmazem, await CodigoAsync(atual.PosicaoId, cancellationToken));
        }

        var (recusa, posicao, codigo) = await PosicaoAsync(pedido.Posicao, pedido.CriarPosicao, cancellationToken);
        if (recusa is { } r)
        {
            return ArmazemMovimentoSaida.Recusa(r, codigo);
        }

        if (pedido.Origem is null)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.SemOrigem, codigo);
        }

        if (pedido.Estado is null)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.SemEstado, codigo);
        }

        if (!Observacoes(pedido.Observacoes, out var observacoes))
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.ObservacoesLongas, codigo);
        }

        var agora = DateTime.UtcNow;
        var ocorrido = pedido.OcorridoEmUtc ?? agora;
        var avisos = new List<string>();
        await AvisosDaDataAsync(tool.ToolId, ocorrido, agora, avisos, cancellationToken);
        await AvisosDaPosicaoAsync(posicao!, tool, avisos, cancellationToken);
        var localizacao = await db.FerramentaLocalizacoes.FirstOrDefaultAsync(l => l.ToolId == tool.ToolId, cancellationToken);
        if (pedido.Estado != FerramentaEstadoTipo.Reparado
            && await DMO.Alpha.Infrastructure.Programada.ReparacaoProgramadaMovimentos.ListaAtivaAsync(db, tool.ToolId, cancellationToken) is { } reparadorDaLista)
        {
            avisos.Add($"Esta ferramenta está na Reparação Programada de {reparadorDaLista}: entrou como «{ArmazemTextos.Estado(pedido.Estado.Value)}» em vez de «Reparado».");
        }

        if (localizacao?.Localizacao == FerramentaLocalizacaoTipo.Sucata)
        {
            avisos.Add("Esta ferramenta estava na Sucata. Se foi engano, a entrada fica e a ferramenta volta ao Fabrico.");
        }

        // O par entra com ela quando está fora do armazém; se já lá está,
        // entra só esta e fica o aviso.
        var par = pedido.ComPar ? await ParAsync(tool.ToolId, tool.Type, tool.Reference, tool.Lot, cancellationToken) : null;
        if (par is not null && await OcupacaoAtivaAsync(par.ToolId, cancellationToken) is { } doPar)
        {
            avisos.Add($"{ArmazemTextos.Tipo(par.Type)} {par.Reference} · lote {par.Lot} já está no armazém, na posição {await CodigoAsync(doPar.PosicaoId, cancellationToken)}: entrou só {ArmazemTextos.Tipo(tool.Type)}.");
            par = null;
        }

        // A Linha, o reparador e o Job On com que a ferramenta vem: os da
        // última Saída (a pessoa pode mudar a Linha e o reparador no formulário).
        var vinda = await UltimoMovimentoAsync(tool.ToolId, cancellationToken) is { Tipo: ArmazemMovimentoTipo.Saida } ultimaSaida ? ultimaSaida : null;
        int? linha;
        string? reparadorDaVinda;
        if (pedido.LinhaEReparadorDoFormulario)
        {
            linha = await LinhaValidaAsync(pedido.LinhaId, cancellationToken);
            reparadorDaVinda = Cortado(Limpo(pedido.Reparador), Reparador.NomeMaxLength);
        }
        else
        {
            linha = vinda?.MachineId;
            reparadorDaVinda = pedido.Origem == ArmazemOrigem.Reparacao ? vinda?.ReparadorNome : null;
        }

        var jobOnDaVinda = pedido.Origem is ArmazemOrigem.Producao or ArmazemOrigem.Reparacao ? vinda?.JobOnId : null;

        var movimento = Novo(pedido.OperacaoId, ArmazemMovimentoTipo.Entrada, tool.ToolId, ocorrido, agora, observacoes);
        movimento.Origem = pedido.Origem;
        movimento.Estado = pedido.Estado;
        movimento.MachineId = linha;
        movimento.EntradaReparadorNome = reparadorDaVinda;
        movimento.JobOnId = jobOnDaVinda;
        var saida = await GravarAsync(movimento, posicao, async () =>
        {
            Entrar(tool.ToolId, posicao!, movimento, localizacao, pedido.Estado.Value, agora);
            // Vinda da Produção, a Entrada abre o lembrete de utilização
            // (blueprint armazem/OVERVIEW §2): o script do SAP vai buscá-la.
            if (pedido.Origem == ArmazemOrigem.Producao)
            {
                await DMO.Alpha.Infrastructure.Tools.FerramentasUsoSap.LembrarAsync(db, tool.ToolId, movimento.Id, movimento.RegistadoPor, agora, cancellationToken);
            }

            if (par is not null)
            {
                var movimentoDoPar = Novo(OperacaoDoPar(pedido.OperacaoId), ArmazemMovimentoTipo.Entrada, par.ToolId, ocorrido, agora, observacoes);
                movimentoDoPar.Origem = pedido.Origem;
                movimentoDoPar.Estado = pedido.Estado;
                movimentoDoPar.MachineId = linha;
                movimentoDoPar.EntradaReparadorNome = reparadorDaVinda;
                movimentoDoPar.JobOnId = jobOnDaVinda;
                movimentoDoPar.PosicaoId = posicao!.Id;
                db.ArmazemMovimentos.Add(movimentoDoPar);
                await db.SaveChangesAsync(cancellationToken);
                var localizacaoDoPar = await db.FerramentaLocalizacoes.FirstOrDefaultAsync(l => l.ToolId == par.ToolId, cancellationToken);
                Entrar(par.ToolId, posicao!, movimentoDoPar, localizacaoDoPar, pedido.Estado.Value, agora);
                if (pedido.Origem == ArmazemOrigem.Producao)
                {
                    await DMO.Alpha.Infrastructure.Tools.FerramentasUsoSap.LembrarAsync(db, par.ToolId, movimentoDoPar.Id, movimentoDoPar.RegistadoPor, agora, cancellationToken);
                }
            }
        }, avisos, posicao!.Codigo, cancellationToken);
        return saida with { ComPar = par is not null };
    }

    private void Entrar(string toolId, ArmazemPosicao posicao, ArmazemMovimento movimento, FerramentaLocalizacao? localizacao,
        FerramentaEstadoTipo estado, DateTime agora)
    {
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao
        {
            ToolId = toolId,
            PosicaoId = posicao.Id,
            MovimentoId = movimento.Id,
            DesdeUtc = movimento.OcorridoEmUtc
        });
        Localizar(localizacao, toolId, FerramentaLocalizacaoTipo.Fabrico, null, movimento);
        RegistarEstado(toolId, estado, movimento, agora);
    }

    /// <summary>O par de uma CM ou de um MF: a outra, da mesma referência e lote (a primeira, se houver mais).</summary>
    private Task<Tool?> ParAsync(string toolId, ToolType tipo, string referencia, string lote, CancellationToken cancellationToken)
    {
        if (tipo is not (ToolType.Cm or ToolType.Mf))
        {
            return Task.FromResult<Tool?>(null);
        }

        var outro = tipo == ToolType.Cm ? ToolType.Mf : ToolType.Cm;
        return db.Tools.AsNoTracking()
            .Where(t => t.Type == outro && t.Reference == referencia && t.Lot == lote
                && t.ToolId != toolId && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .OrderBy(t => t.ToolId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>O envio do par deriva do envio da Entrada: o mesmo envio repetido dá o mesmo identificador.</summary>
    internal static Guid OperacaoDoPar(Guid operacaoId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData([.. operacaoId.ToByteArray(), .. "par"u8]);
        return new Guid(bytes.AsSpan(0, 16));
    }

    private async Task<ArmazemMovimentoSaida> SaidaUmaVezAsync(PedidoDeSaida pedido, CancellationToken cancellationToken)
    {
        if (await JaRegistadoAsync(pedido.OperacaoId, cancellationToken) is { } repetido)
        {
            return repetido;
        }

        var tool = await FerramentaAsync(pedido.ToolId, cancellationToken);
        if (tool is null)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.FerramentaNaoExiste);
        }

        var ocupacao = await db.ArmazemOcupacoes
            .FirstOrDefaultAsync(o => o.ToolId == tool.ToolId && o.LibertadaEmUtc == null, cancellationToken);
        if (ocupacao is null)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.NaoEstaNoArmazem);
        }

        var codigo = await CodigoAsync(ocupacao.PosicaoId, cancellationToken);
        if (pedido.Destino is not { } destino)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.SemDestino, codigo);
        }

        int? reparadorId = null;
        int? bqReparadorId = null;
        string? reparadorNome = null;
        if (destino == ArmazemDestino.Reparacao)
        {
            if (pedido.ReparadorId is not { } escolhido)
            {
                return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.SemReparador, codigo);
            }

            var reparador = (await ReparadoresAsync(tool.Type, cancellationToken)).FirstOrDefault(r => r.Id == escolhido);
            if (reparador is null)
            {
                return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.ReparadorInvalido, codigo);
            }

            reparadorNome = reparador.Nome;
            if (tool.Type == ToolType.Bq)
            {
                bqReparadorId = reparador.Id;
            }
            else
            {
                reparadorId = reparador.Id;
            }
        }

        var fabrica = destino == ArmazemDestino.Emprestado ? Limpo(pedido.FabricaDestino) : null;
        if (fabrica is { Length: > ArmazemMovimento.FabricaMaxLength })
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.FabricaLonga, codigo);
        }

        if (!Observacoes(pedido.Observacoes, out var observacoes))
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.ObservacoesLongas, codigo);
        }

        var agora = DateTime.UtcNow;
        var ocorrido = pedido.OcorridoEmUtc ?? agora;
        var avisos = new List<string>();
        await AvisosDaDataAsync(tool.ToolId, ocorrido, agora, avisos, cancellationToken);

        // O estado só se grava na Saída para Sucata, e só o que a pessoa
        // confirmou (D11: a app pré-preenche «Sucatado»).
        var estado = destino == ArmazemDestino.Sucata ? pedido.Estado : null;
        var movimento = Novo(pedido.OperacaoId, ArmazemMovimentoTipo.Saida, tool.ToolId, ocorrido, agora, observacoes);
        movimento.PosicaoAnteriorId = ocupacao.PosicaoId;
        movimento.Destino = destino;
        movimento.ReparadorId = reparadorId;
        movimento.BqReparadorId = bqReparadorId;
        movimento.ReparadorNome = reparadorNome;
        movimento.FabricaDestino = fabrica;
        movimento.Estado = estado;
        // Para a Produção: a Linha escolhida e o Job On dessa ferramenta nessa
        // Linha. Para o resto: a Linha e o Job On com que a ferramenta entrou.
        if (destino == ArmazemDestino.Producao)
        {
            movimento.MachineId = await LinhaValidaAsync(pedido.LinhaId, cancellationToken);
            movimento.JobOnId = movimento.MachineId is { } linhaDaSaida
                ? await JobOnAtivoAsync(tool.ToolId, tool.Type, linhaDaSaida, ocorrido, cancellationToken)
                : null;
        }
        else if (await UltimoMovimentoAsync(tool.ToolId, cancellationToken) is { Tipo: ArmazemMovimentoTipo.Entrada } entrou)
        {
            movimento.MachineId = entrou.MachineId;
            movimento.JobOnId = entrou.JobOnId;
        }

        var localizacao = await db.FerramentaLocalizacoes.FirstOrDefaultAsync(l => l.ToolId == tool.ToolId, cancellationToken);
        return await GravarAsync(movimento, null, async () =>
        {
            ocupacao.LibertadaEmUtc = ocorrido;
            ocupacao.LibertadaPorMovimentoId = movimento.Id;
            var onde = destino switch
            {
                ArmazemDestino.Reparacao => FerramentaLocalizacaoTipo.AReparar,
                ArmazemDestino.Emprestado => FerramentaLocalizacaoTipo.Emprestado,
                ArmazemDestino.Sucata => FerramentaLocalizacaoTipo.Sucata,
                _ => FerramentaLocalizacaoTipo.Fabrico
            };
            Localizar(localizacao, tool.ToolId, onde, reparadorNome, movimento);
            if (estado is { } e)
            {
                RegistarEstado(tool.ToolId, e, movimento, agora);
            }

            await Task.CompletedTask;
        }, avisos, codigo, cancellationToken);
    }

    private async Task<ArmazemMovimentoSaida> CorrigirUmaVezAsync(PedidoDeCorrecao pedido, CancellationToken cancellationToken)
    {
        if (await JaRegistadoAsync(pedido.OperacaoId, cancellationToken) is { } repetido)
        {
            return repetido;
        }

        var tool = await FerramentaAsync(pedido.ToolId, cancellationToken);
        if (tool is null)
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.FerramentaNaoExiste);
        }

        var ocupacao = await db.ArmazemOcupacoes
            .FirstOrDefaultAsync(o => o.ToolId == tool.ToolId && o.LibertadaEmUtc == null, cancellationToken);
        var foraDoArmazem = string.IsNullOrWhiteSpace(pedido.Posicao);
        ArmazemPosicao? posicao = null;
        string? codigo = null;
        if (!foraDoArmazem)
        {
            ArmazemMovimentoResultado? recusa;
            (recusa, posicao, codigo) = await PosicaoAsync(pedido.Posicao, pedido.CriarPosicao, cancellationToken);
            if (recusa is { } r)
            {
                return ArmazemMovimentoSaida.Recusa(r, codigo);
            }
        }

        if ((foraDoArmazem && ocupacao is null) || (posicao is not null && ocupacao?.PosicaoId == posicao.Id))
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.NadaAMudar, codigo);
        }

        if (!Observacoes(pedido.Observacoes, out var observacoes))
        {
            return ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.ObservacoesLongas, codigo);
        }

        var agora = DateTime.UtcNow;
        var avisos = new List<string>();
        if (posicao is not null)
        {
            await AvisosDaPosicaoAsync(posicao, tool, avisos, cancellationToken);
        }

        var movimento = Novo(pedido.OperacaoId, ArmazemMovimentoTipo.Corrigir, tool.ToolId, agora, agora, observacoes);
        movimento.PosicaoAnteriorId = ocupacao?.PosicaoId;
        var localizacao = await db.FerramentaLocalizacoes.FirstOrDefaultAsync(l => l.ToolId == tool.ToolId, cancellationToken);
        return await GravarAsync(movimento, posicao, async () =>
        {
            if (ocupacao is not null)
            {
                ocupacao.LibertadaEmUtc = agora;
                ocupacao.LibertadaPorMovimentoId = movimento.Id;
            }

            if (posicao is not null)
            {
                db.ArmazemOcupacoes.Add(new ArmazemOcupacao
                {
                    ToolId = tool.ToolId,
                    PosicaoId = posicao.Id,
                    MovimentoId = movimento.Id,
                    DesdeUtc = agora
                });
            }

            // Corrigir repõe o Fabrico (também depois de uma Sucata por
            // engano); o estado fica como está.
            Localizar(localizacao, tool.ToolId, FerramentaLocalizacaoTipo.Fabrico, null, movimento);
            await Task.CompletedTask;
        }, avisos, codigo, cancellationToken);
    }

    // Grava em duas fases na mesma transação: primeiro o movimento com a
    // operação (o envio repetido colide aqui e nada fica), depois o que
    // precisa do Id do movimento (a ocupação, a localização e o estado).
    private async Task<ArmazemMovimentoSaida> GravarAsync(
        ArmazemMovimento movimento,
        ArmazemPosicao? posicao,
        Func<Task> resto,
        List<string> avisos,
        string? codigo,
        CancellationToken cancellationToken)
    {
        var transacao = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            if (posicao is { Id: 0 })
            {
                db.ArmazemPosicoes.Add(posicao);
                await db.SaveChangesAsync(cancellationToken);
            }

            movimento.PosicaoId = movimento.Tipo == ArmazemMovimentoTipo.Saida ? null : posicao?.Id;
            db.ArmazemMovimentos.Add(movimento);
            var operacao = operacoes.Registar(movimento.OperacaoId, OperacaoTipo, new ResultadoDaOperacao(ResultadoTipo, null));
            await db.SaveChangesAsync(cancellationToken);

            operacao.ResultadoId = movimento.Id;
            await resto();
            await db.SaveChangesAsync(cancellationToken);
            // A Reparação Programada: a Saída e a Entrada (também a do par) atualizam as listas ativas.
            var doPar = OperacaoDoPar(movimento.OperacaoId);
            var feitos = db.ChangeTracker.Entries<ArmazemMovimento>().Select(e => e.Entity)
                .Where(m => m.OperacaoId == movimento.OperacaoId || m.OperacaoId == doPar).ToList();
            await DMO.Alpha.Infrastructure.Programada.ReparacaoProgramadaMovimentos.AoMovimentosAsync(db, feitos, cancellationToken);
            if (transacao is not null)
            {
                await transacao.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transacao is not null)
            {
                await transacao.DisposeAsync();
            }
        }

        return new ArmazemMovimentoSaida(ArmazemMovimentoResultado.Gravado, avisos, codigo, movimento.Id);
    }

    // Um conflito na base (o mesmo envio, outra pessoa com a mesma
    // ferramenta, a mesma posição nova) volta a correr uma vez sobre o que
    // está gravado agora, e recebe «já registado» ou a recusa certa.
    private async Task<ArmazemMovimentoSaida> ComRepeticaoAsync(
        Guid operacaoId, Func<Task<ArmazemMovimentoSaida>> tentativa, CancellationToken cancellationToken)
    {
        try
        {
            return await tentativa();
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || UniqueViolationRetry.IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
        }

        try
        {
            return await tentativa();
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || UniqueViolationRetry.IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            return await JaRegistadoAsync(operacaoId, cancellationToken)
                ?? ArmazemMovimentoSaida.Recusa(ArmazemMovimentoResultado.MudouEntretanto);
        }
    }

    private async Task<ArmazemMovimentoSaida?> JaRegistadoAsync(Guid operacaoId, CancellationToken cancellationToken)
    {
        var operacao = await operacoes.ProcurarAsync(operacaoId, cancellationToken);
        return operacao is null
            ? null
            : new ArmazemMovimentoSaida(ArmazemMovimentoResultado.JaRegistado, [], null, operacao.ResultadoId);
    }

    private Task<Tool?> FerramentaAsync(string? toolId, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(toolId) || toolId == ToolRegistryConstants.RemovedToolId
            ? Task.FromResult<Tool?>(null)
            : db.Tools.AsNoTracking().FirstOrDefaultAsync(t => t.ToolId == toolId, cancellationToken);

    private Task<ArmazemMovimento?> UltimoMovimentoAsync(string toolId, CancellationToken cancellationToken) =>
        db.ArmazemMovimentos.AsNoTracking()
            .Where(m => m.ToolId == toolId)
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<ArmazemLinhaOpcao>> LinhasAsync(CancellationToken cancellationToken) =>
        (await db.Machines.AsNoTracking().Select(m => new ArmazemLinhaOpcao(m.Id, m.Code)).ToListAsync(cancellationToken))
            .OrderBy(m => m.Codigo, StringComparer.OrdinalIgnoreCase).ToList();

    // Uma Linha que já não existe não se grava (o movimento grava sem ela).
    private async Task<int?> LinhaValidaAsync(int? linhaId, CancellationToken cancellationToken) =>
        linhaId is { } id && await db.Machines.AsNoTracking().AnyAsync(m => m.Id == id, cancellationToken) ? id : null;

    /// <summary>
    /// O Job On da ferramenta nessa Linha no dia da Saída: o que já começou
    /// (o mais recente) e ainda não acabou; senão, o próximo a começar (a
    /// ferramenta sai do armazém antes de a produção entrar em máquina).
    /// </summary>
    internal async Task<int?> JobOnAtivoAsync(string toolId, ToolType tipo, int machineId, DateTime ocorridoUtc, CancellationToken cancellationToken)
    {
        var dia = DateOnly.FromDateTime(DmoBusinessTime.ToLocal(ocorridoUtc));
        var jobs = db.JobOns.AsNoTracking().Where(j => j.MachineId == machineId && (j.FimEm == null || j.FimEm >= dia));
        jobs = tipo switch
        {
            ToolType.Cm => jobs.Where(j => db.CmContexts.Any(c => c.JobOnId == j.Id && c.ToolId == toolId)),
            ToolType.Mf => jobs.Where(j => db.MfContexts.Any(c => c.JobOnId == j.Id && c.ToolId == toolId)),
            _ => jobs.Where(j => db.BqContexts.Any(c => c.JobOnId == j.Id && c.ToolId == toolId))
        };
        var candidatos = await jobs.Select(j => new { j.Id, j.InicioEm }).ToListAsync(cancellationToken);
        var aCorrer = candidatos.Where(j => j.InicioEm is null || j.InicioEm <= dia)
            .OrderByDescending(j => j.InicioEm ?? DateOnly.MinValue).ThenByDescending(j => j.Id).FirstOrDefault();
        return aCorrer?.Id
            ?? candidatos.OrderBy(j => j.InicioEm).ThenBy(j => j.Id).Select(j => (int?)j.Id).FirstOrDefault();
    }

    private static string? Cortado(string? texto, int max) => texto is { Length: > 0 } t && t.Length > max ? t[..max] : texto;

    private Task<ArmazemOcupacao?> OcupacaoAtivaAsync(string toolId, CancellationToken cancellationToken) =>
        db.ArmazemOcupacoes.AsNoTracking().FirstOrDefaultAsync(o => o.ToolId == toolId && o.LibertadaEmUtc == null, cancellationToken);

    private async Task<string?> CodigoAsync(int posicaoId, CancellationToken cancellationToken) =>
        await db.ArmazemPosicoes.AsNoTracking().Where(p => p.Id == posicaoId).Select(p => p.Codigo).FirstOrDefaultAsync(cancellationToken);

    // A posição escrita: o código pela regra das posições; a que não existe
    // cria-se só com «Criar e registar» (e entra no mesmo envio).
    private async Task<(ArmazemMovimentoResultado? Recusa, ArmazemPosicao? Posicao, string? Codigo)> PosicaoAsync(
        string? texto, bool criar, CancellationToken cancellationToken)
    {
        if (!PosicaoCodigo.TryParse(texto, out var codigo))
        {
            return (ArmazemMovimentoResultado.PosicaoInvalida, null, (texto ?? string.Empty).Trim());
        }

        var posicao = await db.ArmazemPosicoes.AsNoTracking().FirstOrDefaultAsync(p => p.Codigo == codigo, cancellationToken);
        if (posicao is not null)
        {
            return (null, posicao, codigo);
        }

        if (!criar)
        {
            return (ArmazemMovimentoResultado.PosicaoNaoExiste, null, codigo);
        }

        return (null, new ArmazemPosicao { Codigo = codigo, CriadaEmUtc = DateTime.UtcNow, CriadaPor = Autor() }, codigo);
    }

    private async Task AvisosDaDataAsync(string toolId, DateTime ocorrido, DateTime agora, List<string> avisos, CancellationToken cancellationToken)
    {
        if (ocorrido > agora + FolgaDoFuturo)
        {
            avisos.Add($"A data {Data(ocorrido)} é no futuro. O movimento ficou gravado; se a data for outra, registe um Corrigir localização.");
        }

        var ultimo = await db.ArmazemMovimentos.AsNoTracking()
            .Where(m => m.ToolId == toolId)
            .OrderByDescending(m => m.Id)
            .Select(m => (DateTime?)m.OcorridoEmUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (ultimo is { } anterior && ocorrido < anterior)
        {
            avisos.Add($"A data {Data(ocorrido)} é anterior ao último movimento desta ferramenta ({Data(anterior)}). O movimento ficou gravado e conta como o mais recente.");
        }
    }

    // Arquivada, ou com outra ferramenta que não é o par da mesma referência
    // e lote (a CM e o MF guardam-se juntos; perguntas do full Q8).
    private async Task AvisosDaPosicaoAsync(ArmazemPosicao posicao, Tool tool, List<string> avisos, CancellationToken cancellationToken)
    {
        if (posicao.Id == 0)
        {
            return;
        }

        if (!posicao.Ativa)
        {
            avisos.Add($"A posição {posicao.Codigo} está arquivada. Reative-a nas Posições se continua a usar-se.");
        }

        var ocupantes = (await OcupantesAsync([posicao.Id], cancellationToken)).GetValueOrDefault(posicao.Id) ?? [];
        var outros = ocupantes
            .Where(o => o.ToolId != tool.ToolId
                && !(o.Tipo != tool.Type && o.Referencia == tool.Reference && o.Lote == tool.Lot))
            .ToList();
        if (outros.Count > 0)
        {
            avisos.Add($"A posição {posicao.Codigo} já tem {string.Join(", ", outros.Select(o => ArmazemTextos.Ferramenta(o.Tipo, o.Referencia, o.Lote)))}.");
        }
    }

    private async Task<IReadOnlyList<ArmazemReparadorOpcao>> ReparadoresAsync(ToolType tipo, CancellationToken cancellationToken) =>
        tipo switch
        {
            ToolType.Bq => await db.BqRepairers.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new ArmazemReparadorOpcao(r.Id, r.Name))
                .ToListAsync(cancellationToken),
            ToolType.Cm => await db.Reparadores.AsNoTracking()
                .Where(r => r.Ativo && r.AplicaCm)
                .OrderBy(r => r.Nome)
                .Select(r => new ArmazemReparadorOpcao(r.Id, r.Nome))
                .ToListAsync(cancellationToken),
            _ => await db.Reparadores.AsNoTracking()
                .Where(r => r.Ativo && r.AplicaMf)
                .OrderBy(r => r.Nome)
                .Select(r => new ArmazemReparadorOpcao(r.Id, r.Nome))
                .ToListAsync(cancellationToken)
        };

    private async Task<Dictionary<string, (string Codigo, DateTime DesdeUtc)>> PosicoesAtuaisAsync(
        IReadOnlyCollection<string> toolIds, CancellationToken cancellationToken)
    {
        var linhas = await (
                from o in db.ArmazemOcupacoes.AsNoTracking()
                join p in db.ArmazemPosicoes.AsNoTracking() on o.PosicaoId equals p.Id
                where o.LibertadaEmUtc == null && toolIds.Contains(o.ToolId)
                select new { o.ToolId, p.Codigo, o.DesdeUtc })
            .ToListAsync(cancellationToken);
        return linhas.ToDictionary(l => l.ToolId, l => (l.Codigo, l.DesdeUtc));
    }

    private async Task<Dictionary<int, string>> CodigosAsync(IEnumerable<int?> ids, CancellationToken cancellationToken)
    {
        var lista = ids.OfType<int>().Distinct().ToList();
        return lista.Count == 0
            ? []
            : await db.ArmazemPosicoes.AsNoTracking()
                .Where(p => lista.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Codigo, cancellationToken);
    }

    internal static string Descricao(ArmazemMovimento m, IReadOnlyDictionary<int, string> codigos)
    {
        string Codigo(int? id) => id is { } i && codigos.TryGetValue(i, out var c) ? c : "?";
        switch (m.Tipo)
        {
            case ArmazemMovimentoTipo.Entrada:
                var partes = new List<string> { $"Entrada {Codigo(m.PosicaoId)}" };
                if (m.Origem is { } origem && origem != ArmazemOrigem.JaEstava)
                {
                    partes.Add(ArmazemTextos.Origem(origem));
                }

                if (m.Estado is { } estado)
                {
                    partes.Add(ArmazemTextos.Estado(estado));
                }

                return string.Join(" · ", partes);
            case ArmazemMovimentoTipo.Saida:
                var destino = m.Destino is { } d ? ArmazemTextos.Destino(d) : "?";
                var detalhe = m.ReparadorNome ?? m.FabricaDestino;
                return $"Saída {Codigo(m.PosicaoAnteriorId)} para {destino}" + (detalhe is null ? string.Empty : $" · {detalhe}");
            default:
                return m.PosicaoId is null
                    ? "Corrigida: não está no armazém"
                    : $"Corrigida para a posição {Codigo(m.PosicaoId)}";
        }
    }

    private ArmazemMovimento Novo(Guid operacaoId, ArmazemMovimentoTipo tipo, string toolId, DateTime ocorrido, DateTime agora, string? observacoes) =>
        new()
        {
            Tipo = tipo,
            ToolId = toolId,
            OcorridoEmUtc = ocorrido,
            RegistadoEmUtc = agora,
            RegistadoPor = Autor(),
            OperacaoId = operacaoId,
            Observacoes = observacoes,
            VersaoApp = Versao
        };

    private void Localizar(FerramentaLocalizacao? atual, string toolId, FerramentaLocalizacaoTipo onde, string? reparador, ArmazemMovimento movimento)
    {
        if (atual is null)
        {
            atual = new FerramentaLocalizacao { ToolId = toolId };
            db.FerramentaLocalizacoes.Add(atual);
        }

        atual.Localizacao = onde;
        atual.ReparadorNome = reparador;
        atual.DesdeUtc = movimento.OcorridoEmUtc;
        atual.MovimentoId = movimento.Id;
    }

    private void RegistarEstado(string toolId, FerramentaEstadoTipo estado, ArmazemMovimento movimento, DateTime agora) =>
        db.FerramentaEstados.Add(new FerramentaEstado
        {
            ToolId = toolId,
            Estado = estado,
            MovimentoId = movimento.Id,
            RegistadoEmUtc = agora,
            RegistadoPor = Autor()
        });

    private string? Autor()
    {
        var actor = account.Current;
        return actor.IsAuthenticated
            ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName)
            : null;
    }

    private static bool Observacoes(string? texto, out string? observacoes)
    {
        observacoes = Limpo(texto);
        return observacoes is not { Length: > ArmazemMovimento.ObservacoesMaxLength };
    }

    private static string? Limpo(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string Data(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string? VersaoDaApp()
    {
        var versao = typeof(ArmazemMovimentos).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return versao is { Length: > 64 } ? versao[..64] : versao;
    }
}
