namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado da eliminação: o outcome tipado (a Tool apagada não devolve
/// read model — a ficha deixa de existir e o histórico vive nos factos
/// congelados dos registos de consumo reatribuídos à DUMMY).
/// </summary>
public sealed record DeleteToolOutcome(DeleteToolResult Result);
