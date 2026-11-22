using FW.Domain.Entities;

namespace DemoDataSeeder;

/// <summary>Tính cách chấm công của một nhân viên demo — quyết định xác suất đi muộn, về sớm, vắng.</summary>
internal enum Trait
{
    /// <summary>Gần như luôn đúng giờ.</summary>
    Reliable,

    /// <summary>Thỉnh thoảng muộn vài phút.</summary>
    Average,

    /// <summary>Hay đi muộn từ đầu (không đổi theo thời gian — để so sánh với người "xấu dần").</summary>
    OftenLate,

    /// <summary>Đúng giờ trong nhiều tuần rồi xấu dần ở các tuần gần đây: muộn nhiều hơn, vắng, xin nghỉ và xin đổi ca tăng — minh họa cảnh báo nghỉ việc (Task 7.4).</summary>
    Declining
}

/// <summary>Xác suất / mức độ vi phạm trong một tuần.</summary>
internal readonly record struct Rates(double LateProbability, double LateMeanMinutes, double EarlyLeaveProbability, double AbsentProbability);

internal sealed class Person
{
    public required User User { get; init; }
    public int Id { get; set; }
    public required Branch Branch { get; init; }
    public required Trait Trait { get; init; }
    public bool FullTime => User.EmploymentType == "full_time";

    /// <summary>Ca ưa thích của nhân viên bán thời gian (một ca mỗi ngày).</summary>
    public int PreferredShiftIndex { get; init; }

    /// <summary>Có lịch làm việc trong khoảng demo không (người đã nghỉ việc / bị khóa thì không).</summary>
    public bool Works { get; init; } = true;

    /// <summary>Mức xấu đi (0 = bình thường, 1 = tệ nhất) của tuần cách hiện tại <paramref name="weeksFromEnd"/> tuần (0 = tuần này).</summary>
    public static double Severity(int weeksFromEnd) => weeksFromEnd switch { 0 => 1.0, 1 => 1.0, 2 => 0.3, _ => 0.0 };

    public Rates RatesFor(int weeksFromEnd)
    {
        var s = Trait == Trait.Declining ? Severity(weeksFromEnd) : 0.0;
        return Trait switch
        {
            Trait.Reliable => new Rates(0.04, 6, 0.02, 0.004),
            Trait.Average => new Rates(0.12, 9, 0.06, 0.010),
            Trait.OftenLate => new Rates(0.32, 12, 0.10, 0.020),
            // Nền ngang "Average", tuần gần đây: muộn nhiều gấp ~5 lần và muộn lâu gấp ~4 lần, bắt đầu vắng
            _ => new Rates(0.07 + 0.50 * s, 5 + 22 * s, 0.04 + 0.22 * s, 0.006 + 0.10 * s)
        };
    }
}

internal static class Names
{
    private static readonly string[] Surnames =
        { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };

    private static readonly string[] MaleMiddle = { "Văn", "Minh", "Quốc", "Đức", "Hữu", "Anh", "Công", "Gia" };
    private static readonly string[] FemaleMiddle = { "Thị", "Ngọc", "Thu", "Phương", "Khánh", "Bảo", "Diệu", "Hồng" };

    private static readonly string[] MaleGiven =
        { "Anh", "Bình", "Cường", "Dũng", "Hải", "Hiếu", "Hùng", "Khoa", "Long", "Nam", "Phúc", "Quân", "Sơn", "Thắng", "Tuấn", "Việt", "Đạt", "Huy", "Kiên", "Trung" };

    private static readonly string[] FemaleGiven =
        { "Chi", "Dung", "Giang", "Hà", "Hạnh", "Hoa", "Hương", "Lan", "Linh", "Mai", "My", "Ngân", "Nhung", "Quỳnh", "Thảo", "Trang", "Trinh", "Yến", "Vy", "Nga" };

    /// <summary>Tên đầy đủ không trùng nhau (thử tới khi chưa có trong <paramref name="used"/>).</summary>
    public static string Next(Random random, HashSet<string> used)
    {
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var female = random.Next(2) == 0;
            var name = $"{Pick(random, Surnames)} {Pick(random, female ? FemaleMiddle : MaleMiddle)} {Pick(random, female ? FemaleGiven : MaleGiven)}";
            if (used.Add(name)) return name;
        }
        throw new InvalidOperationException("Không sinh được thêm tên không trùng.");
    }

    private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];
}

internal static class RandomExtensions
{
    /// <summary>Số phút ngẫu nhiên theo phân phối mũ với trung bình <paramref name="mean"/> (nhiều lần muộn ngắn, thỉnh thoảng muộn lâu).</summary>
    public static int Exponential(this Random random, double mean) => (int)Math.Round(-mean * Math.Log(1 - random.NextDouble()));

    public static T PickOne<T>(this Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

    public static bool Chance(this Random random, double probability) => random.NextDouble() < probability;
}
