using System.Collections.Generic;

namespace DMO.Alpha.Core.Production;

/// <summary>Uma linha do QUADRO COMPLETO DAS LINHAS do dia (o shared element
/// por Razor — o Owner: «quadro do dia pode ir»): a máquina sempre visível,
/// as produções do dia (o badge + a referência + o nº) ou a linha vazia «—».</summary>
public sealed record DayMachineRow(
    string MachineCode,
    IReadOnlyList<DayProduction> Productions)
{
    public bool HasProductions => Productions is { Count: > 0 };

    /// <summary>Duas produções da máquina que se cruzam no dia: a sobreposição
    /// grava com aviso (decisão do proprietário de 16/10/2026) e a linha fica
    /// marcada a vermelho para se ver e corrigir as datas. A que sai no dia em
    /// que a outra entra não conta (Diogo, 21/10/2026).</summary>
    public bool HasOverlap { get; init; }

    /// <summary>A próxima mudança nesta máquina depois do dia: a produção que
    /// entra a seguir ou a troca de um componente da produção atual, a que
    /// vier primeiro (o proprietário, 17/10/2026); null quando não há
    /// nenhuma marcada.</summary>
    public NextChange? Next { get; init; }
}

/// <summary>A próxima mudança de uma máquina: o dia, o que muda («1199C142
/// 202601» ou «BQ lote 165 para 166») e se é a entrada de outra produção.
/// O texto é um link (Diogo, 21/10/2026): para o Job On que entra, ou para a
/// ficha da ferramenta que entra na troca.</summary>
public sealed record NextChange(
    DateOnly Dia,
    string Texto,
    bool EntraProducao,
    int? JobOnId = null,
    string? ToolId = null);

/// <summary>Uma produção do quadro do dia: os FATOS HUMANOS (a referência e o
/// número), o JobOnId só para a abertura (?jobon_id), e as
/// mudanças de lote desse dia («Muda BQ lote 6 para 7»).</summary>
public sealed record DayProduction(
    int JobOnId,
    string Reference,
    string? ProductionNumber)
{
    public IReadOnlyList<string> LotNotes { get; init; } = [];

    /// <summary>Quantas alterações a produção teve depois de gravada (0 = nenhuma).</summary>
    public int ChangeCount { get; init; }

    /// <summary>Qual foi a alteração, numa linha («Mudou CM: 5447 · lote 4»); null sem alterações.</summary>
    public string? ChangeSummary { get; init; }

    /// <summary>Cada alteração por extenso, para a dica da marca.</summary>
    public IReadOnlyList<string> ChangeDetails { get; init; } = [];
}