namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Leitura das definições funcionais por módulo (a decisão do Owner: as
/// definições vivem no Admin, uma secção por módulo — spec CONFIGURATION.md
/// e o desenho das Boquilhas 2026-10-11).
/// </summary>
public interface IModuleSettingsQuery
{
    /// <summary>O valor configurado para a chave, ou null quando ainda não configurada.</summary>
    Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default);
}

/// <summary>Comando: gravar uma definição de módulo (upsert por chave canónica).</summary>
public sealed record SaveModuleSetting(string SettingKey, string SettingValue);

/// <summary>Recusas tipadas da gravação (vocabulário fechado, nunca texto livre).</summary>
public enum SaveModuleSettingResult
{
    Success,

    /// <summary>A chave não é uma das canónicas conhecidas (vocabulário fechado).</summary>
    UnknownSettingKey,

    /// <summary>O valor está vazio — nenhuma definição fica em branco por omissão.</summary>
    EmptyValue,

    /// <summary>Boquilhas: a hora de viragem tem de ser um inteiro 0–23.</summary>
    BoquilhasInvalidHour,

    /// <summary>
    /// Boquilhas (rota por máquina): o valor não é o Id de uma entidade
    /// reparadora existente (as chaves Boquilhas:RepairerB1..C3 — o
    /// dropdown da Admin oferece apenas os existentes; o valor vazio é
    /// LIMPAR a rota da máquina, nunca esta recusa).
    /// </summary>
    BoquilhasInvalidRepairerId,

    /// <summary>Reparação Interna: os minutos do posto não são uma das opções.</summary>
    PostoMinutosInvalidos
}

public sealed record SaveModuleSettingOutcome(SaveModuleSettingResult Result);

/// <summary>Handler da gravação das definições de módulo.</summary>
public interface ISaveModuleSettingHandler
{
    Task<SaveModuleSettingOutcome> HandleAsync(SaveModuleSetting command, CancellationToken cancellationToken = default);
}

/// <summary>
/// A HORA DE VIRAGEM do dia operacional das Boquilhas (a decisão do Owner
/// 2026-10-11: a produção corrente de uma máquina muda à hora configurada
/// — nunca à meia-noite, nunca ao criar o Job On). A resolução temporal:
/// uma produção cujas datas do Job On cobrem o momento em que o relógio
/// passa a hora de viragem torna-se a corrente a partir desse instante.
/// </summary>
public static class BoquilhasDayChange
{
    /// <summary>
    /// O dia operacional que contém o momento dado: HOJE quando o relógio
    /// já passou a hora de viragem, ONTEM quando ainda não passou (o dia
    /// operacional começa à hora configurada, o padrão 8h — a manhã da
    /// fábrica; nunca 0h). O momento é a hora local de Lisboa: em UTC a
    /// viragem mudava uma hora no verão.
    /// </summary>
    public static DateOnly ResolveOperationalDay(DateTime momentLocal, int dayChangeHour)
    {
        if (dayChangeHour is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(dayChangeHour), "A hora de viragem é um inteiro 0–23.");
        }

        return momentLocal.Hour >= dayChangeHour
            ? DateOnly.FromDateTime(momentLocal)
            : DateOnly.FromDateTime(momentLocal.AddDays(-1));
    }
}
