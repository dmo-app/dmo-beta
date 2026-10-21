namespace DMO.Alpha.Core.Production;

/// <summary>
/// Comando: editar um Job On EXISTENTE — a produção re-planeada. A
/// janela temporal (inicio→fim), a linha (a máquina âncora canónica) e
/// as ferramentas da combinação (os tool_ids canônicos dos contextos
/// CM/MF/BQ) mudam SEM criar outra produção: o jobon_id é o MESMO. A
/// IDENTIDADE da produção — a Reference e o ProductionNumber — é
/// IMUTÁVEL na edição: o comando nem as recebe (a folha de edição
/// envia-as read-only e o handler nem as lê). Um slot null significa
/// «não mexer»: o contexto existente desse tipo fica como está (os
/// traces/movimentos são factos históricos ancorados ao mesmo
/// contexto); um slot com tool_id grava a nova Tool no contexto — o
/// MESMO bq_id com o ToolId novo, os factos históricos ficam.
/// </summary>
public sealed record UpdateJobOn(
    int JobOnId,
    int MachineId,
    string? CmToolId = null,
    string? MfToolId = null,
    string? BqToolId = null,
    // O dia em que a produção entra em máquina (o corte B); null = a janela em aberto.
    DateOnly? InicioEm = null,
    // O dia em que sai de máquina; null = até aviso.
    DateOnly? FimEm = null,
    // A CALOTE DESTA PRODUÇÃO (mm) — o campo j-calote da ficha do
    // dmo-html, EDITÁVEL na ficha: o valor submetido grava SEMPRE (o campo
    // vem preenchido com o valor corrente da ficha); null = a calote limpa
    // (a referência da Tool manda — o apoio TP/calote do Peso usa-a
    // automaticamente). Negativa é recusa tipada (InvalidCalote).
    decimal? CaloteMm = null,
    // Quem edita (o nome, senão o nº de operador ou o email), congelado em
    // cada alteração registada; null quando o pedido chega sem identidade.
    string? ChangedBy = null,
    // A versão da linha quando a ficha foi aberta (a página envia-a escondida);
    // null = sem verificação. Diferente da gravada = ChangedByAnotherUser,
    // salvo quando o pedido é o que já está gravado (o mesmo envio outra vez).
    uint? ExpectedVersion = null,
    // A FOLHA DA FÁBRICA: os campos escritos por chave do catálogo
    // (JobOnFolhaCampos) e as linhas CAL, pela ordem. Null = o envio não traz
    // a folha e ela fica como está. Gravam na mesma escrita do Job On, e o
    // que mudou fica numa linha de «O que mudou».
    IReadOnlyDictionary<string, string?>? Folha = null,
    IReadOnlyList<LinhaCalEnvio>? Cal = null);
