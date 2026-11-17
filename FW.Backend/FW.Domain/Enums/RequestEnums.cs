namespace FW.Domain.Enums;

/// <summary>
/// Nhóm đơn — quyết định đơn cần những thông tin gì và được xử lý ra sao (cột leave_types.category).
/// HR đặt tên loại đơn tùy ý; nghiệp vụ dựa vào nhóm.
/// </summary>
public static class RequestCategory
{
    /// <summary>Nghỉ theo ngày (phép năm, ốm, việc riêng...).</summary>
    public const string Leave = "leave";

    /// <summary>Đổi ca với đồng nghiệp hoặc nhờ đồng nghiệp làm thay — đồng nghiệp phải đồng ý trước.</summary>
    public const string ShiftSwap = "shift_swap";

    /// <summary>Làm thêm giờ (có giờ bắt đầu / kết thúc).</summary>
    public const string Overtime = "overtime";

    /// <summary>Giải trình một lượt chấm công đi muộn / về sớm / chờ duyệt.</summary>
    public const string Explanation = "explanation";

    public static readonly IReadOnlyList<string> All = new[] { Leave, ShiftSwap, Overtime, Explanation };

    public static bool IsValid(string? category) => category is not null && All.Contains(category);
}

/// <summary>Trạng thái đơn (cột leave_requests.status).</summary>
public static class RequestStatus
{
    /// <summary>Đơn đổi ca chờ đồng nghiệp đồng ý.</summary>
    public const string PendingPeer = "pending_peer";

    /// <summary>Chờ Quản lý / HR duyệt.</summary>
    public const string Pending = "pending";

    public const string Approved = "approved";
    public const string Rejected = "rejected";

    /// <summary>Đồng nghiệp từ chối đổi ca — đơn kết thúc, không tới Quản lý.</summary>
    public const string PeerDeclined = "peer_declined";

    /// <summary>Người tạo rút đơn khi chưa được duyệt.</summary>
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All = new[] { PendingPeer, Pending, Approved, Rejected, PeerDeclined, Cancelled };

    /// <summary>Đơn còn đang xử lý (người tạo còn rút được).</summary>
    public static readonly IReadOnlyList<string> Open = new[] { PendingPeer, Pending };

    /// <summary>Đơn đang có hiệu lực hoặc sắp có hiệu lực — chặn tạo đơn trùng (cùng ngày nghỉ, cùng giờ OT...).</summary>
    public static readonly IReadOnlyList<string> Blocking = new[] { PendingPeer, Pending, Approved };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    public static bool IsOpen(string status) => Open.Contains(status);
}
