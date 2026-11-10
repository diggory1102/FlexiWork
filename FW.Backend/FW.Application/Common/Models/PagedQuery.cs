namespace FW.Application.Common.Models;

/// <summary>
/// Tham số phân trang & tìm kiếm dùng chung cho các API danh sách.
/// </summary>
public class PagedQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Trang hiện tại, bắt đầu từ 1.</summary>
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Từ khóa tìm kiếm (tìm gần đúng trên các cột do từng repository quy định).</summary>
    public string? Search { get; set; }

    public int NormalizedPageIndex => PageIndex < 1 ? 1 : PageIndex;

    public int NormalizedPageSize => PageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };

    public int Skip => (NormalizedPageIndex - 1) * NormalizedPageSize;
}
