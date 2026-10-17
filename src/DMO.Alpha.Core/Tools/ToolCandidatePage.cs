namespace DMO.Alpha.Core.Tools;

public sealed record ToolCandidatePage(IReadOnlyList<ToolCandidate> Items, int TotalCount, int PageNumber, int PageSize);
