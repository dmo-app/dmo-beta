using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// O texto da confirmação do «Eliminar Job On» (Diogo, 19/10/2026, D2): a
/// pergunta, o aviso de que pode já ter produzido quando o dia de início já
/// chegou e o que se perde. Só quem tem um módulo do Controlo vê os registos
/// do Controlo contados (Diogo, 19/10/2026: a informação do Controlo no Job On
/// aparece a quem tem módulos do Controlo); os outros leem que se perdem os
/// registos do Controlo.
/// </summary>
public sealed record JobOnEliminarAviso(int JobOnId, string Pergunta, string? JaProduziu, string? Perde)
{
    public static JobOnEliminarAviso From(JobOnDeletionPreview preview, bool veControlo)
    {
        var pergunta = $"Quer mesmo eliminar o Job On {preview.Reference} · nº {preview.ProductionNumber}? Não se pode desfazer.";
        var jaProduziu = preview.StartReached && preview.InicioEm is { } inicio
            ? $"O dia de início ({inicio:dd/MM/yyyy}) já chegou: esta produção pode já ter produzido."
            : null;

        var perdas = new List<string>();
        if (veControlo)
        {
            Add(perdas, preview.Pesos, "Peso", "Pesos");
            Add(perdas, preview.Comparacoes, "comparação do Peso", "comparações do Peso");
            Add(perdas, preview.Pegamentos, "folha dos Pegamentos", "folhas dos Pegamentos");
            Add(perdas, preview.Puncoes, "registo das Punções", "registos das Punções");
            if (preview.Revisto)
            {
                perdas.Add("a decisão da Revisão");
            }
        }
        else if (preview.Pesos + preview.Comparacoes + preview.Pegamentos + preview.Puncoes > 0 || preview.Revisto)
        {
            perdas.Add("os registos do Controlo desta produção");
        }

        if (preview.BqMovements > 0)
        {
            perdas.Add(preview.BqMovements == 1
                ? "1 movimento das Boquilhas (deixa de contar na BQ)"
                : $"{preview.BqMovements} movimentos das Boquilhas (deixam de contar na BQ)");
        }

        if (preview.ReparacoesInternas > 0)
        {
            perdas.Add(preview.ReparacoesInternas == 1
                ? "1 reparação"
                : $"{preview.ReparacoesInternas} reparações");
        }

        var perde = perdas.Count == 0 ? null : "Perde-se também: " + Join(perdas) + ".";
        return new JobOnEliminarAviso(preview.JobOnId, pergunta, jaProduziu, perde);
    }

    private static void Add(List<string> perdas, int count, string one, string many)
    {
        if (count > 0)
        {
            perdas.Add(count == 1 ? $"1 {one}" : $"{count} {many}");
        }
    }

    private static string Join(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " e " + items[^1];
}
