using System.ComponentModel.DataAnnotations;
using FW.Domain.Entities;
using FW.Domain.Services;

namespace FW.Application.Features.Scheduling;

/// <summary>Giá trị của <see cref="StaffingRuleRequest.Scope"/>.</summary>
public static class StaffingScopes
{
    /// <summary>Đúng một ngày cụ thể (VD ngày có tiệc đặt bàn).</summary>
    public const string Date = "date";

    /// <summary>Mọi thứ trong tuần hằng tuần (VD mọi thứ Bảy).</summary>
    public const string Weekday = "weekday";
}

/// <param name="Scope">"date" hoặc "weekday".</param>
/// <param name="Date">Ngày áp dụng khi <paramref name="Scope"/> = "date".</param>
/// <param name="DayOfWeek">Thứ kiểu ISO (1 = Thứ Hai … 7 = Chủ nhật) khi <paramref name="Scope"/> = "weekday".</param>
public record StaffingRuleDto(
    int Id,
    int BranchId,
    int ShiftId,
    string ShiftCode,
    string ShiftName,
    string Scope,
    DateOnly? Date,
    int? DayOfWeek,
    int? MinStaff,
    int? MaxStaff,
    string? Note,
    DateTime CreatedAt)
{
    public static StaffingRuleDto From(ShiftStaffingRule r, Shift shift)
    {
        var isDate = r.ScopeKey.StartsWith(ShiftStaffing.DatePrefix, StringComparison.Ordinal);
        DateOnly? date = isDate && DateOnly.TryParseExact(r.ScopeKey[ShiftStaffing.DatePrefix.Length..], "yyyy-MM-dd", out var d) ? d : null;
        int? dow = !isDate && int.TryParse(r.ScopeKey[ShiftStaffing.WeekdayPrefix.Length..], out var n) ? n : null;
        return new StaffingRuleDto(r.Id, r.BranchId, r.ShiftId, shift.ShiftCode, shift.Name, isDate ? StaffingScopes.Date : StaffingScopes.Weekday,
            date, dow, r.MinStaff, r.MaxStaff, r.Note, r.CreatedAt);
    }
}

/// <summary>Đặt (hoặc thay) ngoại lệ định biên cho một ca của chi nhánh; để trống cả tối thiểu và tối đa = ngày / thứ đó không giới hạn.</summary>
public class StaffingRuleRequest
{
    /// <summary>Bắt buộc với Admin/HR; Quản lý bỏ trống = chi nhánh của mình.</summary>
    public int? BranchId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn ca.")]
    public int ShiftId { get; set; }

    /// <summary>"date" (một ngày cụ thể) hoặc "weekday" (mọi thứ trong tuần).</summary>
    [Required(ErrorMessage = "Vui lòng chọn phạm vi áp dụng.")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>Ngày áp dụng — bắt buộc khi <see cref="Scope"/> = "date".</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Thứ kiểu ISO 1 (Thứ Hai) … 7 (Chủ nhật) — bắt buộc khi <see cref="Scope"/> = "weekday".</summary>
    [Range(1, 7, ErrorMessage = "Thứ trong tuần từ 1 (Thứ Hai) đến 7 (Chủ nhật).")]
    public int? DayOfWeek { get; set; }

    [Range(1, 500, ErrorMessage = "Số người tối thiểu từ 1 đến 500.")]
    public int? MinStaff { get; set; }

    [Range(1, 500, ErrorMessage = "Số người tối đa từ 1 đến 500.")]
    public int? MaxStaff { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? Note { get; set; }
}

/// <summary>Định biên hiệu lực của một ca trong một ngày (đã áp ngoại lệ) — chỉ trả các ô có giới hạn.</summary>
/// <param name="Source">"default" (mặc định của ca), "weekday" hoặc "date" (ngoại lệ).</param>
public record EffectiveStaffingDto(int ShiftId, DateOnly Date, int? MinStaff, int? MaxStaff, string Source, string? Note)
{
    public static string SourceName(StaffingSource source) => source switch
    {
        StaffingSource.Date => "date",
        StaffingSource.Weekday => "weekday",
        _ => "default"
    };
}
