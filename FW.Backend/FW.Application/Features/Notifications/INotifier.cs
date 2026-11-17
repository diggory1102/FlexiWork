namespace FW.Application.Features.Notifications;

/// <summary>
/// Gửi thông báo từ các nghiệp vụ khác (duyệt đơn, công bố lịch, chấm công…): lưu vào CSDL rồi đẩy tức thì cho ai đang online.
/// Không bao giờ ném lỗi — thông báo hỏng không được làm hỏng nghiệp vụ chính (đã commit xong mới gọi).
/// </summary>
public interface INotifier
{
    /// <summary>Gửi cho các người nhận (trùng mã chỉ nhận một lần).</summary>
    Task NotifyAsync(IEnumerable<int> userIds, string type, string title, string content, string? referenceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi cho người duyệt của chi nhánh: các Quản lý chi nhánh đó (trừ người tạo); nếu người tạo là Quản lý hoặc chi nhánh chưa có
    /// Quản lý thì gửi cho HR. Quản trị viên không nhận mặc định để tránh ồn.
    /// </summary>
    Task NotifyReviewersAsync(int? branchId, int requesterId, string type, string title, string content, string? referenceId = null,
        CancellationToken cancellationToken = default);
}
