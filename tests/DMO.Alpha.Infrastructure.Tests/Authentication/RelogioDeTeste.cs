namespace DMO.Alpha.Infrastructure.Tests.Authentication;

/// <summary>Um relógio parado que os testes avançam à mão.</summary>
internal sealed class Relogio : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Agora;
}
