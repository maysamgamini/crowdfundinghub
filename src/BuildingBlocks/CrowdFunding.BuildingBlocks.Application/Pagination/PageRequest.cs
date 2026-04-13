namespace CrowdFunding.BuildingBlocks.Application.Pagination;

/// <summary>
/// Represents paging input used by list queries.
/// </summary>
public sealed record PageRequest(int PageNumber, int PageSize)
{
    public int Skip => (PageNumber - 1) * PageSize;

    public static PageRequest Create(
        int? pageNumber,
        int? pageSize,
        int defaultPageSize = 20,
        int maxPageSize = 100)
    {
        var normalizedPageNumber = pageNumber.GetValueOrDefault(1);
        var normalizedPageSize = pageSize.GetValueOrDefault(defaultPageSize);

        if (normalizedPageNumber < 1)
        {
            normalizedPageNumber = 1;
        }

        if (normalizedPageSize < 1)
        {
            normalizedPageSize = defaultPageSize;
        }

        if (normalizedPageSize > maxPageSize)
        {
            normalizedPageSize = maxPageSize;
        }

        return new PageRequest(normalizedPageNumber, normalizedPageSize);
    }
}
