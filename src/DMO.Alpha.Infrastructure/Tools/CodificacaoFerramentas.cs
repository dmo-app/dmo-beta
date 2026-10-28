using System.Globalization;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// A tabela da codificação por linha (<see cref="Codificacao"/>), guardada nas
/// definições dos módulos. Uma chave sem valor gravado vale o da folha; um
/// campo apagado grava vazio («sem valor»), para não voltar o da folha.
/// </summary>
public sealed class CodificacaoFerramentas(DmoDbContext db) : ICodificacaoFerramentas
{
    private static readonly ToolType[] TiposFerramenta = [ToolType.Cm, ToolType.Mf, ToolType.Bq];

    private static string Sigla(ToolType tipo) => tipo.ToString().ToUpperInvariant();

    private Task<Dictionary<string, string>> DefinicoesAsync(CancellationToken cancellationToken) =>
        db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey.StartsWith(Codificacao.PadraoPrefixo)
                || s.SettingKey.StartsWith(Codificacao.AplicadoPrefixo)
                || s.SettingKey.StartsWith(ToolLotQuantityKeys.Prefix)
                || s.SettingKey == Codificacao.ModoB2Chave)
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);

    private static string? Padrao(Dictionary<string, string> valores, string tipo, CodificacaoLinha linha) =>
        valores.TryGetValue(Codificacao.ChavePadrao(tipo, linha), out var texto)
            ? (string.IsNullOrWhiteSpace(texto) ? null : texto)
            : Codificacao.PadraoPorOmissao(tipo, linha);

    private static int? Quantidade(Dictionary<string, string> valores, string tipo, CodificacaoLinha linha) =>
        valores.TryGetValue(Codificacao.ChaveQuantidade(tipo, linha), out var texto)
            ? (int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : null)
            : Codificacao.QuantidadePorOmissao(tipo, linha);

    private static B2Modo Modo(Dictionary<string, string> valores) =>
        Codificacao.ModoDe(valores.GetValueOrDefault(Codificacao.ModoB2Chave)) ?? B2Modo.Dupla;

    private static CodificacaoTabela Tabela(Dictionary<string, string> valores) => new(
        Modo(valores),
        Codificacao.Linhas.Select(linha => new CodificacaoFila(linha, Codificacao.Tipos
            .Select(tipo => new CodificacaoCelula(tipo, tipo == Codificacao.An ? null : Padrao(valores, tipo, linha), Quantidade(valores, tipo, linha)))
            .ToList())).ToList());

    public async Task<CodificacaoTabela> LerAsync(CancellationToken cancellationToken = default) =>
        Tabela(await DefinicoesAsync(cancellationToken));

    public async Task<GuardarCodificacaoOutcome> GuardarAsync(IReadOnlyDictionary<string, string?> valores, CancellationToken cancellationToken = default)
    {
        var padroes = Codificacao.Linhas.SelectMany(l => Codificacao.Tipos.Where(t => t != Codificacao.An)
            .Select(t => (Chave: Codificacao.ChavePadrao(t, l), Tipo: t, Linha: l))).ToDictionary(c => c.Chave, StringComparer.Ordinal);
        var quantidades = Codificacao.Linhas.SelectMany(l => Codificacao.Tipos
            .Select(t => (Chave: Codificacao.ChaveQuantidade(t, l), Tipo: t, Linha: l))).ToDictionary(c => c.Chave, StringComparer.Ordinal);

        // Uma chave que a tabela não tem recusa tudo, antes de qualquer escrita.
        if (valores.Keys.Any(k => !padroes.ContainsKey(k) && !quantidades.ContainsKey(k)))
            return new GuardarCodificacaoOutcome(GuardarCodificacaoResultado.ChaveDesconhecida, []);

        var gravar = new Dictionary<string, string>(StringComparer.Ordinal);
        var ignoradas = new List<CodificacaoIgnorada>();
        foreach (var (chave, texto) in valores)
        {
            var escrito = texto?.Trim() ?? string.Empty;
            if (padroes.ContainsKey(chave))
            {
                gravar[chave] = NormalizarPadrao(escrito);
                continue;
            }

            if (escrito.Length > 0
                && (!int.TryParse(escrito, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0))
            {
                var c = quantidades[chave];
                ignoradas.Add(new CodificacaoIgnorada(c.Tipo, c.Linha.Nome, escrito));
                continue;
            }

            gravar[chave] = escrito;
        }

        await EscreverAsync(gravar, cancellationToken);
        if (valores.Keys.Any(k => quantidades.TryGetValue(k, out var c) && c.Linha.Modo is not null))
            await SincronizarB2Async(cancellationToken);
        return new GuardarCodificacaoOutcome(GuardarCodificacaoResultado.Guardado, ignoradas);
    }

    // O padrão fica em maiúsculas, menos os «x» da referência (Mxxxx95MPNNX).
    private static string NormalizarPadrao(string escrito)
    {
        var texto = escrito.Replace(" ", string.Empty, StringComparison.Ordinal);
        var maiusculas = texto.ToUpperInvariant().ToCharArray();
        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] == 'x') maiusculas[i] = 'x';
        }

        return new string(maiusculas);
    }

    public async Task GuardarModoB2Async(B2Modo modo, CancellationToken cancellationToken = default)
    {
        await EscreverAsync(new Dictionary<string, string> { [Codificacao.ModoB2Chave] = modo.ToString() }, cancellationToken);
        await SincronizarB2Async(cancellationToken);
    }

    // A chave simples da B2 (a do total do lote por máquina, que a página de
    // criar sugere) fica com as quantidades da B2 do modo escolhido.
    private async Task SincronizarB2Async(CancellationToken cancellationToken)
    {
        var valores = await DefinicoesAsync(cancellationToken);
        var linha = Modo(valores) == B2Modo.Tripla ? Codificacao.B2Tripla : Codificacao.B2Dupla;
        await EscreverAsync(Codificacao.Tipos.ToDictionary(
            tipo => ToolLotQuantityKeys.Prefix + tipo + ":B2",
            tipo => Quantidade(valores, tipo, linha)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty), cancellationToken);
    }

    private async Task EscreverAsync(IReadOnlyDictionary<string, string> valores, CancellationToken cancellationToken)
    {
        var chaves = valores.Keys.ToList();
        var existentes = await db.ModuleSettings.Where(s => chaves.Contains(s.SettingKey)).ToDictionaryAsync(s => s.SettingKey, cancellationToken);
        foreach (var (chave, valor) in valores)
        {
            if (existentes.TryGetValue(chave, out var definicao)) definicao.SettingValue = valor;
            else db.ModuleSettings.Add(new ModuleSetting { SettingKey = chave, SettingValue = valor });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CodificacaoSugestao?> SugerirAsync(ToolType tipo, string referencia, IReadOnlyList<int> machineIds, B2Modo? modo, CancellationToken cancellationToken = default)
    {
        var ids = machineIds.Distinct().ToList();
        var maquinas = await db.Machines.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => m.Code).ToListAsync(cancellationToken);
        var linha = Codificacao.LinhaDe(maquinas, modo);
        if (linha is null) return null;
        var valores = await DefinicoesAsync(cancellationToken);
        var sigla = Sigla(tipo);
        return new CodificacaoSugestao(
            linha,
            Codificacao.NomeTecnico(Padrao(valores, sigla, linha), referencia),
            Quantidade(valores, sigla, linha),
            tipo == ToolType.Bq ? Quantidade(valores, Codificacao.An, linha) : null);
    }

    private sealed record Mudanca(Tool Tool, string? Nome, int? Quantidade, int? An, B2Modo? Modo)
    {
        public bool Muda => Nome is not null || Quantidade is not null || An is not null || Modo is not null;
    }

    private async Task<List<Mudanca>> MudancasAsync(bool seguir, CancellationToken cancellationToken)
    {
        var valores = await DefinicoesAsync(cancellationToken);
        var query = db.Tools.Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId && TiposFerramenta.Contains(t.Type));
        var tools = await (seguir ? query : query.AsNoTracking()).ToListAsync(cancellationToken);
        var maquinasPorTool = (await db.ToolMachineLinks.AsNoTracking()
                .Select(l => new { l.ToolId, l.Machine.Code }).ToListAsync(cancellationToken))
            .GroupBy(l => l.ToolId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Code).ToList(), StringComparer.Ordinal);

        var mudancas = new List<Mudanca>();
        foreach (var tool in tools)
        {
            if (!maquinasPorTool.TryGetValue(tool.ToolId, out var maquinas)) continue;
            var sigla = Sigla(tool.Type);
            var naB2 = maquinas.Contains("B2", StringComparer.OrdinalIgnoreCase);
            B2Modo? modo = naB2 && tool.B2Modo is null ? Codificacao.ModoPelasLinhas(maquinas) : null;
            var linha = Codificacao.LinhaDe(maquinas, tool.B2Modo ?? modo);
            if (linha is null)
            {
                if (modo is not null) mudancas.Add(new Mudanca(tool, null, null, null, modo));
                continue;
            }

            var novo = Codificacao.NomeTecnico(Padrao(valores, sigla, linha), tool.Reference);
            var aplicado = valores.TryGetValue(Codificacao.ChaveAplicado(sigla, linha), out var a) ? a : Codificacao.PadraoPorOmissao(sigla, linha);
            var antigo = Codificacao.NomeTecnico(aplicado, tool.Reference);
            // O nome muda quando está vazio ou veio do padrão anterior; um
            // nome escrito à mão (outro) fica.
            var nome = novo is not null && !string.Equals(novo, tool.TechnicalName, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(tool.TechnicalName) || string.Equals(antigo, tool.TechnicalName, StringComparison.Ordinal))
                ? novo : null;
            var quantidade = tool.BqAccountedQuantity is null ? Quantidade(valores, sigla, linha) : null;
            var an = tool.Type == ToolType.Bq && tool.AnQuantity is null ? Quantidade(valores, Codificacao.An, linha) : null;
            var mudanca = new Mudanca(tool, nome, quantidade, an, modo);
            if (mudanca.Muda) mudancas.Add(mudanca);
        }

        return mudancas;
    }

    public async Task<int> ContarAtualizarAsync(CancellationToken cancellationToken = default) =>
        (await MudancasAsync(seguir: false, cancellationToken)).Count;

    public async Task<int> AtualizarAsync(CancellationToken cancellationToken = default)
    {
        var mudancas = await MudancasAsync(seguir: true, cancellationToken);
        foreach (var m in mudancas)
        {
            if (m.Nome is not null) m.Tool.TechnicalName = m.Nome;
            if (m.Quantidade is not null) m.Tool.BqAccountedQuantity = m.Quantidade;
            if (m.An is not null) m.Tool.AnQuantity = m.An;
            if (m.Modo is not null) m.Tool.B2Modo = m.Modo;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Os padrões de agora passam a ser os aplicados: um «Atualizar»
        // depois de os mudar troca os nomes que vieram destes.
        var valores = await DefinicoesAsync(cancellationToken);
        await EscreverAsync(Codificacao.Linhas
            .SelectMany(l => new[] { Codificacao.Cm, Codificacao.Mf, Codificacao.Bq }.Select(t => (l, t)))
            .ToDictionary(x => Codificacao.ChaveAplicado(x.t, x.l), x => Padrao(valores, x.t, x.l) ?? string.Empty), cancellationToken);
        return mudancas.Count;
    }
}
