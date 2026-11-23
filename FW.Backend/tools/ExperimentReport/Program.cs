// Thống kê thí nghiệm chấm công trên thiết bị thật (Task 6.5). CHỈ ĐỌC bảng attendance_attempts (+ users để lấy mã nhân viên), không ghi gì.
//
//   dotnet run -c Release --project FW.Backend/tools/ExperimentReport -- --from 2026-12-01 --to 2026-12-03 [--sheet phieu-ghi.csv] [--out thu-muc]
//
// Chuỗi kết nối đọc từ biến môi trường FW_REPORT_DB (gõ trong PowerShell, KHÔNG ghi vào file, KHÔNG dán vào chat) hoặc --connection.
// Tùy chọn:  --from / --to yyyy-MM-dd  khoảng ngày (tính cả hai đầu)       --sheet  phiếu ghi của người thử (CSV, xem docs/EXPERIMENT_6_5.md §5)
//            --window N  số phút lệch tối đa khi ghép dòng phiếu với lượt trên hệ thống (mặc định 3)
//            --out thư mục  nơi ghi bao-cao.md và luot-cham-cong.csv (mặc định ./ket-qua-thi-nghiem)
using System.Globalization;
using System.Text;
using Dapper;
using MySqlConnector;

string? Opt(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var connectionString = Opt("--connection") ?? Environment.GetEnvironmentVariable("FW_REPORT_DB");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Thiếu chuỗi kết nối: đặt biến môi trường FW_REPORT_DB (gõ trong PowerShell) hoặc dùng --connection.");
    return 1;
}
if (!DateTime.TryParseExact(Opt("--from"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
    !DateTime.TryParseExact(Opt("--to"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
{
    Console.Error.WriteLine("Cần --from yyyy-MM-dd và --to yyyy-MM-dd.");
    return 1;
}
var windowMinutes = int.Parse(Opt("--window") ?? "3", CultureInfo.InvariantCulture);
var outDir = Opt("--out") ?? "ket-qua-thi-nghiem";
var sheetPath = Opt("--sheet");

List<Attempt> attempts;
await using (var connection = new MySqlConnection(connectionString))
{
    attempts = (await connection.QueryAsync<Attempt>("""
        SELECT a.id AS Id, a.user_id AS UserId, COALESCE(u.employee_code, CONCAT('#', a.user_id)) AS EmployeeCode, a.action AS Action,
               a.attempted_at AS AttemptedAt, a.outcome AS Outcome, a.stage AS Stage, a.message AS Message, a.distance AS Distance,
               a.accuracy AS Accuracy, a.is_mocked AS IsMocked, a.wifi_status AS WifiStatus, a.face_score AS FaceScore,
               a.face_threshold AS FaceThreshold, a.duration_ms AS DurationMs, a.device_id AS DeviceId
        FROM attendance_attempts a LEFT JOIN users u ON u.id = a.user_id
        WHERE a.attempted_at >= @From AND a.attempted_at < @To
        ORDER BY a.attempted_at, a.id
        """, new { From = from.Date, To = to.Date.AddDays(1) })).ToList();
}

List<SheetRow> sheet = sheetPath == null ? new() : SheetReader.Read(sheetPath);
var matches = Matcher.Match(sheet, attempts, TimeSpan.FromMinutes(windowMinutes));

Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "luot-cham-cong.csv"), Report.AttemptsCsv(attempts), new UTF8Encoding(true));
var markdown = Report.Build(from, to, attempts, sheetPath != null ? matches : null, windowMinutes);
File.WriteAllText(Path.Combine(outDir, "bao-cao.md"), markdown, new UTF8Encoding(false));
Console.WriteLine($"Đã đọc {attempts.Count} lượt chấm công{(sheetPath != null ? $", {sheet.Count} dòng phiếu ghi" : "")}.");
Console.WriteLine($"Báo cáo: {Path.GetFullPath(Path.Combine(outDir, "bao-cao.md"))}");
Console.WriteLine($"Dữ liệu thô: {Path.GetFullPath(Path.Combine(outDir, "luot-cham-cong.csv"))}");
return 0;

// =====================================================================================================================

internal sealed class Attempt
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime AttemptedAt { get; set; }
    public string Outcome { get; set; } = "";
    public string Stage { get; set; } = "";
    public string? Message { get; set; }
    public double? Distance { get; set; }
    public double? Accuracy { get; set; }
    public bool IsMocked { get; set; }
    public string? WifiStatus { get; set; }
    public double? FaceScore { get; set; }
    public double? FaceThreshold { get; set; }
    public int DurationMs { get; set; }
    public string? DeviceId { get; set; }
}

/// <summary>Một dòng phiếu ghi của người thử: họ làm kịch bản nào, ở thời điểm nào, KỲ VỌNG hệ thống xử lý ra sao.</summary>
internal sealed record SheetRow(int Line, string Scenario, string EmployeeCode, DateTime Time, string Expected, string ExpectedStage,
    string Device, double? ClientMs, string Note);

internal sealed record Match(SheetRow Row, Attempt? Attempt)
{
    public bool OutcomeOk => Attempt != null && Attempt.Outcome == Row.Expected;
    public bool StageOk => Attempt != null && (Row.ExpectedStage.Length == 0 || Attempt.Stage == Row.ExpectedStage);
    public bool Ok => OutcomeOk && StageOk;

    /// <summary>Phân loại sai lệch theo góc nhìn bảo mật / trải nghiệm.</summary>
    public string Verdict
    {
        get
        {
            if (Attempt == null) return "Không thấy lượt trên hệ thống";
            if (Ok) return "Đúng kỳ vọng";
            var expectedPass = Row.Expected is "accepted" or "pending_approval";
            var actualPass = Attempt.Outcome is "accepted" or "pending_approval";
            if (expectedPass && !actualPass) return "Chặn nhầm (người hợp lệ bị từ chối)";
            if (!expectedPass && actualPass) return "Lọt (gian lận / sai quy định vẫn được ghi nhận)";
            return "Bị chặn/ghi nhận khác lớp hoặc khác kết quả kỳ vọng";
        }
    }
}

internal static class SheetReader
{
    private static readonly string[] Formats = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy HH:mm" };

    /// <summary>
    /// Cột bắt buộc: scenario, employee_code, time, expected (accepted | pending_approval | rejected).
    /// Cột tùy chọn: expected_stage (validate | time | gps | face | wifi | save), device, client_ms (thời gian đo bằng đồng hồ bấm trên máy, ms), note.
    /// </summary>
    public static List<SheetRow> Read(string path)
    {
        var lines = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
        if (lines.Count < 2) throw new InvalidDataException("Phiếu ghi rỗng.");
        var header = lines[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(string name, bool required = true)
        {
            var i = header.IndexOf(name);
            if (i < 0 && required) throw new InvalidDataException($"Phiếu ghi thiếu cột '{name}'.");
            return i;
        }
        int cScenario = Col("scenario"), cCode = Col("employee_code"), cTime = Col("time"), cExpected = Col("expected");
        int cStage = Col("expected_stage", false), cDevice = Col("device", false), cClient = Col("client_ms", false), cNote = Col("note", false);

        string Cell(List<string> row, int i) => i >= 0 && i < row.Count ? row[i].Trim() : "";
        var rows = new List<SheetRow>();
        for (var n = 1; n < lines.Count; n++)
        {
            var r = lines[n];
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            if (!DateTime.TryParseExact(Cell(r, cTime), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                throw new InvalidDataException($"Dòng {n + 1}: giờ '{Cell(r, cTime)}' sai định dạng (dùng yyyy-MM-dd HH:mm).");
            var expected = Cell(r, cExpected).ToLowerInvariant();
            if (expected is not ("accepted" or "pending_approval" or "rejected"))
                throw new InvalidDataException($"Dòng {n + 1}: expected phải là accepted, pending_approval hoặc rejected (đang là '{expected}').");
            double? client = double.TryParse(Cell(r, cClient), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) ? ms : null;
            rows.Add(new SheetRow(n + 1, Cell(r, cScenario), Cell(r, cCode).ToUpperInvariant(), time, expected, Cell(r, cStage).ToLowerInvariant(),
                Cell(r, cDevice), client, Cell(r, cNote)));
        }
        return rows;
    }

    /// <summary>CSV tối giản: dấu phẩy ngăn cột, "..." bao ô chứa dấu phẩy, "" là dấu nháy.</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row); row = new List<string>();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        if (rows.Count > 0 && rows[0].Count > 0) rows[0][0] = rows[0][0].TrimStart('﻿');
        return rows;
    }
}

internal static class Matcher
{
    /// <summary>Ghép mỗi dòng phiếu với lượt của đúng nhân viên gần giờ ghi nhất (trong cửa sổ), mỗi lượt chỉ ghép một lần.</summary>
    public static List<Match> Match(List<SheetRow> sheet, List<Attempt> attempts, TimeSpan window)
    {
        var used = new HashSet<long>();
        var result = new List<Match>();
        foreach (var row in sheet.OrderBy(r => r.Time))
        {
            var best = attempts
                .Where(a => !used.Contains(a.Id) && string.Equals(a.EmployeeCode, row.EmployeeCode, StringComparison.OrdinalIgnoreCase))
                .Select(a => (Attempt: a, Gap: (a.AttemptedAt - row.Time).Duration()))
                .Where(x => x.Gap <= window)
                .OrderBy(x => x.Gap)
                .Select(x => x.Attempt)
                .FirstOrDefault();
            if (best != null) used.Add(best.Id);
            result.Add(new Match(row, best));
        }
        return result;
    }
}

internal static class Report
{
    private static readonly CultureInfo Vn = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, string> StageNames = new()
    {
        ["validate"] = "Kiểm tra dữ liệu gửi lên (Fake GPS, thiếu GPS)",
        ["time"] = "Thời gian / ca làm",
        ["gps"] = "Lớp 1 — GPS",
        ["face"] = "Lớp 3 — Khuôn mặt",
        ["wifi"] = "Lớp 2 — Wi-Fi",
        ["save"] = "Đã qua mọi lớp (lưu bản ghi)"
    };

    public static string AttemptsCsv(List<Attempt> attempts)
    {
        var sb = new StringBuilder("id,employee_code,action,attempted_at,outcome,stage,distance_m,accuracy_m,is_mocked,wifi_status,face_score,face_threshold,duration_ms,device_id,message\n");
        foreach (var a in attempts)
        {
            sb.AppendLine(string.Join(',', a.Id.ToString(Vn), Q(a.EmployeeCode), a.Action, a.AttemptedAt.ToString("yyyy-MM-dd HH:mm:ss", Vn), a.Outcome, a.Stage,
                N(a.Distance), N(a.Accuracy), a.IsMocked ? "1" : "0", a.WifiStatus ?? "", N(a.FaceScore, "0.0000"), N(a.FaceThreshold, "0.00"),
                a.DurationMs.ToString(Vn), Q(a.DeviceId ?? ""), Q(a.Message ?? "")));
        }
        return sb.ToString();
    }

    public static string Build(DateTime from, DateTime to, List<Attempt> attempts, List<Match>? matches, int windowMinutes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Kết quả thí nghiệm chấm công ({from:dd/MM/yyyy} – {to:dd/MM/yyyy})").AppendLine();
        sb.AppendLine($"Tổng cộng **{attempts.Count}** lượt bấm chấm công trong khoảng ngày trên (bảng `attendance_attempts`).").AppendLine();

        Outcomes(sb, attempts);
        Timing(sb, attempts);
        Face(sb, attempts, matches);
        if (matches != null) Scenarios(sb, matches, windowMinutes);
        return sb.ToString();
    }

    private static void Outcomes(StringBuilder sb, List<Attempt> attempts)
    {
        sb.AppendLine("## 1. Kết quả theo lớp xử lý").AppendLine();
        sb.AppendLine("| Kết quả | Số lượt | Tỉ lệ |").AppendLine("|---|---:|---:|");
        foreach (var g in attempts.GroupBy(a => a.Outcome).OrderByDescending(g => g.Count()))
            sb.AppendLine($"| {OutcomeName(g.Key)} | {g.Count()} | {Pct(g.Count(), attempts.Count)} |");
        sb.AppendLine();

        var rejected = attempts.Where(a => a.Outcome == "rejected").ToList();
        if (rejected.Count > 0)
        {
            sb.AppendLine("Lượt bị **từ chối** chia theo lớp chặn:").AppendLine();
            sb.AppendLine("| Lớp chặn | Số lượt | Tỉ lệ trong số lượt bị từ chối |").AppendLine("|---|---:|---:|");
            foreach (var g in rejected.GroupBy(a => a.Stage).OrderByDescending(g => g.Count()))
                sb.AppendLine($"| {StageNames.GetValueOrDefault(g.Key, g.Key)} | {g.Count()} | {Pct(g.Count(), rejected.Count)} |");
            sb.AppendLine();
        }
    }

    private static void Timing(StringBuilder sb, List<Attempt> attempts)
    {
        sb.AppendLine("## 2. Thời gian xử lý phía máy chủ").AppendLine();
        sb.AppendLine("Đo từ lúc máy chủ nhận yêu cầu tới lúc có kết quả (đã gồm nhận diện khuôn mặt và tải ảnh lên kho ảnh). **Chưa gồm** thời gian chụp ảnh và mạng di động — xem cột `client_ms` ở mục 4.").AppendLine();
        sb.AppendLine("| Nhóm | Số lượt | Trung bình (ms) | Trung vị (ms) | P95 (ms) | Lớn nhất (ms) |").AppendLine("|---|---:|---:|---:|---:|---:|");
        void Row(string name, IEnumerable<Attempt> items)
        {
            var d = items.Select(a => (double)a.DurationMs).OrderBy(x => x).ToList();
            if (d.Count == 0) { sb.AppendLine($"| {name} | 0 | – | – | – | – |"); return; }
            sb.AppendLine($"| {name} | {d.Count} | {d.Average():0} | {Percentile(d, 0.5):0} | {Percentile(d, 0.95):0} | {d[^1]:0} |");
        }
        Row("Được ghi nhận (đủ 3 lớp, chờ duyệt hoặc không)", attempts.Where(a => a.Outcome is "accepted" or "pending_approval"));
        Row("Bị từ chối", attempts.Where(a => a.Outcome == "rejected"));
        Row("— bị chặn ở lớp GPS", attempts.Where(a => a.Outcome == "rejected" && a.Stage == "gps"));
        Row("— bị chặn ở lớp khuôn mặt", attempts.Where(a => a.Outcome == "rejected" && a.Stage == "face"));
        Row("Tất cả", attempts);
        sb.AppendLine();
    }

    private static void Face(StringBuilder sb, List<Attempt> attempts, List<Match>? matches)
    {
        sb.AppendLine("## 3. Nhận diện khuôn mặt").AppendLine();
        var scored = attempts.Where(a => a.FaceScore != null).ToList();
        if (scored.Count == 0)
        {
            sb.AppendLine("Chưa có lượt nào tới được lớp khuôn mặt.").AppendLine();
            return;
        }
        var threshold = scored.Select(a => a.FaceThreshold).FirstOrDefault(t => t != null) ?? 0.35;
        sb.AppendLine($"Có **{scored.Count}** lượt tới được lớp khuôn mặt (ngưỡng đang dùng: cosine ≥ **{threshold:0.00}**).").AppendLine();

        if (matches == null)
        {
            var s = scored.Select(a => a.FaceScore!.Value).OrderBy(x => x).ToList();
            sb.AppendLine($"Độ tương đồng: nhỏ nhất {s[0]:0.000}, trung vị {Percentile(s, 0.5):0.000}, lớn nhất {s[^1]:0.000}. " +
                          "Muốn tính tỉ lệ từ chối nhầm / chấp nhận nhầm theo từng ngưỡng, hãy truyền thêm `--sheet` để biết lượt nào là người thật và lượt nào là người lạ.").AppendLine();
            return;
        }

        // Người thật = phiếu ghi kỳ vọng ghi nhận; người lạ = phiếu ghi kỳ vọng bị chặn ở lớp face
        var genuine = matches.Where(m => m.Attempt?.FaceScore != null && m.Row.Expected is "accepted" or "pending_approval").Select(m => m.Attempt!.FaceScore!.Value).ToList();
        var impostor = matches.Where(m => m.Attempt?.FaceScore != null && m.Row.Expected == "rejected" && m.Row.ExpectedStage == "face").Select(m => m.Attempt!.FaceScore!.Value).ToList();
        sb.AppendLine($"Theo phiếu ghi: **{genuine.Count}** lượt người thật, **{impostor.Count}** lượt người lạ / mạo danh.").AppendLine();
        if (genuine.Count == 0 || impostor.Count == 0)
        {
            sb.AppendLine("Cần cả hai nhóm (người thật và người lạ có `expected=rejected, expected_stage=face`) mới tính được FRR/FAR.").AppendLine();
            return;
        }
        sb.AppendLine($"- Người thật: nhỏ nhất {genuine.Min():0.000}, trung bình {genuine.Average():0.000}, lớn nhất {genuine.Max():0.000}");
        sb.AppendLine($"- Người lạ: nhỏ nhất {impostor.Min():0.000}, trung bình {impostor.Average():0.000}, lớn nhất {impostor.Max():0.000}").AppendLine();
        sb.AppendLine("| Ngưỡng cosine | FRR — người thật bị từ chối nhầm | FAR — người lạ lọt qua |").AppendLine("|---:|---:|---:|");
        for (var t = 0.20; t <= 0.601; t += 0.05)
        {
            var frr = genuine.Count(x => x < t);
            var far = impostor.Count(x => x >= t);
            var mark = Math.Abs(t - threshold) < 0.0251 ? " ← gần ngưỡng đang dùng" : "";
            sb.AppendLine($"| {t:0.00}{mark} | {Pct(frr, genuine.Count)} ({frr}/{genuine.Count}) | {Pct(far, impostor.Count)} ({far}/{impostor.Count}) |");
        }
        sb.AppendLine();
        sb.AppendLine("> Lưu ý: ảnh chụp lại màn hình / ảnh in của chính chủ KHÔNG nằm trong nhóm người lạ — hệ thống chưa có chống giả mạo (liveness), kết quả các lượt đó ghi riêng ở mục 4 như một hạn chế đã biết (xem docs/FACE_THRESHOLD.md).").AppendLine();
    }

    private static void Scenarios(StringBuilder sb, List<Match> matches, int windowMinutes)
    {
        sb.AppendLine("## 4. Đối chiếu phiếu ghi của người thử với hệ thống").AppendLine();
        sb.AppendLine($"Mỗi dòng phiếu ghép với lượt gần nhất của đúng nhân viên (lệch tối đa {windowMinutes} phút).").AppendLine();
        sb.AppendLine("| Kịch bản | Số lần thử | Đúng kỳ vọng | Chặn nhầm | Lọt | Khác | Không thấy lượt |").AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var g in matches.GroupBy(m => m.Row.Scenario).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            int C(Func<Match, bool> f) => g.Count(f);
            sb.AppendLine($"| {g.Key} | {g.Count()} | {C(m => m.Ok)} | {C(m => m.Verdict.StartsWith("Chặn nhầm"))} | {C(m => m.Verdict.StartsWith("Lọt"))} | " +
                          $"{C(m => m.Attempt != null && !m.Ok && !m.Verdict.StartsWith("Chặn nhầm") && !m.Verdict.StartsWith("Lọt"))} | {C(m => m.Attempt == null)} |");
        }
        var total = matches.Count;
        var ok = matches.Count(m => m.Ok);
        sb.AppendLine($"| **Tổng** | **{total}** | **{ok}** ({Pct(ok, total)}) | {matches.Count(m => m.Verdict.StartsWith("Chặn nhầm"))} | {matches.Count(m => m.Verdict.StartsWith("Lọt"))} | " +
                      $"{matches.Count(m => m.Attempt != null && !m.Ok && !m.Verdict.StartsWith("Chặn nhầm") && !m.Verdict.StartsWith("Lọt"))} | {matches.Count(m => m.Attempt == null)} |").AppendLine();

        var client = matches.Where(m => m.Row.ClientMs != null && m.Attempt != null && m.Attempt.Outcome != "error").ToList();
        if (client.Count > 0)
        {
            var e2e = client.Select(m => m.Row.ClientMs!.Value).OrderBy(x => x).ToList();
            var server = client.Select(m => (double)m.Attempt!.DurationMs).OrderBy(x => x).ToList();
            sb.AppendLine("**Thời gian từ lúc bấm nút tới lúc thấy kết quả** (đồng hồ bấm trên máy người thử, gồm cả mạng):").AppendLine();
            sb.AppendLine("| | Số lượt | Trung bình (ms) | Trung vị (ms) | P95 (ms) |").AppendLine("|---|---:|---:|---:|---:|");
            sb.AppendLine($"| Toàn trình (máy người thử) | {e2e.Count} | {e2e.Average():0} | {Percentile(e2e, 0.5):0} | {Percentile(e2e, 0.95):0} |");
            sb.AppendLine($"| Riêng máy chủ xử lý | {server.Count} | {server.Average():0} | {Percentile(server, 0.5):0} | {Percentile(server, 0.95):0} |").AppendLine();
        }

        var bad = matches.Where(m => !m.Ok).ToList();
        if (bad.Count > 0)
        {
            sb.AppendLine("### Các lượt sai lệch cần xem lại").AppendLine();
            sb.AppendLine("| Dòng | Kịch bản | NV | Giờ | Kỳ vọng | Thực tế (lớp) | Nhận định | Ghi chú / lý do hệ thống |").AppendLine("|---:|---|---|---|---|---|---|---|");
            foreach (var m in bad)
            {
                var expected = m.Row.ExpectedStage.Length > 0 ? $"{m.Row.Expected} ({m.Row.ExpectedStage})" : m.Row.Expected;
                var actual = m.Attempt == null ? "–" : $"{m.Attempt.Outcome} ({m.Attempt.Stage})";
                var note = string.Join(" · ", new[] { m.Row.Note, m.Attempt?.Message }.Where(x => !string.IsNullOrWhiteSpace(x))).Replace("|", "/");
                sb.AppendLine($"| {m.Row.Line} | {m.Row.Scenario} | {m.Row.EmployeeCode} | {m.Row.Time:dd/MM HH:mm} | {expected} | {actual} | {m.Verdict} | {note} |");
            }
            sb.AppendLine();
        }
    }

    private static string OutcomeName(string outcome) => outcome switch
    {
        "accepted" => "Ghi nhận hợp lệ (accepted)",
        "pending_approval" => "Ghi nhận, chờ duyệt (pending_approval)",
        "rejected" => "Bị từ chối (rejected)",
        "error" => "Lỗi hệ thống (error)",
        _ => outcome
    };

    private static string Pct(int part, int whole) => whole == 0 ? "–" : (100.0 * part / whole).ToString("0.0", Vn) + "%";

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 1) return sorted[0];
        var rank = p * (sorted.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }

    private static string N(double? v, string format = "0.##") => v == null ? "" : v.Value.ToString(format, Vn);

    private static string Q(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
