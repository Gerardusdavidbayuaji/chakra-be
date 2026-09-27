using Gridify;

namespace Chakra.Application.Common;

public class PaginatedRequest
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Filter { get; set; }
    public string? OrderBy { get; set; }

    public GridifyQuery ToGridifyQuery() => new()
    {
        Page = Page is null or < 1 ? 1 : Page.Value,
        PageSize = PageSize is null or < 1 ? 10 : PageSize.Value,
        Filter = Filter,
        OrderBy = OrderBy
    };
}

public class PaginatedResult<T>
{
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public int Count { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}