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

/// <summary>
/// Builds <see cref="PagedResponse{T}"/> from an application-layer page.
/// </summary>
public static class PagedResponse
{
    /// <summary>
    /// Maps every item of <paramref name="page"/> and copies its paging metadata.
    /// </summary>
    /// <typeparam name="TSource">The application result item type.</typeparam>
    /// <typeparam name="T">The HTTP response item type.</typeparam>
    /// <param name="page">The application-layer page.</param>
    /// <param name="map">Maps one item.</param>
    /// <returns>The HTTP response page.</returns>
    public static PagedResponse<T> From<TSource, T>(CrowdFunding.BuildingBlocks.Application.Pagination.PagedResult<TSource> page, Func<TSource, T> map)
        => new(page.Items.Select(map).ToArray(), page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages);
}
