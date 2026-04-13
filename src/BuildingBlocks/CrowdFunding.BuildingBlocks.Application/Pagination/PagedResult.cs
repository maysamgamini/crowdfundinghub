namespace CrowdFunding.BuildingBlocks.Application.Pagination;

/// <summary>
/// Represents a page of data returned by a query operation.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
