using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.AspNetCore.Mvc;

namespace DMO.Alpha.Web.ViewComponents;

/// <summary>
/// O «Enviar» do Resumo do controlo (Diogo, 19/10/2026, D9): o botão, a janela
/// com o modelo e a lista, o último envio e o resultado do envio que acabou
/// de voltar (?envio=). Fica fora do ResumoModel para o Resumo não crescer:
/// a página só o chama.
/// </summary>
public sealed class EnvioPesoViewComponent(
    EnvioEmailDefinicoes definicoes,
    EnviarPesoPorEmailHandler enviar,
    IEmailSender emailSender) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int jobOnId, int controloId)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var resultado = Enum.TryParse<EnviarPesoResultado>(Request.Query["envio"].ToString(), out var lido) ? lido : (EnviarPesoResultado?)null;
        return View(new EnvioPesoVista(
            jobOnId,
            await definicoes.ModelosAsync(cancellationToken),
            await definicoes.ListasAsync(cancellationToken),
            emailSender.Configurado,
            (await enviar.UltimosAsync(controloId, 1, cancellationToken)).FirstOrDefault(),
            resultado));
    }
}

public sealed record EnvioPesoVista(
    int JobOnId,
    IReadOnlyList<EmailModelo> Modelos,
    IReadOnlyList<EmailLista> Listas,
    bool Configurado,
    EmailEnvio? UltimoEnvio,
    EnviarPesoResultado? Resultado)
{
    public string Mensagem => Resultado switch
    {
        EnviarPesoResultado.Enviado => "PDF do Peso enviado.",
        EnviarPesoResultado.SemServidor => "O envio por email ainda não está configurado no servidor. Nada foi enviado.",
        EnviarPesoResultado.SemPeso => "Esta produção ainda não tem Peso. Nada foi enviado.",
        EnviarPesoResultado.SemModelo => "Escolha um modelo do envio. Nada foi enviado.",
        EnviarPesoResultado.SemLista => "Escolha uma lista de destinatários. Nada foi enviado.",
        EnviarPesoResultado.Falhou => "O servidor de email não aceitou o envio. Nada foi enviado; tente outra vez.",
        _ => string.Empty
    };

    /// <summary>O modelo e a lista são das Definições do Controlo: o link do aviso vai para lá.</summary>
    public bool ResolveNasDefinicoes => Resultado is EnviarPesoResultado.SemModelo or EnviarPesoResultado.SemLista;

    /// <summary>A lista proposta pelo primeiro modelo, escolhida ao abrir.</summary>
    public int? ListaProposta => Modelos.FirstOrDefault()?.ListaId;
}
