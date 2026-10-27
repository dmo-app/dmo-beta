namespace DMO.Alpha.Core.Admin;

/// <summary>
/// Os MÓDULOS que um template pode dar (o Owner 2026-10-16: «os modulos sao
/// basicamente as paginas e suas funcoes»). Cada chave abre um conjunto de
/// páginas; a página declara o seu módulo ([ModuleEntry]) e a entrada é
/// verificada uma vez, antes de qualquer handler. Dentro do módulo não há
/// verificações por botão. As chaves são as strings já guardadas em
/// Template.Capabilities (sem migração).
/// </summary>
/// <param name="EmDesenvolvimento">
/// Módulo por acabar (plano de uma versão, §2): desligado por omissão e,
/// quando ligado, mostra a faixa «Em desenvolvimento».
/// </param>
public sealed record DmoModule(string Key, string Label, string Home, bool EmDesenvolvimento = false);

public static class DmoCapabilities
{
    /// <summary>Planeamento (Job On). A chave histórica mantém-se.</summary>
    public const string JobOnCreate = "jobon.create";

    /// <summary>
    /// Job On ver (Diogo, 22/10/2026): abre a folha do Job On só em leitura,
    /// sem Editar nem Eliminar. Quem tem <see cref="JobOnCreate"/> vê e edita.
    /// </summary>
    public const string JobOnView = "jobon.view";

    /// <summary>Controlo Criar: Resumo, Peso, Pegamentos, Comparação e Histórico.</summary>
    public const string ControloCreate = "controlo.create";

    /// <summary>Controlo Aprovar: a revisão e a aprovação.</summary>
    public const string ControloApprove = "controlo.approve";

    /// <summary>Boquilhas · criar: registo, ficheiros, histórico e definições. A chave histórica mantém-se.</summary>
    public const string Boquilhas = "boquilhas";

    /// <summary>
    /// Boquilhas · ver (Diogo, 22/10/2026: «para ele é ver apenas os
    /// registos»): só o Histórico, em leitura, com o painel das linhas.
    /// Sem registar, sem corrigir, sem definições.
    /// </summary>
    public const string BoquilhasView = "boquilhas.view";

    /// <summary>Ferramentas: ver, criar e editar fichas.</summary>
    public const string Ferramentas = "ferramentas";

    /// <summary>
    /// Armazém, gestão (módulo do full, peça E do plano): as posições, os
    /// reparadores de CM e MF e, a seguir, a Entrada, a Saída e o Corrigir
    /// localização. A consulta terá a sua chave (<c>armazem.consulta</c>).
    /// </summary>
    public const string ArmazemGestao = "armazem.gestao";

    /// <summary>
    /// Reparação Interna (módulo do full, peça D do plano): registar na
    /// máquina cada número de CM ou MF reparado e consultar o próprio histórico.
    /// A chave histórica «ri» mantém só este acesso; nunca concede a consulta
    /// dos responsáveis. Só o ADMIN a dá num Template.
    /// </summary>
    public const string ReparacaoInterna = "ri";

    /// <summary>Reparação Interna · ver: consulta dos responsáveis e associação de ferramentas.</summary>
    public const string ReparacaoInternaView = "ri.view";

    /// <summary>
    /// A lista única dos módulos, pela ordem da barra principal. O menu, a
    /// página inicial depois do login e a gestão de templates saem daqui.
    /// </summary>
    public static readonly IReadOnlyList<DmoModule> Modules =
    [
        new(JobOnCreate, "Planeamento", "/JobOn"),
        new(JobOnView, "Planeamento", "/JobOn"),
        new(ControloCreate, "Controlo", "/Controlo/Resumo"),
        new(ControloApprove, "Controlo", "/Controlo/Approve"),
        new(Boquilhas, "Boquilhas", "/Boquilhas"),
        new(BoquilhasView, "Boquilhas", BoquilhasViewHome),
        new(Ferramentas, "Ferramentas", "/Ferramentas"),
        new(ArmazemGestao, "Armazém", "/Armazem/Movimentos", EmDesenvolvimento: true),
        new(ReparacaoInterna, "Reparação Interna", "/ReparacaoInterna", EmDesenvolvimento: true),
        new(ReparacaoInternaView, "Reparação Interna", "/ReparacaoInterna/Consultar", EmDesenvolvimento: true)
    ];

    /// <summary>A entrada de quem só vê as Boquilhas: o Histórico, um movimento por linha.</summary>
    public const string BoquilhasViewHome = "/Boquilhas?historico=1&vista=movimentos";

    /// <summary>Só «Boquilhas · ver», sem «criar»: as Boquilhas em leitura.</summary>
    public static bool BoquilhasOnlyView(IReadOnlyList<string> capabilities) =>
        capabilities.Contains(BoquilhasView, StringComparer.Ordinal)
        && !capabilities.Contains(Boquilhas, StringComparer.Ordinal);

    /// <summary>As chaves válidas num template (validação da gestão de templates).</summary>
    public static readonly IReadOnlyList<string> BetaSet = Modules.Select(m => m.Key).ToArray();

    /// <summary>A página do primeiro módulo dado, ou null quando não há nenhum.</summary>
    public static string? HomeFor(IReadOnlyList<string> capabilities) =>
        Modules.FirstOrDefault(m => capabilities.Contains(m.Key, StringComparer.Ordinal))?.Home;

    /// <summary>
    /// Whether a capability string belongs to the current Beta vocabulary
    /// (exact ordinal match; capability names are never fuzzy-matched and
    /// never derived from labels).
    /// </summary>
    /// <summary>
    /// O template dá os dois Controlos: a mesma pessoa cria e aprova. É
    /// permitido (são páginas separadas) e o Admin → Templates avisa.
    /// </summary>
    public static bool GrantsBothControlos(IReadOnlyList<string> capabilities) =>
        capabilities.Contains(ControloCreate, StringComparer.Ordinal)
        && capabilities.Contains(ControloApprove, StringComparer.Ordinal);

    /// <summary>
    /// Os módulos do menu: um por nome. Com os dois Controlos fica só
    /// «Controlo», que abre o Criar; a Revisão abre-se lá dentro.
    /// </summary>
    public static IReadOnlyList<DmoModule> MenuModules(IReadOnlyList<string> granted) =>
        Modules.Where(m => granted.Contains(m.Key, StringComparer.Ordinal))
            .DistinctBy(m => m.Label)
            .ToArray();

    public static bool IsBetaCapability(string capability) =>
        BetaSet.Contains(capability, StringComparer.Ordinal);
}
