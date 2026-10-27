using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Services;

namespace DMO.Alpha.Web.Presentation;

/// <summary>Uma leitura deixada de fora da comparação: a linha do form, o nº de CM, o valor escrito e o motivo.</summary>
public sealed record PesoComparacaoLeituraFora(
    int Linha,
    string? CmSubject,
    string? Valor,
    PesoComparacaoMotivoIgnorada Motivo)
{
    public string Message
    {
        get
        {
            var quem = string.IsNullOrWhiteSpace(CmSubject)
                ? $"Leitura {Linha} (sem nº de CM)"
                : $"Leitura {Linha} (CM {CmSubject})";
            return Motivo switch
            {
                PesoComparacaoMotivoIgnorada.MassaIlegivel =>
                    $"{quem}: a massa de água «{Valor}» é ilegível (escreva por exemplo 199,64 ou 199.64).",
                PesoComparacaoMotivoIgnorada.SemMassa =>
                    $"{quem}: falta a massa de água.",
                PesoComparacaoMotivoIgnorada.MassaNaoPositiva =>
                    $"{quem}: a massa de água {Valor} g não é maior que 0.",
                _ =>
                    $"{quem}: o peso calculado com a massa {Valor} g não é positivo."
            };
        }
    }
}

/// <summary>
/// O formulário da Comparação de Peso lido (saiu da página do Peso, Diogo,
/// 27/10/2026): as linhas escritas, as leituras que seguem para o backend,
/// as que ficam de fora com aviso e a recusa quando não há nada a comparar.
/// Também a escolha do Peso inicial e o aviso que atravessa o PRG do Guardar.
/// </summary>
public sealed class PesoComparacaoFormulario
{
    // A linha do form (1-based, a ordem das linhas reapresentadas), o nº de
    // CM e o texto escrito de cada leitura enviada ao backend, pela ordem do
    // pedido: para o aviso apontar a linha certa quando o backend deixa uma
    // de fora.
    private readonly IReadOnlyList<(int Linha, string? CmSubject, string? Valor)> _origens;

    private PesoComparacaoFormulario(
        IReadOnlyList<(string? Subject, string? Water)> linhas,
        IReadOnlyList<PesoComparacaoLeituraFora> fora,
        IReadOnlyList<(int, string?, string?)> origens,
        PesoComparacaoResultado? recusa,
        IReadOnlyList<PesoComparacaoLeituraInput>? leituras,
        decimal temperatura)
    {
        Linhas = linhas;
        Fora = fora;
        _origens = origens;
        Recusa = recusa;
        Leituras = leituras;
        Temperatura = temperatura;
    }

    /// <summary>As linhas escritas, sem as em branco (o re-render do form com os valores).</summary>
    public IReadOnlyList<(string? Subject, string? Water)> Linhas { get; }

    /// <summary>As linhas que ficaram de fora antes do backend (sem massa ou massa ilegível).</summary>
    public IReadOnlyList<PesoComparacaoLeituraFora> Fora { get; }

    /// <summary>Porque não se compara; nulo quando há leituras a comparar.</summary>
    public PesoComparacaoResultado? Recusa { get; }

    /// <summary>As leituras que seguem para o backend; nulas quando há recusa.</summary>
    public IReadOnlyList<PesoComparacaoLeituraInput>? Leituras { get; }

    public decimal Temperatura { get; }

    /// <summary>
    /// As linhas em branco ignoram-se; o nº de CM sem massa de água e a massa
    /// presente mas ilegível ficam DE FORA com aviso (A3, beta.65 — antes
    /// anulavam a comparação toda); a massa não positiva segue para o
    /// backend, que também a deixa de fora com aviso. Só sem nenhuma leitura
    /// utilizável a comparação é recusada (nada a comparar). A temperatura é
    /// flexível (ponto OU vírgula).
    /// </summary>
    public static PesoComparacaoFormulario Ler(
        int? pesoInicialId,
        string? waterTemperature,
        string[]? cmSubjects,
        string[]? waterWeights)
    {
        var subjects = cmSubjects ?? [];
        var waters = waterWeights ?? [];
        var count = Math.Max(subjects.Length, waters.Length);
        var linhas = new List<(string?, string?)>();
        var leituras = new List<PesoComparacaoLeituraInput>();
        var fora = new List<PesoComparacaoLeituraFora>();
        var origens = new List<(int, string?, string?)>();
        for (var i = 0; i < count; i++)
        {
            var subject = i < subjects.Length ? subjects[i]?.Trim() : null;
            var waterRaw = i < waters.Length ? waters[i]?.Trim() : null;
            var subjectTem = !string.IsNullOrEmpty(subject);
            var waterTem = !string.IsNullOrEmpty(waterRaw);
            if (!subjectTem && !waterTem)
            {
                continue; // linha em branco: nada a comparar
            }

            linhas.Add((subject, waterRaw));
            if (waterTem && MeasurementDecimal.Parse(waterRaw) is { } water)
            {
                leituras.Add(new PesoComparacaoLeituraInput(subject ?? string.Empty, water));
                origens.Add((linhas.Count, subject, waterRaw));
            }
            else
            {
                // Nº de CM sem massa de água, ou massa presente mas ilegível:
                // a linha fica de fora (nenhum facto é inventado) e o aviso
                // diz qual — as outras leituras comparam-se.
                fora.Add(new PesoComparacaoLeituraFora(
                    linhas.Count,
                    subject,
                    waterRaw,
                    waterTem ? PesoComparacaoMotivoIgnorada.MassaIlegivel : PesoComparacaoMotivoIgnorada.SemMassa));
            }
        }

        PesoComparacaoFormulario Recusado(PesoComparacaoResultado recusa) =>
            new(linhas, fora, origens, recusa, null, 0m);

        if (pesoInicialId is not { } inicialId || inicialId <= 0)
        {
            return Recusado(PesoComparacaoResultado.PesoInicialNaoSelecionado);
        }

        if (MeasurementDecimal.Parse(waterTemperature) is not { } temperatura)
        {
            // Vazia ou ilegível diz «ilegível»; fora de 5–35 °C é do backend.
            return Recusado(PesoComparacaoResultado.TemperaturaIlegivel);
        }

        if (leituras.Count == 0)
        {
            // Nenhuma leitura utilizável: nada a comparar. Com linhas
            // escritas mas ilegíveis o motivo é esse (o aviso diz quais).
            return Recusado(fora.Count > 0 ? PesoComparacaoResultado.MassaAguaIlegivel : PesoComparacaoResultado.SemLeituras);
        }

        return new(linhas, fora, origens, null, leituras, temperatura);
    }

    /// <summary>O aviso todo: as de fora daqui mais as que o backend deixou de fora (massa ≤ 0 ou resultado ≤ 0), pela linha.</summary>
    public IReadOnlyList<PesoComparacaoLeituraFora> ComAsDoBackend(IReadOnlyList<PesoComparacaoLeituraIgnorada>? ignoradas)
    {
        if (ignoradas is not { Count: > 0 })
        {
            return Fora;
        }

        var todas = Fora.ToList();
        foreach (var ignorada in ignoradas)
        {
            var origem = ignorada.Indice >= 1 && ignorada.Indice <= _origens.Count
                ? _origens[ignorada.Indice - 1]
                : (Linha: ignorada.Indice, CmSubject: ignorada.CmSubject, Valor: ignorada.WaterWeightGrams?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            todas.Add(new PesoComparacaoLeituraFora(origem.Linha, origem.CmSubject, origem.Valor, ignorada.Motivo));
        }

        return todas.OrderBy(a => a.Linha).ToList();
    }

    /// <summary>
    /// O peso inicial: o pedido (se é desta produção) ou, com vários Pesos (a
    /// CM ou a BQ mudou a meio; 18/10/2026), o das ferramentas atuais, senão
    /// o mais recente.
    /// </summary>
    public static int? EscolherPesoInicial(IReadOnlyList<PesoComparacaoPesoOpcao> iniciais, int? pedido) =>
        pedido is int id && iniciais.Any(i => i.PesoId == id)
            ? id
            : (iniciais.FirstOrDefault(i => i.IsCurrent) ?? iniciais.LastOrDefault())?.PesoId;

    /// <summary>
    /// O aviso das leituras de fora atravessa o PRG do Guardar num só
    /// parâmetro: uma linha por leitura, «linha|motivo|valor|cm» (o motivo
    /// é do vocabulário fechado; o valor e o nº de CM são o que o operador
    /// escreveu, limitados e sem os separadores).
    /// </summary>
    public static string? Codificar(IReadOnlyList<PesoComparacaoLeituraFora> fora)
    {
        if (fora.Count == 0)
        {
            return null;
        }

        static string Limpo(string? valor)
        {
            var texto = (valor ?? string.Empty).Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ').Trim();
            return texto.Length > 40 ? texto[..40] : texto;
        }

        return string.Join('\n', fora.Take(50).Select(a => $"{a.Linha}|{a.Motivo}|{Limpo(a.Valor)}|{Limpo(a.CmSubject)}"));
    }

    public static IReadOnlyList<PesoComparacaoLeituraFora> Descodificar(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return [];
        }

        var avisos = new List<PesoComparacaoLeituraFora>();
        foreach (var linha in encoded.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(50))
        {
            var partes = linha.Split('|', 4);
            if (partes.Length == 4
                && int.TryParse(partes[0], out var numero)
                && Enum.TryParse(partes[1], out PesoComparacaoMotivoIgnorada motivo)
                && Enum.IsDefined(motivo))
            {
                avisos.Add(new PesoComparacaoLeituraFora(numero, Vazio(partes[3]), Vazio(partes[2]), motivo));
            }
        }

        return avisos;
    }

    private static string? Vazio(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
