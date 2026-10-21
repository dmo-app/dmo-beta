namespace DMO.Alpha.Core.Production;

/// <summary>
/// Uma linha CAL da folha de um Job On: uma designação, um valor e a QTD Máq.
/// (Diogo, 21/10/2026), escritos. A linha apaga-se em cascata com o Job On.
/// <para>
/// A <see cref="Ordem"/> é a do ecrã, a partir de 1. Ao guardar, quando a lista
/// muda, as linhas gravadas dão lugar às do envio, pela ordem; por isso o índice
/// <c>(JobOnId, Ordem)</c> não é único. Uma linha só com um dos três campos
/// fica; uma sem nenhum não se grava. Os máximos vêm do catálogo
/// (<see cref="JobOnFolhaCampos.CalMaximo"/>); as colunas são <c>text</c>.
/// </para>
/// </summary>
public sealed class JobOnFolhaCalLinha
{
    public int Id { get; set; }

    public int JobOnId { get; set; }

    public JobOn? JobOn { get; set; }

    /// <summary>A posição da linha na CAL, pela ordem do ecrã, a partir de 1.</summary>
    public int Ordem { get; set; }

    /// <summary>A designação («Nível», «Pinças»); null quando não tem.</summary>
    public string? Designacao { get; set; }

    /// <summary>O valor («37.60 MM»); null quando não tem.</summary>
    public string? Valor { get; set; }

    /// <summary>A QTD Máq. da linha, escrita; null quando não tem.</summary>
    public string? QtdMaq { get; set; }
}
