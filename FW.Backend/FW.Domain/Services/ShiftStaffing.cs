using FW.Domain.Entities;

namespace FW.Domain.Services;

/// <summary>Quy tắc nào đang quyết định định biên của một ô (ca, ngày).</summary>
public enum StaffingSource
{
    /// <summary>Số người mặc định khai báo ở khung ca.</summary>
    Default,

    /// <summary>Ngoại lệ theo thứ trong tuần.</summary>
    Weekday,

    /// <summary>Ngoại lệ cho đúng ngày đó.</summary>
    Date
}

/// <summary>Định biên hiệu lực của một ô (ca, ngày); null = không yêu cầu / không giới hạn.</summary>
public record StaffingLimits(int? Min, int? Max, StaffingSource Source, string? Note)
{
    public bool IsLimited => Min.HasValue || Max.HasValue;
}

/// <summary>
/// Định biên ca theo thứ tự ưu tiên từ cụ thể nhất: ngoại lệ đúng ngày → ngoại lệ theo thứ trong tuần → mặc định của ca.
/// Quy tắc tìm thấy THAY THẾ hoàn toàn cả tối thiểu lẫn tối đa của mặc định (không trộn từng nửa); quy tắc để trống cả hai nghĩa là
/// "ngày đó không giới hạn". Hàm thuần, không đụng CSDL.
/// </summary>
public static class ShiftStaffing
{
    public const string DatePrefix = "date:";
    public const string WeekdayPrefix = "dow:";

    /// <summary>Khóa phạm vi cho một ngày cụ thể, VD "date:2026-12-24".</summary>
    public static string DateKey(DateTime date) => $"{DatePrefix}{date:yyyy-MM-dd}";

    /// <summary>Khóa phạm vi cho một thứ trong tuần theo kiểu ISO (1 = Thứ Hai … 7 = Chủ nhật), VD "dow:6".</summary>
    public static string WeekdayKey(int isoDayOfWeek) => $"{WeekdayPrefix}{isoDayOfWeek}";

    /// <summary>Thứ trong tuần kiểu ISO: Thứ Hai = 1 … Chủ nhật = 7.</summary>
    public static int IsoDayOfWeek(DateTime date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    /// <param name="rules">Các quy tắc của CHÍNH ca và chi nhánh đang xét (có thể kèm cả ngày khác, hàm tự chọn đúng quy tắc).</param>
    public static StaffingLimits Resolve(Shift shift, IEnumerable<ShiftStaffingRule> rules, DateTime date)
    {
        var list = rules as IReadOnlyCollection<ShiftStaffingRule> ?? rules.ToList();
        var byDate = list.FirstOrDefault(r => r.ScopeKey == DateKey(date));
        if (byDate != null) return new StaffingLimits(byDate.MinStaff, byDate.MaxStaff, StaffingSource.Date, byDate.Note);

        var byWeekday = list.FirstOrDefault(r => r.ScopeKey == WeekdayKey(IsoDayOfWeek(date)));
        if (byWeekday != null) return new StaffingLimits(byWeekday.MinStaff, byWeekday.MaxStaff, StaffingSource.Weekday, byWeekday.Note);

        return new StaffingLimits(shift.MinStaff, shift.MaxStaff, StaffingSource.Default, null);
    }
}
