namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface de escrita canónica: registar uma Tool real no registo
/// Ferramentas. O handler minta o tool_id («T» + 10 hex minúsculos com
/// verificação de existência + re-mint), valida os factos com recusas
/// tipadas do enum fechado ANTES de qualquer escrita e grava a Tool com as
/// ligações de máquina aplicáveis numa única escrita atómica. GET-OR-CREATE
/// (o Owner 2026-10-12: «ferramentas duplicadas sao os lotes diferentes,
/// ou maquinas diferentes»): a chave de unicidade é (tipo, referência,
/// lote) — os mesmos factos devolvem a Tool EXISTENTE com as máquinas do
/// form acrescentadas ao ToolMachineLink dela; um lote diferente é a
/// criação nova.
/// </summary>
public interface ICreateToolHandler
{
    Task<CreateToolOutcome> HandleAsync(
        CreateTool command,
        CancellationToken cancellationToken = default);
}
