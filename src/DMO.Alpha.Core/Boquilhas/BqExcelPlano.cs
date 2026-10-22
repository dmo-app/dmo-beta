using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Uma linha do Excel das Boquilhas numa máquina e num dia: a BQ com o lote
/// («T173/89») e as quantidades de cada movimento desse dia, por tipo. Mais
/// do que uma quantidade escreve-se como soma (=23+10), como o Diogo faz.
/// </summary>
public sealed record BqExcelLinha(
    string Ref,
    IReadOnlyList<int> Saidas,
    IReadOnlyList<int> Entradas,
    IReadOnlyList<int> SemReparacao);

/// <summary>Um dia do Excel: as linhas de cada máquina (B1..C3); máquina sem movimentos = sem linhas.</summary>
public sealed record BqExcelDia(DateOnly Dia, IReadOnlyDictionary<string, IReadOnlyList<BqExcelLinha>> Maquinas);

/// <summary>
/// O EXCEL DAS BOQUILHAS DO CHEFE (Diogo, 21/10/2026: «cada registo que faço
/// passa guardar a informação nesse excel que está numa pasta do pc»): o que
/// a app escreve em cada dia, a partir dos movimentos. Todos os dias do
/// intervalo entram, mesmo sem movimentos, para que uma correção ou uma
/// anulação também apague o que já lá estava escrito.
/// </summary>
public static class BqExcelPlano
{
    public static readonly IReadOnlyList<string> Maquinas = ["B1", "B2", "B3", "C1", "C2", "C3"];

    public static IReadOnlyList<BqExcelDia> From(IEnumerable<BqHistoryMovementItem> movements, DateOnly desde, DateOnly ate)
    {
        var entrada = BqMovementType.Entrada.ToString();
        var saida = BqMovementType.Saida.ToString();
        var semReparacao = BqMovementType.EntradaSemReparacao.ToString();

        var porDia = movements
            .Where(movement => movement.MachineCode is not null && Maquinas.Contains(movement.MachineCode))
            .OrderBy(movement => movement.OccurredAtUtc)
            .ThenBy(movement => movement.MovementId)
            .GroupBy(movement => DateOnly.FromDateTime(DmoBusinessTime.ToLocal(movement.OccurredAtUtc)))
            .ToDictionary(group => group.Key, group => group.ToList());

        var dias = new List<BqExcelDia>();
        for (var dia = desde; dia <= ate; dia = dia.AddDays(1))
        {
            var doDia = porDia.GetValueOrDefault(dia) ?? [];
            var maquinas = Maquinas.ToDictionary(
                maquina => maquina,
                maquina => (IReadOnlyList<BqExcelLinha>)doDia
                    .Where(movement => movement.MachineCode == maquina)
                    .GroupBy(movement => RefDe(movement.ToolReference, movement.ToolLot))
                    .Select(group => new BqExcelLinha(
                        group.Key,
                        group.Where(m => m.MovementType == saida).Select(m => m.Quantity).ToList(),
                        group.Where(m => m.MovementType == entrada).Select(m => m.Quantity).ToList(),
                        group.Where(m => m.MovementType == semReparacao).Select(m => m.Quantity).ToList()))
                    .ToList());
            dias.Add(new BqExcelDia(dia, maquinas));
        }

        return dias;
    }

    /// <summary>A Ref do Excel: a referência da BQ e o lote, separados por «/».</summary>
    public static string RefDe(string? reference, string? lot)
    {
        var referencia = reference?.Trim() ?? "";
        var lote = lot?.Trim() ?? "";
        return lote.Length == 0 ? referencia : $"{referencia}/{lote}";
    }
}

/// <summary>O ficheiro não é o Excel das Boquilhas (ou não se consegue ler); a mensagem vai para o ecrã.</summary>
public sealed class BqExcelInvalidoException(string message) : Exception(message);

/// <summary>O livro escrito, o que ficou por escrever (um aviso por linha) e a versão dos movimentos que leva.</summary>
public sealed record BqExcelResultado(byte[] Livro, IReadOnlyList<string> Avisos, string Versao = "", IReadOnlyList<BqExcelEscrita>? Escritas = null);

/// <summary>
/// O «Atualizar» do Excel (Diogo, 22/10/2026): o livro escrito e os dias
/// passados que estavam diferentes da app e foram reescritos (do mais antigo
/// para o mais recente).
/// </summary>
public sealed record BqExcelAtualizacao(BqExcelResultado Resultado, IReadOnlyList<DateOnly> Atualizados)
{
    /// <summary>A mensagem para o ecrã: «Atualizados 3 dias: 19/10, 20/10, 21/10» ou «O Excel já estava em dia».</summary>
    public string Mensagem => Atualizados.Count switch
    {
        0 => "O Excel já estava em dia.",
        1 => $"Atualizado 1 dia: {Atualizados[0].ToString("dd'/'MM", System.Globalization.CultureInfo.InvariantCulture)}.",
        _ => $"Atualizados {Atualizados.Count} dias: {string.Join(", ", Atualizados.Select(d => d.ToString("dd'/'MM", System.Globalization.CultureInfo.InvariantCulture)))}."
    };
}

/// <summary>Uma linha que a app escreveu (dia, máquina, linha da folha e a Ref): só essas a app volta a mexer.</summary>
public sealed record BqExcelEscrita(DateOnly Dia, string Maquina, int Linha, string Ref);

/// <summary>
/// O Excel das Boquilhas do chefe: a VERSÃO dos movimentos (muda a cada
/// registo, correção ou anulação; o navegador só reescreve o ficheiro quando
/// ela mudou) e a escrita dos dias desde a data em que o ficheiro foi ligado.
/// </summary>
public interface IBqExcelBoquilhas
{
    Task<string> VersaoAsync(CancellationToken cancellationToken = default);

    /// <summary>Um Excel novo, vazio, com o separador do mês do dia dado.</summary>
    byte[] Novo(DateOnly dia);

    Task<BqExcelResultado> EscreverAsync(byte[] livro, DateOnly desde, DateOnly ate, IReadOnlyList<BqExcelEscrita> anteriores, CancellationToken cancellationToken = default);

    /// <summary>
    /// «Atualizar» (Diogo, 22/10/2026): escreve hoje e, a partir de ontem,
    /// recua um dia de cada vez enquanto o Excel estiver diferente da app;
    /// pára no primeiro dia igual (ou no primeiro dia com movimentos na app,
    /// ou no início do ano do livro).
    /// </summary>
    Task<BqExcelAtualizacao> AtualizarAsync(byte[] livro, DateOnly hoje, IReadOnlyList<BqExcelEscrita> anteriores, CancellationToken cancellationToken = default);
}
