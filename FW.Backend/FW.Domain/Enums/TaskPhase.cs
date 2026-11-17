namespace FW.Domain.Enums;

/// <summary>Giai đoạn trong ca của một đầu việc (cột shift_tasks.phase) — checklist hiển thị theo thứ tự này.</summary>
public static class TaskPhase
{
    /// <summary>Đầu ca (mở cửa, kiểm két...).</summary>
    public const string Start = "start";

    /// <summary>Trong ca.</summary>
    public const string During = "during";

    /// <summary>Cuối ca (bàn giao, đóng cửa...).</summary>
    public const string End = "end";

    public static readonly IReadOnlyList<string> All = new[] { Start, During, End };

    public static bool IsValid(string? phase) => phase is not null && All.Contains(phase);

    /// <summary>Thứ tự sắp xếp: đầu ca → trong ca → cuối ca.</summary>
    public static int Order(string phase) => phase switch { Start => 0, During => 1, End => 2, _ => 3 };
}
