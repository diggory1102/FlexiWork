using FW.Domain.Entities;

namespace FW.Domain.Services;

/// <summary>Một ca được xếp (hoặc dự định xếp) cho một nhân viên vào một ngày.</summary>
public record PlannedShift(int UserId, Shift Shift, DateTime WorkDate)
{
    public DateTime Date => WorkDate.Date;
}

public enum ScheduleConflictKind
{
    /// <summary>Nhân viên đã có đúng ca này trong ngày này.</summary>
    Duplicate,

    /// <summary>Giờ làm chồng lên một ca khác của cùng nhân viên (kể cả ca đêm hôm trước kéo sang).</summary>
    Overlap
}

/// <param name="Item">Ca dự định xếp bị từ chối.</param>
/// <param name="With">Ca đã có (trong lịch hoặc xếp trước đó trong cùng yêu cầu) gây xung đột.</param>
/// <param name="WithIsExisting">true nếu <paramref name="With"/> là lịch đã lưu, false nếu nằm trong cùng yêu cầu.</param>
public record ScheduleConflict(PlannedShift Item, PlannedShift With, ScheduleConflictKind Kind, bool WithIsExisting);

/// <summary>Kết quả lập lịch: các ca xếp được và các ca bị từ chối do xung đột.</summary>
public record SchedulePlan(IReadOnlyList<PlannedShift> Accepted, IReadOnlyList<ScheduleConflict> Conflicts);

/// <summary>
/// Kiểm tra xung đột khi xếp lịch (Task 2.3). Xét lần lượt từng ca dự định theo thứ tự gửi lên:
/// ca bị từ chối nếu trùng hoặc chồng giờ với lịch đã có của nhân viên, hoặc với một ca đã được chấp nhận trước đó
/// trong cùng yêu cầu. Ca nối tiếp nhau (ca gãy) không bị coi là chồng giờ — xem <see cref="Shift.OverlapsWith"/>.
/// </summary>
public static class SchedulePlanner
{
    /// <param name="existing">Lịch hiệu lực (chưa hủy) của các nhân viên liên quan, nên lấy rộng hơn khoảng ngày xếp 1 ngày mỗi phía.</param>
    public static SchedulePlan Plan(IEnumerable<PlannedShift> items, IEnumerable<PlannedShift> existing)
    {
        var existingByUser = existing.ToLookup(s => s.UserId);
        var acceptedByUser = new Dictionary<int, List<PlannedShift>>();
        var accepted = new List<PlannedShift>();
        var conflicts = new List<ScheduleConflict>();

        foreach (var item in items)
        {
            var conflict = FindConflict(item, existingByUser[item.UserId], withIsExisting: true)
                           ?? FindConflict(item, acceptedByUser.GetValueOrDefault(item.UserId) ?? new List<PlannedShift>(), withIsExisting: false);

            if (conflict != null)
            {
                conflicts.Add(conflict);
                continue;
            }

            accepted.Add(item);
            if (!acceptedByUser.TryGetValue(item.UserId, out var list)) acceptedByUser[item.UserId] = list = new List<PlannedShift>();
            list.Add(item);
        }

        return new SchedulePlan(accepted, conflicts);
    }

    private static ScheduleConflict? FindConflict(PlannedShift item, IEnumerable<PlannedShift> others, bool withIsExisting)
    {
        foreach (var other in others)
        {
            // Ca chỉ có thể chồng với ca trong ngày liền trước / liền sau (ca dài tối đa 16 giờ)
            if (Math.Abs((other.Date - item.Date).TotalDays) > 1) continue;

            if (other.Shift.Id == item.Shift.Id && other.Date == item.Date)
                return new ScheduleConflict(item, other, ScheduleConflictKind.Duplicate, withIsExisting);

            if (item.Shift.OverlapsWith(item.Date, other.Shift, other.Date))
                return new ScheduleConflict(item, other, ScheduleConflictKind.Overlap, withIsExisting);
        }
        return null;
    }
}
