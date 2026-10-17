using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// O aviso «O Job On mudou depois deste registo» num módulo (o proprietário,
/// 17/10/2026): a frase de abertura, as alterações que esse módulo vigia e o
/// link para «O que mudou» na ficha do Job On. Só informa; não bloqueia nada.
/// </summary>
public sealed record JobOnChangeNoticeView(string Intro, JobOnChangeNotice Notice)
{
    public string Href => $"/JobOn?jobon_id={Notice.JobOnId}&vista=alteracoes#fichaMudancas";

    public static string When(DateTime changedAtUtc) =>
        DmoBusinessTime.ToLocal(changedAtUtc).ToString("dd/MM/yyyy HH:mm");

    public static JobOnChangeNoticeView? From(string intro, JobOnChangeNotice? notice) =>
        notice is null ? null : new JobOnChangeNoticeView(intro, notice);
}
