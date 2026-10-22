using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// As capabilities CORRENTES do utilizador logado (o corte H — o modelo
/// FINAL de permissões, o Owner 2026-10-12: «admin existe apenas para
/// entrar na pagina admin, dai criar users com os modulos que queremos,
/// isso é o que cria as permissoes»): o acesso efetivo resolvido PELO
/// SERVIÇO CANÓNICO do Admin (IAdminAuthorizationService — a autoridade
/// de TEMPLATES.md), por pedido (a edição do template muda o acesso
/// efetivo sem re-login — sem claims stale). O ADMIN SÓ administra: sem
/// template, resolve a lista VAZIA pelo caminho comum (a fronteira ADMIN
/// do próprio serviço canónico devolve Capabilities=[]); a área /Admin
/// autoriza-se pelo CLAIM DmoClaimTypes.IsAdmin, nunca por capabilities
/// de produção. O «fix» de 10/10 (7990275 — o admin resolvia o BetaSet
/// inteiro e administrava E operava) está REVOGADO por design
/// 2026-10-12: o admin não tem superfícies de produção — as permissões
/// nascem dos USERS criados com os módulos (templates) atribuídos.
/// </summary>
public sealed class CurrentUserCapabilities(
    IAdminAuthorizationService authorization,
    ICurrentAccountContext account,
    IModuleSwitches switches)
{
    private IReadOnlyList<string>? _template;
    private IReadOnlyList<string>? _resolved;
    private string? _templateName;

    /// <summary>
    /// As capabilities efetivas do template corrente (vazio quando anónimo,
    /// sem template, stand-by ou ADMIN — o admin só administra).
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved is not null)
        {
            return _resolved;
        }

        var actor = account.Current;
        if (!actor.IsAuthenticated || actor.ActorId is not { } userId)
        {
            _resolved = Array.Empty<string>();
            return _resolved;
        }

        if (_template is null)
        {
            await ResolveTemplateAsync(userId, cancellationToken);
        }

        // Os interruptores dos módulos (Admin → Setup): um módulo desligado
        // para este utilizador sai das capabilities, por isso some do menu e
        // a entrada no módulo trata-o como sem acesso.
        var state = await switches.GetAsync(cancellationToken);
        _resolved = state.Filter(_template!, userId);
        return _resolved;
    }

    private async Task ResolveTemplateAsync(Guid userId, CancellationToken cancellationToken)
    {
        // O caminho COMUM para todos os autenticados (o corte H): o serviço
        // canónico resolve — para o ADMIN (sem template) devolve a lista
        // VAZIA (a fronteira ADMIN do AdminAuthorizationService), para os
        // operacionais devolve as capabilities do template corrente. O
        // bloco if (actor.IsAdmin) → BetaSet (o «fix» 7990275 de 10/10)
        // está REVOGADO: o admin perdeu as capabilities de produção —
        // administra apenas; os atos operacionais ficam com quem tem o
        // template com a capability.
        var effective = await authorization.ResolveEffectiveAccessAsync(userId, cancellationToken);
        _templateName = effective?.TemplateName;
        _template = effective?.Capabilities ?? Array.Empty<string>();
    }

    /// <summary>
    /// Guarda o acesso já lido pela validação da sessão, para o pedido não
    /// voltar à base pelo mesmo utilizador.
    /// </summary>
    public void Prime(EffectiveAccess access)
    {
        _templateName = access.TemplateName;
        _template = access.Capabilities;
        _resolved = null;
    }

    /// <summary>True quando o utilizador tem a capability no template corrente.</summary>
    public async Task<bool> HasAsync(string capability, CancellationToken cancellationToken = default)
    {
        var caps = await GetAsync(cancellationToken);
        return caps.Contains(capability, StringComparer.Ordinal);
    }

    /// <summary>
    /// O NOME do template corrente do utilizador (a ETIQUETA — o papel no
    /// cabeçalho ao entrar; o Owner 2026-10-12: «a app nao precisa de
    /// saber so precisa de mostrar os templates associados ao user quando
    /// entrar, e o template trata do resto»): o que ele É, não o que ele
    /// pode FAZER (isso são as capabilities, validadas nos POSTs como
    /// recusas tipadas). Null quando sem template ou ADMIN (o admin só
    /// administra — não tem papel operacional).
    /// </summary>
    public async Task<string?> GetTemplateNameAsync(CancellationToken cancellationToken = default)
    {
        if (_template is null)
        {
            await GetAsync(cancellationToken);
        }

        return _templateName;
    }
}
