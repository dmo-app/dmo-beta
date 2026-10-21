using DMO.Alpha.Core.Admin;
using Microsoft.AspNetCore.Http;

namespace DMO.Alpha.Web.Pages.Shared;

/// <summary>
/// O LINK DE UM AVISO (Diogo, 19/10/2026: «todos os warning, notices ter um
/// link para levar o user à página onde pode resolver, se possível»). Vai no
/// fim do texto do aviso, pelo partial <c>_DmoAvisoLink</c>; o partial só o
/// mostra a quem tem o módulo da página de destino (<see cref="Modulo"/>,
/// uma das <see cref="DmoCapabilities"/>; null = qualquer pessoa que já vê o
/// aviso). Quando não há página onde o problema se resolva, o aviso fica sem
/// link (lista em docs/ui-reference/componentes-partilhados.md).
/// </summary>
public sealed record DmoAvisoLink(string Texto, string Href, string? Modulo = null)
{
    /// <summary>
    /// Abre noutro separador: num formulário ainda por gravar (a folha do Job
    /// On), sair da página perdia o que se escreveu.
    /// </summary>
    public bool NovoSeparador { get; init; }

    /// <summary>Quem tem estas capabilities pode abrir o destino.</summary>
    public bool PodeAbrir(IReadOnlyCollection<string> capabilities) =>
        Modulo is null || capabilities.Contains(Modulo, StringComparer.Ordinal);

    /// <summary>
    /// A ficha de uma ferramenta. Com <paramref name="tecnica"/>, abre na
    /// informação técnica; com <paramref name="continuar"/> (um caminho local),
    /// a ficha mostra o link para voltar ao sítio de onde se veio.
    /// </summary>
    public static DmoAvisoLink FichaFerramenta(string toolId, string texto, bool tecnica = false, string? continuar = null)
    {
        var href = $"/Ferramentas/Ficha?tool_id={Uri.EscapeDataString(toolId)}";
        if (tecnica)
        {
            href += "&tecnica=1";
        }

        if (!string.IsNullOrEmpty(continuar))
        {
            href += $"&continuar={Uri.EscapeDataString(continuar)}";
        }

        return new DmoAvisoLink(texto, href, DmoCapabilities.Ferramentas);
    }

    /// <summary>Registar uma ferramenta nova em Ferramentas, já com o tipo e a referência escritos.</summary>
    public static DmoAvisoLink RegistarFerramenta(string tipo, string? referencia, string texto)
    {
        var href = $"/Ferramentas?criar=1&type={Uri.EscapeDataString(tipo)}";
        if (!string.IsNullOrWhiteSpace(referencia))
        {
            href += $"&reference={Uri.EscapeDataString(referencia.Trim())}";
        }

        return new DmoAvisoLink(texto, href, DmoCapabilities.Ferramentas);
    }

    /// <summary>Os movimentos de uma ferramenta no Armazém, onde se dá entrada e se regista o estado.</summary>
    public static DmoAvisoLink MovimentosDoArmazem(string toolId, string texto) =>
        new(texto, $"/Armazem/Movimentos?ferramenta={Uri.EscapeDataString(toolId)}", DmoCapabilities.ArmazemGestao);

    /// <summary>Uma produção aberta no Job On; com <paramref name="editar"/>, já no Editar.</summary>
    public static DmoAvisoLink JobOn(int jobOnId, string texto, bool editar = false) =>
        new(texto, $"/JobOn?jobon_id={jobOnId}{(editar ? "&editar=1#lightSheet" : null)}", DmoCapabilities.JobOnCreate);

    /// <summary>A Revisão do Controlo de uma produção, onde se aprova ou se volta a decidir.</summary>
    public static DmoAvisoLink Revisao(int jobOnId, string texto = "Rever na Revisão") =>
        new(texto, $"/Controlo/Approve?jobon_id={jobOnId}", DmoCapabilities.ControloApprove);

    /// <summary>
    /// A «Produção anterior» no Peso do controlo, onde o operador escolhe com
    /// que produção se compara (C6).
    /// </summary>
    public static DmoAvisoLink ProducaoAnteriorNoPeso(int controloId, string texto = "Escolher no Peso") =>
        new(texto, $"/Controlo/Peso?controlo_id={controloId}#pesoAnterior", DmoCapabilities.ControloCreate);

    /// <summary>
    /// O endereço da página aberta, para o «continuar» de um link voltar a
    /// ela; null numa gravação (um POST não se repete ao voltar).
    /// </summary>
    public static string? Aqui(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) ? $"{request.Path}{request.QueryString}" : null;

    /// <summary>As Definições do Controlo (a densidade do vidro, a tolerância dos Pegamentos).</summary>
    public static DmoAvisoLink DefinicoesDoControlo(string texto) =>
        new(texto, "/Controlo/Definicoes", DmoCapabilities.ControloCreate);

    /// <summary>Criar um Job On novo, com a referência já escrita quando se sabe.</summary>
    public static DmoAvisoLink CriarJobOn(string? referencia = null, string texto = "Criar o Job On") =>
        new(texto,
            "/JobOn?novo=1" + (string.IsNullOrWhiteSpace(referencia) ? null : $"&reference={Uri.EscapeDataString(referencia.Trim())}") + "#lightSheet",
            DmoCapabilities.JobOnCreate);
}
