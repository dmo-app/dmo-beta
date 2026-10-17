namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// O RAIL DAS LINHAS — a forma única (o contrato de UI da casa, regra 1:
/// «UM componente único com O MESMO traje em TODAS as páginas... NUNCA
/// variantes por página — um partial + um CSS = a única fonte»).
///
/// Até aqui existiam três verdades: as Boquilhas montavam
/// <c>_DmoSidepanelBoquilhas</c> com <c>&lt;button onclick&gt;</c>, o Job On
/// tinha um <c>&lt;aside&gt;</c> escrito à mão na própria folha, e o
/// <c>_DmoSidepanelPlaneamento</c> — que o blueprint lista como
/// implementado — renderizava botões com <c>data-jobon-href</c> que
/// NENHUM script lê (o <c>beta-jobon.js</c> foi apagado da árvore), ou seja
/// markup morto. Resultado medido no HTML servido: Job On e Boquilhas com
/// rails diferentes e as três páginas do Controlo sem rail nenhum.
///
/// Isto é a apresentação; NENHUMA destas regras é o read model. Quem tem a
/// forma (máquina + detalhe + para onde vai) monta-a, o partial só veste. E
/// navegar é um <c>&lt;a href&gt;</c>: funciona sem JavaScript, como manda a
/// lei do Owner («tudo razor, o js era só para o mockup»).
/// </summary>
public sealed record DmoRailPresentation(
    string Title,
    string Subtitle,
    string AriaLabel,
    IReadOnlyList<DmoRailItem> Items,
    // O estado truthful quando não há nada em linha nenhuma: nunca uma linha
    // inventada, nunca um painel vazio sem explicação.
    string EmptyLabel = "Sem linhas.",
    // O id DOM, quando a folha precisa de o ancorar (as Boquilhas ligam o
    // layout do grid a #bqCurrentLines). Nulo = sem id.
    string? Id = null,
    // Navegação direta, pedida para o painel das Boquilhas em 16/10/2026.
    bool OpenOnClick = false);

/// <summary>
/// UMA linha do rail. A <c>Href</c> existe só quando há realmente aonde ir:
/// sem destino a linha nasce como estado não-clicável («Sem produção»), e o
/// clique nunca é um botão que não faz nada — foi exatamente assim que o
/// partial partilhado ficou inerte.
///
/// A fronteira com a referência HTML (o Owner 2026-10-14: «o html foi feito
/// para uso local»): a seleção é APENAS apresentação. Nada aqui decide dados,
/// nada vive no browser, e abrir é navegar — é o servidor que recompõe o
/// contexto da produção a partir do Job On, nunca um estado de página no cliente.
/// </summary>
/// <param name="Key">O identificador opaco da linha para quem quiser reagir à
/// seleção (o <c>data-id</c> do contrato canónico). Apresentação only: quem age
/// usa sempre o <c>Href</c>, que é o que o servidor sabe recompór.</param>
public sealed record DmoRailItem(
    string Code,
    string Detail,
    string? Href = null,
    string? AriaLabel = null,
    string? Key = null);
