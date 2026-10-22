namespace DMO.Alpha.Core.Admin;

/// <summary>
/// O PIN DAS DEFINIÇÕES (Diogo, 22/10/2026: «as definições do cadeado são
/// unlocked usando um pin criado no admin»). O ADMIN cria-o ou muda-o no
/// Setup; quem o escreve no cadeado muda a parte Geral das Definições até
/// sair. Guarda-se só o hash (PBKDF2 com sal), nunca o PIN.
/// </summary>
public interface IPinDasDefinicoes
{
    /// <summary>O hash guardado, ou null sem PIN criado.</summary>
    Task<string?> HashAsync(CancellationToken cancellationToken = default);

    Task<PinGravacao> DefinirAsync(string? pin, CancellationToken cancellationToken = default);

    Task<bool> ConfereAsync(string? pin, CancellationToken cancellationToken = default);
}

public enum PinGravacao
{
    Gravado,

    /// <summary>O PIN tem de ter de 4 a 12 algarismos.</summary>
    Invalido
}

public static class PinDasDefinicoesRegras
{
    public const int Minimo = 4;

    public const int Maximo = 12;

    /// <summary>A chave em ModuleSettings onde fica o hash.</summary>
    public const string Chave = "Definicoes:PinHash";

    public static bool Valido(string? pin) =>
        pin is { Length: >= Minimo and <= Maximo } && pin.All(char.IsAsciiDigit);
}
