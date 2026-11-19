using FW.Domain.Entities;

namespace FW.Domain.Services;

/// <summary>Một ca dự định xếp bị từ chối vì ca đó đã đủ người trong ngày.</summary>
/// <param name="Item">Ca dự định xếp.</param>
/// <param name="Current">Số người đang có trong ô (ca, ngày) khi xét ca này.</param>
/// <param name="Max">Số người tối đa của ca.</param>
public record StaffingRejection(PlannedShift Item, int Current, int Max);

/// <summary>Kết quả kiểm tra định biên: các ca xếp được và các ca bị từ chối vì ca đã đủ người.</summary>
public record StaffingPlan(IReadOnlyList<PlannedShift> Allowed, IReadOnlyList<StaffingRejection> Rejected);

/// <summary>
/// Kiểm tra số người tối đa của ca khi xếp lịch (mặc định <see cref="Shift.MaxStaff"/>; truyền <c>maxOf</c> để dùng định biên hiệu lực
/// sau khi áp ngoại lệ theo ngày / thứ — xem <see cref="ShiftStaffing.Resolve"/>). Xét lần lượt từng ca dự định theo thứ tự gửi lên:
/// một ô (ca, ngày) nhận thêm người cho tới khi chạm tối đa, tính cả những người đã xếp trước đó trong cùng yêu cầu.
/// Ca không đặt tối đa thì không bị giới hạn. Hàm thuần — việc đếm người đã có và khóa dòng làm ở tầng truy cập dữ liệu.
/// </summary>
public static class StaffingPlanner
{
    /// <param name="staffed">Mỗi phần tử là một người đang giữ chỗ trong ô (ShiftId, ngày); ca đã hủy / nghỉ phép không đưa vào.</param>
    /// <param name="maxOf">Số người tối đa áp dụng cho một ca dự định (null = không giới hạn); bỏ trống thì lấy <see cref="Shift.MaxStaff"/>.</param>
    public static StaffingPlan Plan(IEnumerable<PlannedShift> items, IEnumerable<(int ShiftId, DateTime Date)> staffed,
        Func<PlannedShift, int?>? maxOf = null)
    {
        maxOf ??= item => item.Shift.MaxStaff;
        var counts = new Dictionary<(int ShiftId, DateTime Date), int>();
        foreach (var slot in staffed)
        {
            var key = (slot.ShiftId, slot.Date.Date);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var allowed = new List<PlannedShift>();
        var rejected = new List<StaffingRejection>();
        foreach (var item in items)
        {
            var key = (item.Shift.Id, item.Date);
            var current = counts.GetValueOrDefault(key);
            if (maxOf(item) is { } max && current >= max)
            {
                rejected.Add(new StaffingRejection(item, current, max));
                continue;
            }

            counts[key] = current + 1;
            allowed.Add(item);
        }

        return new StaffingPlan(allowed, rejected);
    }
}
