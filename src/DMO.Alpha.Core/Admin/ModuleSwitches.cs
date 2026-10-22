namespace DMO.Alpha.Core.Admin;

/// <summary>
/// O interruptor de um módulo nas Definições do Admin (plano de uma versão,
/// §2). O interruptor não dá o módulo: quem entra continua a precisar do
/// módulo no seu template. Desligado, o módulo some do menu e as páginas
/// comportam-se como sem acesso.
/// </summary>
public enum ModuleSwitchMode
{
    /// <summary>Ninguém entra no módulo.</summary>
    Desligado,

    /// <summary>Só os utilizadores de teste escolhidos pelo ADMIN.</summary>
    Teste,

    /// <summary>Todos os que têm o módulo no template.</summary>
    Todos
}

/// <summary>Os interruptores guardados: o modo de cada módulo e os utilizadores de teste.</summary>
public sealed record ModuleSwitchState(
    IReadOnlyDictionary<string, ModuleSwitchMode> Modes,
    IReadOnlyList<Guid> Testers)
{
    public ModuleSwitchMode ModeOf(string moduleKey) =>
        Modes.TryGetValue(moduleKey, out var mode) ? mode : ModuleSwitchMode.Todos;

    /// <summary>True quando o módulo está ligado para este utilizador.</summary>
    public bool IsOnFor(string moduleKey, Guid? userId) => ModeOf(moduleKey) switch
    {
        ModuleSwitchMode.Todos => true,
        ModuleSwitchMode.Teste => userId is { } id && Testers.Contains(id),
        _ => false
    };

    /// <summary>As capabilities do template sem as dos módulos desligados para este utilizador.</summary>
    public IReadOnlyList<string> Filter(IReadOnlyList<string> capabilities, Guid? userId) =>
        capabilities.Where(c => !DmoCapabilities.IsBetaCapability(c) || IsOnFor(c, userId)).ToArray();
}

public interface IModuleSwitches
{
    Task<ModuleSwitchState> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyDictionary<string, ModuleSwitchMode> modes,
        IReadOnlyList<Guid> testers,
        CancellationToken cancellationToken = default);
}

/// <summary>As chaves e os valores dos interruptores na tabela ModuleSettings.</summary>
public static class ModuleSwitchKeys
{
    public const string Prefix = "Modulo:";
    public const string Testers = "Modulos:Testadores";

    public static string For(string moduleKey) => Prefix + moduleKey;

    public static string ToValue(ModuleSwitchMode mode) => mode switch
    {
        ModuleSwitchMode.Desligado => "desligado",
        ModuleSwitchMode.Teste => "teste",
        _ => "todos"
    };

    public static ModuleSwitchMode? Parse(string? value) => value?.Trim() switch
    {
        "desligado" => ModuleSwitchMode.Desligado,
        "teste" => ModuleSwitchMode.Teste,
        "todos" => ModuleSwitchMode.Todos,
        _ => null
    };

    /// <summary>O estado de um módulo em desenvolvimento sem linha guardada:
    /// o valor da configuração, ou desligado quando falta ou não se percebe.</summary>
    public static ModuleSwitchMode DefaultEmDesenvolvimento(string? value) => Parse(value) ?? ModuleSwitchMode.Desligado;
}
