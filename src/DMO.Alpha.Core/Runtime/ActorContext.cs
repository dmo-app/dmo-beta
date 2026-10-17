namespace DMO.Alpha.Core.Runtime;

public sealed record ActorContext
{
    public static ActorContext Anonymous { get; } = new();

    public bool IsAuthenticated { get; init; }

    public Guid? ActorId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? ProfileLabel { get; init; }

    public bool IsAdmin { get; init; }

    /// <summary>
    /// Operator / BA Glass ID for normal Users. Null for ADMIN.
    /// </summary>
    public string? OperatorId { get; init; }
}
