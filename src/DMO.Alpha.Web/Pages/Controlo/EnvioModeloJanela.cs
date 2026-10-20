using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>A janela de um modelo do envio por email: um modelo para mudar, ou null para criar.</summary>
public sealed record EnvioModeloJanela(EmailModelo? Modelo, IReadOnlyList<EmailLista> Listas)
{
    public string Id => Modelo is null ? "ctl-modelo-novo" : $"ctl-modelo-{Modelo.Id}";
}
