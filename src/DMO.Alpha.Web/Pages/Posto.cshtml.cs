using System.Runtime.CompilerServices;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace DMO.Alpha.Web.Pages;

/// <summary>
/// O posto da Reparação Interna (Diogo, 26/10/2026): quem tem a Reparação
/// Interna (ver ou criar) escolhe o seu nome e entra com o PIN do posto.
/// Como no login, a conta nunca fica bloqueada: depois de 5 PINs errados
/// o ecrã manda falar com o ADMIN, mas o PIN certo entra sempre.
/// </summary>
[AllowAnonymous]
public sealed class PostoModel(
    IPostoRi posto,
    DmoSignInService signIn,
    IMemoryCache cache,
    TravaoDoPosto travao) : PageModel
{
    public const int TentativasAntesDoAviso = 5;
    private const string Recusa = "Nome ou PIN errado.";

    public IReadOnlyList<PostoReparador> Reparadores { get; private set; } = [];

    public Guid? Escolhido { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync() => Reparadores = await posto.ListarAsync(HttpContext.RequestAborted);

    public async Task<IActionResult> OnPostAsync(string? utilizador, string? pin)
    {
        Reparadores = await posto.ListarAsync(HttpContext.RequestAborted);
        if (!Guid.TryParse(utilizador, out var id) || Reparadores.All(r => r.Id != id))
        {
            ErrorMessage = "Escolha o seu nome.";
            return Page();
        }

        Escolhido = id;
        var chave = "dmo-posto-falhas:" + id;
        // A origem confere um PIN de cada vez e espera mais a cada falha
        // (TravaoDoPosto); a conta em si nunca fica bloqueada. O IP vem de
        // antes do X-Forwarded-For (HttpContext.Items), não depois: esse já
        // é o que o cliente escreveu no cabeçalho, e qualquer um o muda a
        // cada pedido para nunca esperar (auditoria do Codex de 29/10/2026,
        // AUTH-B02).
        var origem = HttpContext.Items[TravaoDoPosto.ChaveIpDaLigacao] as string;
        using var vez = await travao.EsperarVezAsync(origem ?? "sem-origem", HttpContext.RequestAborted);
        if (!await posto.ConfereAsync(id, pin, HttpContext.RequestAborted))
        {
            vez.Falhou();
            var falhas = cache.GetOrCreate(chave, entrada =>
            {
                entrada.SlidingExpiration = TimeSpan.FromHours(1);
                return new StrongBox<int>();
            })!;
            await Task.Delay(400, HttpContext.RequestAborted);
            ErrorMessage = Interlocked.Increment(ref falhas.Value) >= TentativasAntesDoAviso
                ? "Demasiadas tentativas erradas. Consulte o ADMIN para rever o PIN."
                : Recusa;
            return Page();
        }

        cache.Remove(chave);
        var resultado = await signIn.SignInPostoAsync(id);
        if (!resultado.Succeeded)
        {
            ErrorMessage = resultado.ErrorMessage;
            return Page();
        }

        return Redirect(resultado.RedirectUrl!);
    }
}
