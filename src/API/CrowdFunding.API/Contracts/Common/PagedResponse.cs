namespace CrowdFunding.API.Contracts.Common;

/// <summary>
/// Represents a paged HTTP response payload.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
