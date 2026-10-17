using System;
using System.Collections.Generic;

namespace DMO.Alpha.Web.Pages.Shared;

/// <summary>
/// O SHARED ELEMENT do calendário (o Owner 2026-10-14: «os calendários estão
/// diferentes isso deveria ser shared elements») — UM SÓ componente servido
/// pelas páginas que o usam (o JobOn, o histórico das Boquilhas e a Revisão do
/// Controlo; ver docs/ui-reference/componentes-partilhados.md), nunca duas
/// cópias divergentes. O HTML é servido por Razor (a fonte da verdade é o
/// servidor — os defaults de JS nunca alimentam a página).
/// </summary>
/// <summary>
/// O estado de UM dia do calendário: o <see cref="Day"/> é a IDENTIDADE do dia
/// (1..N do mês consultado — nunca a contagem de registos), <see cref="Has"/>
/// acende-o quando há registo naquele dia, <see cref="Selected"/> é o dia
/// consultado pelo quadro, <see cref="IsToday"/> marca o hoje e
/// <see cref="Count"/> é a contagem de registos no campo PRÓPRIO — os dois
/// fatos viajam SEPARADOS: cada dia conserva a sua identidade.
/// </summary>
public sealed record DmoCalendarDay(int Day, bool Has, bool Selected, bool IsToday, int Count);

/// <summary>
/// O estado de um calendário de mês: a grelha completa (os dias 1..N com os
/// estados) e o destino de navegação por query. O <see cref="NavigationPage"/>
/// é o PAGE NAME canónico da página (o partial gera os URLs por asp-page e o
/// href resultante é o da ROTA declarada — nunca URLs escritos à mão); o
/// <see cref="PreservedQuery"/> leva apenas os restantes parâmetros a
/// preservar (SEM mes/ano: a navegação do mês é o partial que a compõe — um
/// mês enviado, o ano atualizado na viragem dezembro/janeiro).
/// </summary>
public sealed record DmoCalendarPresentation(
    int Year,
    int Month,
    string MonthTitle,
    IReadOnlyList<DmoCalendarDay> Days,
    string? NavigationPage,
    IReadOnlyDictionary<string, string> PreservedQuery)
{
    /// <summary>A legenda do ponto dos dias (a Revisão diz «Por aprovar»).</summary>
    public string LegendLabel { get; init; } = "Com marcações";

    /// <summary>
    /// Monta o mês completo (os dias 1..N, o hoje e o dia escolhido) para o
    /// partial _DmoCalendar. A página só diz quantos registos há em cada dia
    /// (<paramref name="countForDay"/>); o ponto acende quando há registos, ou
    /// pela regra própria em <paramref name="hasForDay"/> (a Revisão acende só
    /// os dias por aprovar). Uma página nova usa isto em vez de copiar o ciclo.
    /// </summary>
    public static DmoCalendarPresentation ForMonth(
        int year,
        int month,
        Func<DateOnly, int> countForDay,
        DateOnly? selected,
        string navigationPage,
        IReadOnlyDictionary<string, string>? preservedQuery = null,
        Func<DateOnly, bool>? hasForDay = null)
    {
        var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
        var total = DateTime.DaysInMonth(year, month);
        var dias = new List<DmoCalendarDay>(total);
        for (var d = 1; d <= total; d++)
        {
            var data = new DateOnly(year, month, d);
            var count = countForDay(data);
            var has = hasForDay?.Invoke(data) ?? count > 0;
            dias.Add(new(d, has, selected == data, hoje == data, count));
        }
        return new(year, month, MonthTitleFor(year, month), dias, navigationPage,
            preservedQuery ?? new Dictionary<string, string>());
    }

    public static string MonthTitleFor(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy",
            System.Globalization.CultureInfo.GetCultureInfo("pt-PT"));
}
