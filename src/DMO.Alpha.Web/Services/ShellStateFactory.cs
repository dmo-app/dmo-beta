using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Presentation;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// Builds the shared shell state for native shell pages from the authenticated
/// actor context. Presentation only: it resolves no authorization, owns no
/// domain facts and exposes no capability beyond the current build registry.
/// Module pages with real read models replace the presentation values they own.
/// </summary>
public sealed class ShellStateFactory
{
    private readonly ICurrentAccountContext _accountContext;
    private readonly CurrentUserCapabilities _capabilities;
    private readonly SumarioNovidades _sumario;

    public ShellStateFactory(ICurrentAccountContext accountContext, CurrentUserCapabilities capabilities, SumarioNovidades sumario)
    {
        _accountContext = accountContext;
        _capabilities = capabilities;
        _sumario = sumario;
    }

    /// <summary>
    /// A barra principal: os módulos que o template dá, pela ordem da lista
    /// única (DmoCapabilities.Modules — Planeamento → Controlo → Boquilhas
    /// → Ferramentas, a ordem do Owner 2026-10-12). Os dois Controlos
    /// chamam-se «Controlo»: com os dois há uma só entrada, que fica
    /// marcada no Criar e na Revisão.
    /// </summary>
    public static IReadOnlyList<DestinationPresentation> ModuleDestinations(
        string? moduleKey, IReadOnlyList<string> granted) =>
        DmoCapabilities.MenuModules(granted)
            .Select(m => new DestinationPresentation(
                Label: m.Label,
                Href: m.Home,
                Selected: DmoCapabilities.Modules
                    .Where(other => other.Label == m.Label && granted.Contains(other.Key, StringComparer.Ordinal))
                    .Any(other => IsSelected(other.Key, moduleKey)),
                Visible: true,
                Enabled: true))
            .ToArray();

    /// <summary>
    /// True nas páginas de um módulo em desenvolvimento (Armazém, Reparação
    /// Interna): o layout mostra a faixa «Em desenvolvimento».
    /// </summary>
    public static bool IsEmDesenvolvimento(string? moduleKey) =>
        DmoCapabilities.Modules.Any(m => m.EmDesenvolvimento && IsSelected(m.Key, moduleKey));

    private static bool IsSelected(string module, string? moduleKey) => module switch
    {
        DmoCapabilities.JobOnCreate => moduleKey == "planeamento",
        DmoCapabilities.ControloCreate => moduleKey is "controlo" or "controlo-create",
        DmoCapabilities.ControloApprove => moduleKey == "controlo-approve",
        DmoCapabilities.Boquilhas => moduleKey == "boquilhas",
        DmoCapabilities.BoquilhasView => moduleKey == "boquilhas",
        DmoCapabilities.Ferramentas => moduleKey == "ferramentas",
        DmoCapabilities.ArmazemGestao => moduleKey == "armazem",
        DmoCapabilities.ReparacaoInterna => moduleKey == "reparacao-interna",
        DmoCapabilities.ReparacaoInternaView => moduleKey == "reparacao-interna",
        _ => false
    };

    /// <summary>
    /// Shell state for a destination without side panel or module functions.
    /// </summary>
    public async Task<ShellPresentation> ForDestinationAsync(string destinationKey, ModulePresentation? module)
    {
        var actor = _accountContext.Current;

        // O PAPEL do utilizador no cabeçalho (o Owner 2026-10-12: «a app
        // nao precisa de saber so precisa de mostrar os templates
        // associados ao user quando entrar, e o template trata do resto»):
        // o NOME do template CORRENTE resolvido por pedido — nunca o label
        // de login (stale quando o template muda). O ADMIN administra, não
        // tem papel operacional. Uma leitura assíncrona por pedido: as
        // capabilities abaixo reutilizam o mesmo resultado.
        var papel = actor.IsAuthenticated && !actor.IsAdmin
            ? await _capabilities.GetTemplateNameAsync()
            : null;

        // Os destinos são os módulos do template, lidos uma vez por pedido
        // (a entrada no módulo já os leu; aqui vêm da mesma leitura).
        var granted = actor.IsAuthenticated && !actor.IsAdmin
            ? await _capabilities.GetAsync()
            : Array.Empty<string>();
        var destinations = ModuleDestinations(module?.Key, granted);

        // Com os dois Controlos há um só «Controlo» no menu: a barra de
        // cada um leva ao outro.
        IReadOnlyList<FunctionPresentation>? moduleLinks = !DmoCapabilities.GrantsBothControlos(granted) ? null
            : module?.Key is "controlo" or "controlo-create"
                ? [new FunctionPresentation("revisao", "Revisão", "/Controlo/Approve", null, false, true, true)]
            : module?.Key == "controlo-approve"
                ? [new FunctionPresentation("resumo", "Resumo", "/Controlo/Resumo", null, false, true, true)]
            : null;

        return new ShellPresentation(
            module,
            destinationKey,
            ActiveFunctionKey: null,
            Functions: Array.Empty<FunctionPresentation>(),
            // A BARA DO ADMIN NÃO TEM OS MÓDULOS OPERACIONAIS (o Owner
            // 2026-10-12: «a pagina admin nao deveria mostrar planeamento
            // controlo boquilhas nem ferramentas» + a precisão: «nao deveria
            // esconder.. devia nao ter» — o admin SÓ administra: a regra
            // 0mv4f8b2m; a barra dele nasce VAZIA de módulos, não escondida:
            // os destinos operacionais NÃO EXISTEM no shell do admin).
            Destinations: actor.IsAdmin
                ? Array.Empty<DestinationPresentation>()
                : destinations,
            Planeamento: new DestinationPresentation(
                Label: "Planeamento",
                Href: string.Empty,
                Selected: false,
                // idem: o admin não TEM o atalho do Planeamento
                Visible: !actor.IsAdmin,
                Enabled: false),
            Session: new SessionPresentation(
                Name: actor.IsAuthenticated ? actor.DisplayName : string.Empty,
                Title: papel ?? actor.ProfileLabel ?? string.Empty,
                LogoutHref: "/logout"),
            ActionAvailability: new Dictionary<string, ActionAvailability>(),
            Sidepanel: new SidepanelPresentation(ShellSidepanelKind.None, Visible: false),
            ModuleLinks: moduleLinks);
    }

    /// <summary>
    /// A barra do Controlo Aprovar: a Revisão e, ao lado, o Resumo do
    /// controlo (Diogo, 19/10/2026: «um tab ao lado da revisão onde podemos
    /// procurar pelos Job Ons já feitos, e o chefe ver tipo histórico»).
    /// </summary>
    public async Task<ShellPresentation> ForControloReviewAsync(int? jobOnId = null, string activeKey = "revisao") =>
        await ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo-approve", "Controlo"))
        with
        {
            ActiveFunctionKey = activeKey,
            Functions = [
                new FunctionPresentation("revisao", "Revisão",
                    jobOnId is int id ? $"/Controlo/Approve?jobon_id={id}" : "/Controlo/Approve",
                    null, activeKey == "revisao", true, true),
                new FunctionPresentation("resumo-controlo", "Resumo do controlo",
                    jobOnId is int resumo ? $"/Controlo/Approve/Detalhe?tipo=controlo&jobon_id={resumo}" : "/Controlo/Approve/Detalhe?tipo=controlo",
                    null, activeKey == "resumo-controlo", true, true)
            ]
        };

    /// <summary>
    /// Navegação do módulo Controlo Criar. O Controlo Aprovar tem a sua
    /// própria barra (<see cref="ForControloReview"/>). Menos separadores
    /// (o Owner 16/10/2026): «Resumo», «Funções» com as folhas da produção
    /// num dropdown (Peso, Pegamentos, PU NNPB) e depois Histórico. A
    /// Comparação de Peso não tem separador: abre-se no Resumo da produção
    /// (Diogo, 21/10/2026: «esta comparação passa a existir dentro do
    /// controlo_id»).
    /// As Definições do Controlo (a pasta partilhada dos PDFs e a tolerância
    /// dos Pegamentos) fecham a barra (Diogo, 17/10/2026: «associaçao do dir
    /// fica no controlo.create»).
    /// </summary>
    public static IReadOnlyList<FunctionPresentation> ControloFunctions(
        string activeKey, int? jobOnId = null, int? controloId = null)
    {
        // A tab é a função, não uma página independente do contexto.
        // Guardar a identidade nos links permite navegar sem perder o Job On.
        var resumo = jobOnId is int j ? $"/Controlo/Resumo?jobon_id={j}" : "/Controlo/Resumo";
        var peso = controloId is int c ? $"/Controlo/Peso?controlo_id={c}" : "/Controlo/Peso";
        var pegamentos = controloId is int p ? $"/Controlo/Pegamentos?controlo_id={p}" : "/Controlo/Pegamentos";
        var puncoes = controloId is int pu ? $"/Controlo/Puncoes?controlo_id={pu}" : "/Controlo/Puncoes";
        var historico = "/Controlo/Peso?historico=1";
        var funcoes = new[]
        {
            new FunctionPresentation("peso", "Peso", peso, null, activeKey == "peso", true, true),
            new FunctionPresentation("pegamentos", "Pegamentos", pegamentos, null, activeKey == "pegamentos", true, true),
            new FunctionPresentation("puncoes", "PU NNPB", puncoes, null, activeKey == "puncoes", true, true)
        };
        return new[]
        {
            new FunctionPresentation("resumo", "Resumo", resumo, null, activeKey == "resumo", true, true),
            new FunctionPresentation("funcoes", "Funções", null, null, funcoes.Any(f => f.Selected), true, true,
                Children: funcoes),
            new FunctionPresentation("historico", "Histórico", historico, null, activeKey == "historico", true, true)
        };
    }

    /// <summary>
    /// AS FUNÇÕES DO MÓDULO PLANEAMENTO (o Owner 2026-10-12: «o jobon
    /// fica no tab do planeamento, aparece no segundo tab. O jobon é uma
    /// função do planeamento» + a refunda do calendário 2026-10: «esse
    /// calendário está mal… deve ser como está no front end referência»):
    /// JOB ON (a folha do planeamento refundada pelo mockup — o
    /// dmo-calendar do mês, a lista do dia e a folha da produção) e
    /// HISTÓRICO (as produções anteriores com as associações das tools e
    /// os sem dia — o padrão `?historico=1` do Controlo/Peso). Nunca um
    /// item mestre próprio na barra principal. O «Calendário» da grelha
    /// máquinas×31-dias morreu com a refunda: o calendário vivo é o
    /// dmo-calendar DENTRO do Job On. SUMÁRIO ao lado do Job On (Diogo,
    /// 19/10/2026): o que há para fazer e o que mudou, pelos módulos da conta.
    /// </summary>
    /// <param name="soVer">Só com o Job On ver: não há «Histórico», que pede o Job On
    /// criar (auditoria de 27/10/2026). O «Job On» abre o Planeamento em leitura
    /// (Diogo, 27/10/2026: «jobon view esta sem planeamento»).</param>
    public static IReadOnlyList<FunctionPresentation> PlanningFunctions(string activeKey, bool sumarioNovo = false, bool soVer = false) =>
        soVer
        ? new[]
        {
            new FunctionPresentation("jobon", "Job On", "/JobOn", null, activeKey == "jobon", true, true),
            new FunctionPresentation("sumario", "Sumário", "/Planeamento/Sumario", null, activeKey == "sumario", true, true,
                sumarioNovo ? "dmo-tab-novo" : "")
        }
        : new[]
        {
            new FunctionPresentation("jobon", "Job On", "/JobOn", null, activeKey == "jobon", true, true),
            new FunctionPresentation("sumario", "Sumário", "/Planeamento/Sumario", null, activeKey == "sumario", true, true,
                sumarioNovo ? "dmo-tab-novo" : ""),
            new FunctionPresentation("historico", "Histórico", "/JobOn?historico=1", null, activeKey == "historico", true, true)
        };

    /// <summary>
    /// As funções do Planeamento com o ponto do Sumário (Diogo, 23/10/2026:
    /// um ponto sem número quando há importantes por ver): no próprio Sumário
    /// não aparece, porque abri-lo é vê-los.
    /// </summary>
    public async Task<IReadOnlyList<FunctionPresentation>> PlanningFunctionsAsync(string activeKey, CancellationToken cancellationToken = default, bool soVer = false) =>
        PlanningFunctions(activeKey, activeKey != "sumario" && await _sumario.TemNovoAsync(cancellationToken), soVer);

    /// <summary>
    /// AS FUNÇÕES DO MÓDULO BOQUILHAS (o Owner 2026-10-13: «registo e
    /// historico sao botoes quando deveriam estar no tab»): as funções
    /// vivem na BARRA de funções (o padrão do Controlo/Planeamento — nunca
    /// botões no page-head) e as MÁQUINAS ficam no SIDEPANEL sempre visível
    /// ao lado (a produção corrente — o aside da folha, nunca uma tab
    /// escondida). As DEFINIÇÕES (os reparadores e a rota por máquina) fecham
    /// a barra (Diogo, 17/10/2026: «separamos as definições»).
    /// </summary>
    public static IReadOnlyList<FunctionPresentation> BoquilhasFunctions(string activeKey, bool onlyView = false) =>
        onlyView
        ? new[]
        {
            new FunctionPresentation("historico", "Movimentos", DmoCapabilities.BoquilhasViewHome, null, true, true, true)
        }
        : new[]
        {
            new FunctionPresentation("registo", "Registo", "/Boquilhas", null, activeKey == "registo", true, true),
            new FunctionPresentation("ficheiros", "Boquilhas", "/Boquilhas?ficheiros=1", null, activeKey == "ficheiros", true, true),
            new FunctionPresentation("historico", "Movimentos", "/Boquilhas?historico=1&vista=matriz", null, activeKey == "historico", true, true)
        };

    /// <summary>
    /// AS FUNÇÕES DO MÓDULO FERRAMENTAS (o Owner 2026-10-13: «deveria ser
    /// um tab para criar e outro para ver o que existe»): VER (a procura +
    /// a lista do que existe) e CRIAR (o registo da ferramenta real) —
    /// nunca tudo misturado numa só página. As DEFINIÇÕES (os totais de lote
    /// por máquina) fecham a barra (Diogo, 17/10/2026: «ferramentas nao deve
    /// ficar no admin»).
    /// </summary>
    public static IReadOnlyList<FunctionPresentation> FerramentasFunctions(string activeKey) =>
        new[]
        {
            new FunctionPresentation("ver", "Pesquisar", "/Ferramentas", null, activeKey == "ver", true, true),
            new FunctionPresentation("criar", "Criar", "/Ferramentas?criar=1", null, activeKey == "criar", true, true),
            // A Reparação Programada (Diogo, 22/10/2026): um separador das Ferramentas, sem chave própria.
            // Sem separador «Histórico» (Diogo, 28/10/2026: «deixar apenas pesquisar»); o histórico de cada ferramenta abre na ficha.
            new FunctionPresentation("programada", "Registar Reparação", "/Ferramentas/ReparacaoProgramada", null, activeKey == "programada", true, true)
        };

    /// <summary>
    /// As funções do Armazém (módulo do full): o Registo, os Movimentos (o que está fora em reparação e as últimas entradas e saídas, no lugar do Histórico; Diogo, 22/10/2026) e as Definições
    /// (os reparadores de CM e MF), que fecham a barra como nos outros módulos.
    /// </summary>
    public static IReadOnlyList<FunctionPresentation> ArmazemFunctions(string activeKey) =>
        new[]
        {
            new FunctionPresentation("movimentos", "Registo", "/Armazem/Movimentos", null, activeKey == "movimentos", true, true),
            new FunctionPresentation("ultimos", "Movimentos", "/Armazem/Ultimos", null, activeKey == "ultimos", true, true),
            new FunctionPresentation("pesquisar", "Pesquisar", "/Armazem/Pesquisar", null, activeKey == "pesquisar", true, true)
        };

    /// <summary>
    /// As funções da Reparação Interna (módulo do full): o Registar na
    /// máquina e a Consulta dos chefes (quem reparou o quê e em que produção).
    /// </summary>
    public static IReadOnlyList<FunctionPresentation> ReparacaoInternaFunctions(string activeKey, IReadOnlyList<string> granted) =>
        new[]
        {
            new FunctionPresentation("registar", "Registar", "/ReparacaoInterna", null, activeKey == "registar", granted.Contains(DmoCapabilities.ReparacaoInterna, StringComparer.Ordinal), true),
            new FunctionPresentation("historico", "Histórico", "/ReparacaoInterna?historico=1", null, activeKey == "historico", granted.Contains(DmoCapabilities.ReparacaoInterna, StringComparer.Ordinal), true),
            new FunctionPresentation("consultar", "Consultar", "/ReparacaoInterna/Consultar", null, activeKey == "consultar", granted.Contains(DmoCapabilities.ReparacaoInternaView, StringComparer.Ordinal), true)
        };
}
