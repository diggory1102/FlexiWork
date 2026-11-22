namespace FW.Domain.Enums;

/// <summary>Cách giao một việc mẫu cho những người làm ca (cột shift_tasks.assign_mode).</summary>
public static class TaskAssignMode
{
    /// <summary>Mỗi người một bản việc riêng, ai cũng phải tự hoàn thành (kiểm đếm két của mình, nhận đồng phục...).</summary>
    public const string Each = "each";

    /// <summary>Làm chung: mọi người đều thấy việc, một người hoàn thành là cả ca xong (mở cửa, lau sàn, sắp xếp kệ...).</summary>
    public const string Shared = "shared";

    public static readonly IReadOnlyList<string> All = new[] { Each, Shared };

    public static bool IsValid(string? mode) => mode is not null && All.Contains(mode);
}
