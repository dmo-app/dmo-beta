namespace DMO.Alpha.Core.Tools;

public sealed record AvisosSap(string ToolId, uint Versao, int? QuantidadeAtual, int? Pecas,
    decimal? UsoAtual, decimal? Uso, string? Fornecedor, DateOnly? DataAquisicao);

public enum DecisaoSapResultado { Gravado, Desatualizado, Invalido, NaoEncontrado }

public interface IFerramentasAvisosSap
{
    Task<AvisosSap?> LerAsync(string toolId, CancellationToken cancellationToken = default);
    Task<DecisaoSapResultado> DecidirAsync(string toolId, uint versao, string campo, bool aceitar, CancellationToken cancellationToken = default);
}
