namespace CrowdFunding.API.Contracts.Common;

/// <summary>
/// Represents a paginated HTTP response payload wrapping a subset of items.
/// </summary>
/// <typeparam name="T">The model type contained within the paginated list.</typeparam>
/// <param name="Items">The items belonging to the requested page.</param>
/// <param name="PageNumber">The current page number (1-based index).</param>
/// <param name="PageSize">The maximum number of items returned per page.</param>
/// <param name="TotalCount">The total number of items matching the query criteria across all pages.</param>
/// <param name="TotalPages">The total number of available pages.</param>
public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
