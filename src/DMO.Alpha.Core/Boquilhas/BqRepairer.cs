namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// ENTIDADE REPARADORA das Boquilhas (o Owner 2026-10-12, verbatim:
/// «reparadores sao as entidades para onde enviamos as boquilhas»): o
/// destino do encaminhamento das boquilhas que saem para reparação —
/// master data administrado em Admin → App Definições → Boquilhas.
/// A identidade canónica é o Id emitido pelo backend (nunca nas folhas
/// — só os fatos humanos: o Name); o movimento congela o nome no
/// momento da saída (BqRepairerNameFrozen), pelo que a verdade
/// histórica de cada movimento sobrevive à edição ou eliminação da
/// entidade (blueprint boquilhas/OVERVIEW.md L372-374: «The repairer
/// that applied when the real movement occurred remains part of that
/// movement's historical truth»).
/// </summary>
public sealed class BqRepairer
{
    public int Id { get; set; }

    /// <summary>
    /// O NOME humano do reparador (o fato que as folhas mostram e que o
    /// movimento congela na saída). Vocabulário administrado: o nome é
    /// único (dois reparadores com o mesmo nome seriam o mesmo fato
    /// humano para a rota); a unicidade é recusa TIPADA no handler, com
    /// o índice único como última linha da persistência.
    /// </summary>
    public string Name { get; set; } = null!;
}

/// <summary>
/// O VOCABULÁRIO DO ENCAMINHAMENTO por máquina de produção (blueprint
/// boquilhas/OVERVIEW.md L348-355: «Repair routing is configured by
/// production machine: B1; B2; B3; C1; C2; C3»). As 6 chaves
/// ModuleSettings <c>Boquilhas:Repairer&lt;code&gt;</c> — o valor é o Id
/// da entidade reparadora. A Saída NUNCA pergunta o reparador ao
/// operador (L357): a rota é BQ tool_id → a máquina (o contexto de
/// produção; antes de produção: a máquina associada da ficha da Tool)
/// → a config máquina→reparador → O REPARADOR usado pela Saída (L359-366).
/// </summary>
public static class BqRepairerRouting
{
    /// <summary>
    /// Os códigos canónicos das 6 máquinas de produção que o
    /// encaminhamento conhece (o vocabulário fechado do mapa da Admin —
    /// nunca texto livre).
    /// </summary>
    public static readonly string[] MachineCodes = ["B1", "B2", "B3", "C1", "C2", "C3"];

    /// <summary>
    /// A chave ModuleSettings da rota de uma máquina: o código B1..C3 →
    /// <c>Boquilhas:RepairerB1</c>..<c>Boquilhas:RepairerC3</c> (o CÓDIGO
    /// da máquina, nunca o Id da linha de Machines).
    /// </summary>
    public static string SettingKeyFor(string machineCode) => "Boquilhas:Repairer" + machineCode;

    /// <summary>
    /// TRUE quando a chave é uma das 6 canónicas do encaminhamento, com
    /// o código da máquina no out.
    /// </summary>
    public static bool TryGetMachineCode(string settingKey, out string machineCode)
    {
        machineCode = settingKey.StartsWith("Boquilhas:Repairer", StringComparison.Ordinal)
            ? settingKey["Boquilhas:Repairer".Length..]
            : string.Empty;

        return MachineCodes.Contains(machineCode);
    }
}
