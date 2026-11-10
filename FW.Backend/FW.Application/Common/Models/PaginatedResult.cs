namespace FW.Application.Common.Models;

/// <summary>
/// Định dạng kết quả phân trang dùng chung cho các API danh sách.
/// </summary>
public class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public PaginatedResult(IReadOnlyList<T> items, int count, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = count;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    public static PaginatedResult<T> Create(IReadOnlyList<T> items, int count, int pageIndex, int pageSize)
    {
        return new PaginatedResult<T>(items, count, pageIndex, pageSize);
    }

    /// <summary>Chuyển đổi từng phần tử (vd: Entity → DTO), giữ nguyên thông tin phân trang.</summary>
    public PaginatedResult<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        return new PaginatedResult<TResult>(Items.Select(selector).ToList(), TotalCount, PageIndex, PageSize);
    }
}

