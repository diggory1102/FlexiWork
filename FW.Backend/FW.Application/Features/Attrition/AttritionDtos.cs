namespace FW.Application.Features.Attrition;

public sealed record AttritionSignalDto(
    string Key, string Label, double Recent, double Baseline, double SubScore, double Weight, double Contribution, string? Reason)
{
    public static AttritionSignalDto From(SignalResult s) =>
        new(s.Key, s.Label, s.Recent, s.Baseline, s.SubScore, s.Weight, s.Contribution, s.Reason);
}

/// <summary>Điểm nguy cơ của một nhân viên trong một tuần. <see cref="Level"/>: low | medium | high.</summary>
public sealed record AttritionAlertDto(
    int UserId,
    string EmployeeName,
    int? BranchId,
    DateOnly WeekStart,
    int Score,
    string Level,
    bool DataSufficient,
    int? PreviousScore,
    int? Delta,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<AttritionSignalDto> Signals,
    DateTime ComputedAt)
{
    public static AttritionAlertDto From(AttritionSnapshot s) => new(
        s.UserId, s.EmployeeName, s.BranchId, s.WeekStart, s.Score, s.Level.ToString().ToLowerInvariant(), s.DataSufficient,
        s.PreviousScore, s.Delta, s.Reasons, s.Signals.Select(AttritionSignalDto.From).ToList(), s.ComputedAt);
}

/// <summary>Tổng hợp cho ô cảnh báo trên Dashboard: số nhân viên đã chấm (đủ dữ liệu), số ở mức trung bình / cao của lần chấm gần nhất.</summary>
public sealed record AttritionSummaryDto(DateOnly? WeekStart, DateTime? ComputedAt, int Scored, int Medium, int High);

public sealed record AttritionNarrativeDto(int UserId, string Text);
