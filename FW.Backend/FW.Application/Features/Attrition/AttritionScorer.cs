
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;

namespace FW.Application.Features.Attrition;

public sealed class AttritionOptions
{
    public const string SectionName = "Attrition";

    public bool JobEnabled { get; set; } = true;

    /// <summary>Thứ chạy tác vụ hằng tuần (giờ VN) và giờ chạy.</summary>
    public DayOfWeek JobDay { get; set; } = DayOfWeek.Monday;
    public int JobHour { get; set; } = 2;

    public int RecentWeeks { get; set; } = 2;
    public int BaselineWeeks { get; set; } = 8;

    /// <summary>Cần tối thiểu bấy nhiêu ca (đã chấm công hoặc vắng) trong nền / cửa sổ gần để kết luận — nhân viên mới vào chưa đủ dữ liệu.</summary>
    public int MinBaselineShifts { get; set; } = 8;
    public int MinRecentShifts { get; set; } = 2;

    /// <summary>Trọng số từng tín hiệu (tổng không cần bằng 1 — được chuẩn hóa theo các tín hiệu đang có dữ liệu).</summary>
    public double WeightLateFrequency { get; set; } = 0.15;
    public double WeightLateMinutes { get; set; } = 0.10;
    public double WeightEarlyLeave { get; set; } = 0.05;
    public double WeightAbsence { get; set; } = 0.25;
    public double WeightLeaveRequests { get; set; } = 0.20;
    public double WeightShiftSwap { get; set; } = 0.10;
    public double WeightAvailability { get; set; } = 0.15;

    /// <summary>Mức tăng (so với nền) được coi là "tăng tối đa" (điểm con = 1) cho từng tín hiệu.</summary>
    public double SaturationLateFrequency { get; set; } = 0.30;   // +30 điểm % ca đi muộn
    public double SaturationLateMinutes { get; set; } = 10;       // +10 phút muộn TB / ca
    public double SaturationEarlyLeave { get; set; } = 0.25;      // +25 điểm % ca về sớm
    public double SaturationAbsence { get; set; } = 0.15;         // +15 điểm % ca vắng
    public double SaturationLeaveDaysPerWeek { get; set; } = 1.5; // +1,5 ngày nghỉ / tuần
    public double SaturationSwapPerWeek { get; set; } = 1.0;      // +1 đơn đổi ca / tuần
    public double SaturationAvailabilityDrop { get; set; } = 0.5; // giảm 50% số ca đăng ký

    public int MediumThreshold { get; set; } = 25;
    public int HighThreshold { get; set; } = 50;

    /// <summary>Chỉ nêu thành "lý do" các tín hiệu có điểm con từ mức này trở lên.</summary>
    public double ReasonMinSubScore { get; set; } = 0.25;
}

/// <summary>
/// Chấm điểm nguy cơ nghỉ việc 0–100 bằng phương pháp CÓ GIẢI THÍCH (không học máy vì không có dữ liệu nhãn người đã nghỉ):
/// so hành vi <c>RecentWeeks</c> tuần gần nhất với nền <c>BaselineWeeks</c> tuần trước đó CỦA CHÍNH nhân viên,
/// mỗi tín hiệu quy về điểm con 0–1 theo mức tăng, rồi cộng có trọng số. Chỉ so mức TĂNG, nên người vốn hay đi muộn
/// không bị gắn cờ oan; người đang xấu đi thì bị. Đây là tín hiệu tham khảo cho Quản lý, không dùng để kỷ luật.
/// </summary>
public static class AttritionScorer
{
    public static AttritionAssessment Assess(EmployeeActivity employee, DateOnly asOfExclusive, AttritionOptions o)
    {
        var recentFrom = asOfExclusive.AddDays(-7 * o.RecentWeeks);
        var baselineFrom = recentFrom.AddDays(-7 * o.BaselineWeeks);

        var recentShifts = employee.Shifts.Where(s => s.Date >= recentFrom && s.Date < asOfExclusive).ToList();
        var baseShifts = employee.Shifts.Where(s => s.Date >= baselineFrom && s.Date < recentFrom).ToList();

        var recent = ShiftStats.From(recentShifts);
        var baseline = ShiftStats.From(baseShifts);

        if (baseline.Due < o.MinBaselineShifts || recent.Due < o.MinRecentShifts)
            return new AttritionAssessment(0, RiskLevel.Low, false, Array.Empty<SignalResult>(), Array.Empty<string>());

        var recentReqs = employee.Requests.Where(r => r.SubmittedOn >= recentFrom && r.SubmittedOn < asOfExclusive).ToList();
        var baseReqs = employee.Requests.Where(r => r.SubmittedOn >= baselineFrom && r.SubmittedOn < recentFrom).ToList();

        var rw = (double)o.RecentWeeks;
        var bw = (double)o.BaselineWeeks;
        var signals = new List<SignalResult>();

        // 1. Tần suất đi muộn
        signals.Add(Make("late_frequency", "Tần suất đi muộn", recent.LateRate, baseline.LateRate,
            Sub(recent.LateRate - baseline.LateRate, o.SaturationLateFrequency), o.WeightLateFrequency,
            $"Tỷ lệ ca đi muộn tăng từ {Vi.Percent(baseline.LateRate)} lên {Vi.Percent(recent.LateRate)} (so với {o.BaselineWeeks} tuần trước đó)"));

        // 2. Số phút muộn trung bình
        signals.Add(Make("late_minutes", "Số phút đi muộn", recent.AvgLateMinutes, baseline.AvgLateMinutes,
            Sub(recent.AvgLateMinutes - baseline.AvgLateMinutes, o.SaturationLateMinutes), o.WeightLateMinutes,
            $"Đi muộn trung bình mỗi ca tăng từ {Vi.Num(baseline.AvgLateMinutes)} lên {Vi.Num(recent.AvgLateMinutes)} phút"));

        // 3. Về sớm
        signals.Add(Make("early_leave", "Về sớm", recent.EarlyRate, baseline.EarlyRate,
            Sub(recent.EarlyRate - baseline.EarlyRate, o.SaturationEarlyLeave), o.WeightEarlyLeave,
            $"Tỷ lệ ca về sớm tăng từ {Vi.Percent(baseline.EarlyRate)} lên {Vi.Percent(recent.EarlyRate)}"));

        // 4. Vắng mặt
        signals.Add(Make("absence", "Vắng mặt", recent.AbsentRate, baseline.AbsentRate,
            Sub(recent.AbsentRate - baseline.AbsentRate, o.SaturationAbsence), o.WeightAbsence,
            $"Vắng mặt không chấm công {recent.Absent} ca trong {o.RecentWeeks} tuần gần nhất (tỷ lệ ca vắng từ {Vi.Percent(baseline.AbsentRate)} lên {Vi.Percent(recent.AbsentRate)})"));

        // 5. Xin nghỉ (ngày/tuần)
        var recentLeave = (double)recentReqs.Where(r => r.Category == RequestKind.Leave).Sum(r => r.Days) / rw;
        var baseLeave = (double)baseReqs.Where(r => r.Category == RequestKind.Leave).Sum(r => r.Days) / bw;
        signals.Add(Make("leave_requests", "Xin nghỉ phép", recentLeave, baseLeave,
            Sub(recentLeave - baseLeave, o.SaturationLeaveDaysPerWeek), o.WeightLeaveRequests,
            $"Xin nghỉ nhiều hơn: {Vi.Num(recentLeave)} ngày/tuần so với {Vi.Num(baseLeave)} ngày/tuần trước đó"));

        // 6. Xin đổi ca (đơn/tuần)
        var recentSwap = recentReqs.Count(r => r.Category == RequestKind.ShiftSwap) / rw;
        var baseSwap = baseReqs.Count(r => r.Category == RequestKind.ShiftSwap) / bw;
        signals.Add(Make("shift_swap", "Xin đổi ca", recentSwap, baseSwap,
            Sub(recentSwap - baseSwap, o.SaturationSwapPerWeek), o.WeightShiftSwap,
            $"Xin đổi ca nhiều hơn: {Vi.Num(recentSwap)} đơn/tuần so với {Vi.Num(baseSwap)} đơn/tuần trước đó"));

        // 7. Giảm đăng ký ca (chỉ khi hệ thống có dữ liệu này và nhân viên từng đăng ký)
        if (employee.Availability is not null)
        {
            var recentSlots = AverageSlots(employee.Availability, recentFrom, asOfExclusive, o.RecentWeeks);
            var baseSlots = AverageSlots(employee.Availability, baselineFrom, recentFrom, o.BaselineWeeks);
            if (baseSlots > 0)
            {
                var drop = Math.Max(0, (baseSlots - recentSlots) / baseSlots);
                signals.Add(Make("availability", "Đăng ký ca", recentSlots, baseSlots,
                    Sub(drop, o.SaturationAvailabilityDrop), o.WeightAvailability,
                    $"Đăng ký ca nguyện vọng giảm {Vi.Percent(drop)} (từ {Vi.Num(baseSlots)} xuống {Vi.Num(recentSlots)} khung/tuần)"));
            }
        }

        var totalWeight = signals.Sum(s => s.Weight);
        var score = totalWeight <= 0 ? 0 : (int)Math.Round(100.0 * signals.Sum(s => s.Contribution) / totalWeight, MidpointRounding.AwayFromZero);
        score = Math.Clamp(score, 0, 100);

        var level = score >= o.HighThreshold ? RiskLevel.High
            : score >= o.MediumThreshold ? RiskLevel.Medium
            : RiskLevel.Low;

        var reasons = signals
            .Where(s => s.SubScore >= o.ReasonMinSubScore && s.Reason is not null)
            .OrderByDescending(s => s.Contribution)
            .Select(s => s.Reason!)
            .ToList();

        return new AttritionAssessment(score, level, true, signals, reasons);
    }

    private static double Sub(double delta, double saturation) =>
        saturation <= 0 ? 0 : Math.Clamp(delta / saturation, 0, 1);

    private static SignalResult Make(string key, string label, double recent, double baseline, double sub, double weight, string reason) =>
        new(key, label, Math.Round(recent, 3), Math.Round(baseline, 3), Math.Round(sub, 3), weight, Math.Round(weight * sub, 4), reason);

    private static double AverageSlots(IReadOnlyList<AvailabilityRecord> records, DateOnly from, DateOnly toExclusive, int weeks) =>
        weeks <= 0 ? 0 : records.Where(r => r.WeekStart >= from && r.WeekStart < toExclusive).Sum(r => r.Slots) / (double)weeks;

    private readonly record struct ShiftStats(int Due, int Attended, int Absent, int LateShifts, int LateMinutes, int EarlyShifts)
    {
        public double LateRate => Attended == 0 ? 0 : (double)LateShifts / Attended;
        public double EarlyRate => Attended == 0 ? 0 : (double)EarlyShifts / Attended;
        public double AvgLateMinutes => Attended == 0 ? 0 : (double)LateMinutes / Attended;
        public double AbsentRate => Due == 0 ? 0 : (double)Absent / Due;

        public static ShiftStats From(IReadOnlyList<ShiftRecord> shifts)
        {
            var attended = shifts.Where(s => s.Attended).ToList();
            var absent = shifts.Count(s => s.Absent && !s.Attended);
            return new ShiftStats(
                Due: attended.Count + absent,
                Attended: attended.Count,
                Absent: absent,
                LateShifts: attended.Count(s => s.LateMinutes > 0),
                LateMinutes: attended.Sum(s => s.LateMinutes),
                EarlyShifts: attended.Count(s => s.EarlyMinutes > 0));
        }
    }
}
