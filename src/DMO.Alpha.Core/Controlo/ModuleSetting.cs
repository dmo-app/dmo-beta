namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// As definições funcionais EDITÁVEIS POR MÓDULO (a decisão do Owner
/// 2026-10-11: «no admin vamos ter definições para cada modulo») — cada
/// chave canónica pertence a um módulo (o prefixo) e vive na mesma tabela:
///
///   Boquilhas:DayChangeHour    — a hora de viragem do dia operacional das
///                                boquilhas (o Owner: «dia 16/10 entra
///                                5447T173 na B1, jobon lança o ping, o
///                                módulo das boquilhas não deve acionar
///                                logo, apenas quando muda realmente... e
///                                não à meia-noite. Se meter as 8, às 8 da
///                                manhã o módulo boquilhas passa a mostrar
///                                T173 na B1»): a produção corrente de uma
///                                máquina é a cujas datas do Job On cobrem
///                                HOJE a partir dessa hora (o dia operacional
///                                conta das 8h às 8h do dia seguinte, o
///                                padrão da fábrica — nunca 0h).
///   Boquilhas:RepairerB1..C3   — o mapa do ENCAMINHAMENTO por máquina de
///                                produção (o Owner 2026-10-12: «reparadores
///                                sao as entidades para onde enviamos as
///                                boquilhas»): o valor é o Id da entidade
///                                BqRepairer usada pela Saída daquela máquina
///                                (o vocabulário fechado dos 6 códigos vive
///                                em BqRepairerRouting, Core/Boquilhas —
///                                as chaves pertencem ao módulo Boquilhas).
///
/// As chaves futuras da spec CONFIGURATION.md (templates de envio,
/// destinatários B/C) entram na mesma tabela com os seus prefixos — o
/// schema de persistência não é prescrito pelo Blueprint.
/// </summary>
public sealed class ModuleSetting
{
    /// <summary>A chave canônica namespaced pelo módulo (ex.: Boquilhas:DayChangeHour).</summary>
    public string SettingKey { get; set; } = null!;

    /// <summary>O valor configurado (o texto tal como o operador o confirmou).</summary>
    public string SettingValue { get; set; } = null!;
}

/// <summary>As chaves canónicas das definições por módulo (vocabulário fechado).</summary>
public static class ModuleSettingKeys
{
    /// <summary>
    /// Boquilhas — a hora de viragem do dia operacional (0–23; a
    /// produção corrente de uma máquina muda a esta hora, o padrão é 8).
    /// </summary>
    public const string BoquilhasDayChangeHour = "Boquilhas:DayChangeHour";

    /// <summary>
    /// O nome da pasta partilhada dos PDFs do Controlo (Definições → Controlo →
    /// Geral). O acesso à pasta dá-se em cada navegador; este nome diz qual é
    /// a pasta esperada e o navegador avisa se a escolhida tiver outro nome.
    /// </summary>
    public const string ControloPastaPdf = "Controlo:PastaPdf";

    /// <summary>O tamanho máximo do nome da pasta dos PDFs.</summary>
    public const int ControloPastaPdfMax = 200;

    /// <summary>
    /// Reparação Interna — a hora de viragem do dia operacional (0–23; a
    /// produção do dia de cada máquina muda a esta hora, o padrão é 8). Uma
    /// linha à parte da das Boquilhas no Canal Jobon (DP-15).
    /// </summary>
    public const string ReparacaoInternaDayChangeHour = "ReparacaoInterna:DayChangeHour";

    /// <summary>
    /// Reparação Interna — os minutos sem uso ao fim dos quais a sessão
    /// aberta pelo posto fecha sozinha (Diogo, 27/10/2026: «3min auto e
    /// opções nas definições para aumentar»). Ver PostoRiRegras.MinutosOpcoes.
    /// </summary>
    public const string ReparacaoInternaPostoMinutos = "ReparacaoInterna:PostoMinutos";

    /// <summary>
    /// Administração — o email do ADMIN (o ADMIN é apenas um email,
    /// guardado nas Definições).
    /// O login por email compara-se a este valor; um User com este email
    /// é o boundary (não é gerido como utilizador). Vazio num ambiente
    /// novo: o primeiro login por email com sucesso assume-o.
    /// </summary>
    public const string AdminEmail = "Admin:Email";

    /// <summary>
    /// Administração — a versão da sessão do ADMIN (o ADMIN não tem linha
    /// em Users): sobe quando o ADMIN muda a palavra-passe e as sessões
    /// abertas com a versão anterior terminam. Sem linha vale 0.
    /// </summary>
    public const string AdminSessionVersion = "Admin:SessionVersion";
}
