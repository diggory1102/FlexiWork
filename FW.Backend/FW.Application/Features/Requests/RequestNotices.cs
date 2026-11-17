using System.Globalization;
using FW.Domain.Enums;

namespace FW.Application.Features.Requests;

/// <summary>Câu chữ thông báo cho các sự kiện của đơn từ (tạo đơn, đồng nghiệp trả lời, duyệt đơn).</summary>
public static class RequestNotices
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Tóm tắt nội dung đơn trong một dòng: "nghỉ 20/11–21/11 (2 ngày)", "OT 20/11 17:00–19:30"…</summary>
    public static string Describe(LeaveRequestDto r) => r.Category switch
    {
        RequestCategory.Leave => r.StartDate == r.EndDate
            ? $"nghỉ {r.StartDate:dd/MM} ({Days(r.DurationDays)} ngày)"
            : $"nghỉ {r.StartDate:dd/MM}–{r.EndDate:dd/MM} ({Days(r.DurationDays)} ngày)",
        RequestCategory.Overtime => $"OT {r.StartAt:dd/MM} {r.StartAt:HH:mm}–{r.EndAt:HH:mm}",
        RequestCategory.ShiftSwap => r.TargetSchedule is null
            ? $"nhờ {r.TargetUser?.FullName} làm thay ca {r.SourceSchedule?.WorkDate:dd/MM}"
            : $"đổi ca {r.SourceSchedule?.WorkDate:dd/MM} lấy ca {r.TargetSchedule.WorkDate:dd/MM}",
        RequestCategory.Explanation => $"giải trình ca ngày {r.StartDate:dd/MM}",
        _ => r.LeaveTypeName
    };

    private static string Days(decimal value) => value.ToString("0.#", Vi);
}
