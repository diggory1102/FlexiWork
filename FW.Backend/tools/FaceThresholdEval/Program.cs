// Hiệu chỉnh ngưỡng nhận diện khuôn mặt (Task 6.2) từ một bộ ảnh có nhãn người.
//
//   dotnet run -c Release --project FW.Backend/tools/FaceThresholdEval -- <thư mục ảnh> <thư mục mô hình> [tùy chọn]
//
// Thư mục ảnh: mỗi người một thư mục con, trong đó là các ảnh của người đó:   <thư mục ảnh>/<người>/<ảnh>.jpg
// Thư mục mô hình: chứa det_500m.onnx và w600k_mbf.onnx (VD FW.Backend/FW.API/models/buffalo_sc).
// Tùy chọn:  --max-per-person N (mặc định 12)   --max-persons N (mặc định không giới hạn)   --det-size N (mặc định 640)
//            --min-face N (mặc định 64, cùng FaceRecognition:MinFaceSize)   --out báo-cáo.md
//            --probe <thư mục ảnh thử>  chế độ "đăng ký ảnh tốt, chấm công ảnh kém": thư mục này có CÙNG cấu trúc (người/tên ảnh) nhưng ảnh đã bị
//                                       làm xấu (thiếu sáng, nhòe, nén mạnh…). Chỉ ghép ảnh đăng ký (thư mục chính) với ảnh thử khác ảnh gốc.
//            --label <chữ>              nhãn ghi trong báo cáo (VD "thiếu sáng")
//
// Dùng ĐÚNG các lớp phát hiện / căn chỉnh / trích vector của backend (ScrfdDetector, FaceAligner, ArcFaceEncoder), nên số đo phản ánh
// hệ thống thật. Với mỗi cặp ảnh tính độ giống cosine:
//   - cặp CÙNG người  (genuine)  — kỳ vọng cao;
//   - cặp KHÁC người  (impostor) — kỳ vọng thấp.
// Từ đó suy ra hai ngưỡng:
//   MatchThreshold      (chấm công)        : người lạ qua được (FAR) đủ nhỏ, người thật bị từ chối nhầm (FRR) vẫn chấp nhận được;
//   DuplicateThreshold  (chặn đăng ký trùng): hai nhân viên khác nhau bị coi là trùng nhầm phải rất hiếm (tính trên cả công ty),
//                                              mà cùng một người đăng ký hai tài khoản vẫn bị bắt.
using System.Diagnostics;
using System.Globalization;
using System.Text;
using FW.Infrastructure.Services.FaceRecognition.Onnx;
using Microsoft.ML.OnnxRuntime;

if (args.Length < 2)
{
    Console.Error.WriteLine("Cách dùng: FaceThresholdEval <thư mục ảnh> <thư mục mô hình> [--max-per-person N] [--max-persons N] [--det-size N] [--min-face N] [--out báo-cáo.md]");
    return 1;
}

var datasetDir = args[0];
var modelsDir = args[1];
int maxPerPerson = IntOption("--max-per-person", 12);
int maxPersons = IntOption("--max-persons", int.MaxValue);
int detSize = IntOption("--det-size", 640);
int minFace = IntOption("--min-face", 64);
var outFile = StringOption("--out");
var probeDir = StringOption("--probe");
var label = StringOption("--label");

int IntOption(string name, int fallback)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? int.Parse(args[i + 1], CultureInfo.InvariantCulture) : fallback;
}

string? StringOption(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var inv = CultureInfo.InvariantCulture;
var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, IntraOpNumThreads = 4, InterOpNumThreads = 1 };
using var detector = new ScrfdDetector(Path.Combine(modelsDir, "det_500m.onnx"), detSize, options);
using var encoder = new ArcFaceEncoder(Path.Combine(modelsDir, "w600k_mbf.onnx"), options);

// ---------------------------------------------------------------- 1. Trích vector từng ảnh
var total = 0;
var noFace = 0;
var tooSmall = 0;
var clock = Stopwatch.StartNew();

// Mỗi người một thư mục; trả về (tên người, tên ảnh, vector) — ảnh không thấy mặt / mặt quá nhỏ bị bỏ (giống hệ thống thật)
List<(string Person, string File, float[] Vector)> Extract(string dir, IReadOnlyCollection<string>? onlyPersons)
{
    var result = new List<(string, string, float[])>();
    var folders = Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.Ordinal)
        .Where(d => onlyPersons == null || onlyPersons.Contains(Path.GetFileName(d))).Take(maxPersons).ToList();
    for (var p = 0; p < folders.Count; p++)
    {
        var files = Directory.GetFiles(folders[p]).Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal).Take(maxPerPerson).ToList();
        foreach (var file in files)
        {
            total++;
            using var stream = File.OpenRead(file);
            var (image, scale) = RgbImage.Decode(stream, 1280);
            var faces = detector.Detect(image, 0.5f);
            if (faces.Count == 0)
            {
                noFace++;
                continue;
            }

            var face = faces.MaxBy(f => f.Area)!;
            if (Math.Min(face.Width, face.Height) / scale < minFace)
            {
                tooSmall++;
                continue;
            }

            result.Add((Path.GetFileName(folders[p]), Path.GetFileName(file), encoder.Encode(FaceAligner.Align(image, face.Landmarks))));
        }

        if ((p + 1) % 20 == 0 || p == folders.Count - 1)
            Console.Error.WriteLine($"  {dir}: {p + 1}/{folders.Count} người, {result.Count} vector, {clock.Elapsed.TotalSeconds:0}s");
    }
    return result;
}

var enrolled = Extract(datasetDir, null);
var personCount = enrolled.Select(e => e.Person).Distinct().Count();
List<(string Person, string File, float[] Vector)>? probes = null;
var probeSkipped = 0;
if (probeDir != null)
{
    var before = noFace + tooSmall;
    probes = Extract(probeDir, enrolled.Select(e => e.Person).Distinct().ToHashSet());
    probeSkipped = noFace + tooSmall - before; // ảnh thử không còn nhận ra mặt (thiếu sáng / nhòe / quá nhỏ) = lần chấm công bị từ chối ngay ở bước phát hiện
}

var usable = enrolled.GroupBy(e => e.Person).Where(g => g.Count() >= 2).SelectMany(g => g).ToList();
if (probes == null) personCount = usable.Select(e => e.Person).Distinct().Count();

// ---------------------------------------------------------------- 2. Độ giống của mọi cặp ảnh
static double Cosine(float[] a, float[] b)
{
    double dot = 0, na = 0, nb = 0;
    for (var i = 0; i < a.Length; i++)
    {
        dot += a[i] * b[i];
        na += a[i] * a[i];
        nb += b[i] * b[i];
    }
    return dot / Math.Sqrt(na * nb);
}

var genuine = new List<double>();
var impostor = new List<double>();
if (probes == null)
{
    // Mọi cặp trong cùng một bộ ảnh
    for (var i = 0; i < usable.Count; i++)
    {
        for (var j = i + 1; j < usable.Count; j++)
        {
            var sim = Cosine(usable[i].Vector, usable[j].Vector);
            (usable[i].Person == usable[j].Person ? genuine : impostor).Add(sim);
        }
    }
}
else
{
    // Ảnh đăng ký (tốt) × ảnh thử (đã làm xấu), bỏ cặp cùng một ảnh gốc
    foreach (var a in enrolled)
    {
        foreach (var b in probes)
        {
            if (a.Person == b.Person && a.File == b.File) continue;
            var sim = Cosine(a.Vector, b.Vector);
            (a.Person == b.Person ? genuine : impostor).Add(sim);
        }
    }
}

genuine.Sort();
impostor.Sort();
if (genuine.Count == 0 || impostor.Count == 0)
{
    Console.Error.WriteLine($"Đọc {total} ảnh: không thấy mặt {noFace}, mặt nhỏ hơn {minFace}px {tooSmall}" + (probes != null ? $"; ảnh thử dùng được {probes.Count}" : "") + ".");
    Console.Error.WriteLine("Không đủ cặp để đánh giá (cần ít nhất 2 người, mỗi người ≥ 2 ảnh có mặt).");
    return 2;
}

// ---------------------------------------------------------------- 3. Chỉ số theo ngưỡng
// FAR(t) = tỉ lệ cặp KHÁC người có độ giống ≥ t (người lạ lọt qua);  FRR(t) = tỉ lệ cặp CÙNG người có độ giống < t (người thật bị từ chối).
double Far(double t) => 1.0 - LowerBound(impostor, t) / (double)impostor.Count;
double Frr(double t) => LowerBound(genuine, t) / (double)genuine.Count;

static int LowerBound(List<double> sorted, double value)
{
    int lo = 0, hi = sorted.Count;
    while (lo < hi)
    {
        var mid = (lo + hi) / 2;
        if (sorted[mid] < value) lo = mid + 1; else hi = mid;
    }
    return lo;
}

static double Percentile(List<double> sorted, double q) => sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(q * sorted.Count))];

// Ngưỡng nhỏ nhất để FAR ≤ mục tiêu (dựa trên phân phối cặp khác người)
double ThresholdForFar(double target)
{
    var allowed = (int)Math.Floor(target * impostor.Count); // số cặp khác người được phép ≥ ngưỡng
    var index = impostor.Count - allowed;
    return index >= impostor.Count ? impostor[^1] + 1e-6 : impostor[Math.Max(0, index - 1)] + 1e-6;
}

var eerThreshold = 0.0;
var eerGap = double.MaxValue;
for (var t = 0.0; t <= 1.0; t += 0.0025)
{
    var gap = Math.Abs(Far(t) - Frr(t));
    if (gap < eerGap)
    {
        eerGap = gap;
        eerThreshold = t;
    }
}

// Công ty ~80 nhân viên: mỗi lần đăng ký so với 79 người khác → xác suất bị chặn nhầm ≈ 79 · FAR
const int Employees = 80;
double CompanyFalseBlock(double far) => 1 - Math.Pow(1 - far, Employees - 1);

var sb = new StringBuilder();
void Line(string text = "") => sb.AppendLine(text);
string Pct(double v) => (v * 100).ToString(v < 0.001 ? "0.0000" : "0.00", inv) + "%";

Line("# Báo cáo hiệu chỉnh ngưỡng nhận diện khuôn mặt");
Line();
Line($"- Bộ ảnh đăng ký: `{datasetDir}`" + (probeDir != null ? $"; ảnh thử{(label != null ? $" ({label})" : "")}: `{probeDir}` — ghép ảnh đăng ký (tốt) với ảnh thử khác ảnh gốc." : "."));
Line($"- Đọc {total} ảnh (tối đa {maxPerPerson}/người). Không thấy mặt: {noFace}; mặt nhỏ hơn {minFace}px: {tooSmall}" + (probeDir != null ? $" (trong đó {probeSkipped} ảnh thử bị loại ngay ở bước phát hiện)" : "") + $"; đánh giá trên **{personCount} người / {(probes?.Count ?? usable.Count)} ảnh {(probes != null ? "thử" : "")}**.");
Line($"- Cặp cùng người: **{genuine.Count:N0}**; cặp khác người: **{impostor.Count:N0}**. Mô hình `buffalo_sc`, cỡ ảnh phát hiện {detSize}. Thời gian trích vector {clock.Elapsed.TotalSeconds:0}s.");
Line();
Line("## Phân phối độ giống (cosine)");
Line();
Line("| | nhỏ nhất | 1% | 5% | trung vị | 95% | 99% | lớn nhất |");
Line("|---|---|---|---|---|---|---|---|");
string Row(string name, List<double> v) =>
    $"| {name} | {v[0]:0.000} | {Percentile(v, 0.01):0.000} | {Percentile(v, 0.05):0.000} | {Percentile(v, 0.5):0.000} | {Percentile(v, 0.95):0.000} | {Percentile(v, 0.99):0.000} | {v[^1]:0.000} |";
Line(Row("Cùng người", genuine));
Line(Row("Khác người", impostor));
Line();
Line("## Tỉ lệ sai theo ngưỡng");
Line();
Line("FAR = người lạ lọt qua (cặp khác người có độ giống ≥ ngưỡng). FRR = người thật bị từ chối (cặp cùng người có độ giống < ngưỡng).");
Line($"“Chặn nhầm / lần đăng ký” = xác suất một nhân viên mới bị coi là trùng với ai đó trong công ty {Employees} người (≈ 79 · FAR).");
Line();
Line("| Ngưỡng | FAR | FRR | Chặn nhầm / lần đăng ký |");
Line("|---|---|---|---|");
foreach (var t in new[] { 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70 })
{
    var far = Far(t);
    Line($"| {t:0.00} | {Pct(far)} | {Pct(Frr(t))} | {Pct(CompanyFalseBlock(far))} |");
}

Line();
Line($"Điểm cân bằng (EER, FAR ≈ FRR): ngưỡng ≈ **{eerThreshold:0.000}**, tỉ lệ lỗi ≈ {Pct((Far(eerThreshold) + Frr(eerThreshold)) / 2)}.");
Line();
Line("## Đề xuất");
Line();
var matchFor1 = ThresholdForFar(0.01);
var matchFor01 = ThresholdForFar(0.001);
var matchFor001 = ThresholdForFar(0.0001);
Line("**MatchThreshold (chấm công)** — người lạ lọt qua phải rất hiếm; người thật bị từ chối chỉ phải chụp lại / chờ duyệt:");
Line();
Line("| Mục tiêu FAR | Ngưỡng | FRR tương ứng |");
Line("|---|---|---|");
Line($"| ≤ 1% | {matchFor1:0.000} | {Pct(Frr(matchFor1))} |");
Line($"| ≤ 0,1% | {matchFor01:0.000} | {Pct(Frr(matchFor01))} |");
Line($"| ≤ 0,01% | {matchFor001:0.000} | {Pct(Frr(matchFor001))} |");
Line();
var dupFor = ThresholdForFar(0.0001 / 1.0); // ~ 0,01% mỗi cặp → ≈ 0,8% bị chặn nhầm mỗi lần đăng ký ở công ty 80 người
var dupStrict = ThresholdForFar(0.00001);
Line($"**DuplicateThreshold (chặn đăng ký trùng)** — mục tiêu: bị chặn nhầm ≤ ~1% mỗi lần đăng ký ở công ty {Employees} người (FAR ≈ 0,01%), mà cùng một người đăng ký hai tài khoản vẫn bị bắt:");
Line();
Line("| Mục tiêu | Ngưỡng | Chặn nhầm / lần đăng ký | Bắt được cặp cùng người |");
Line("|---|---|---|---|");
Line($"| FAR ≤ 0,01% | {dupFor:0.000} | {Pct(CompanyFalseBlock(Far(dupFor)))} | {Pct(1 - Frr(dupFor))} |");
Line($"| FAR ≤ 0,001% | {dupStrict:0.000} | {Pct(CompanyFalseBlock(Far(dupStrict)))} | {Pct(1 - Frr(dupStrict))} |");
Line();
Line($"Hiện cấu hình: MatchThreshold = 0,40 → FAR {Pct(Far(0.40))}, FRR {Pct(Frr(0.40))}; DuplicateThreshold = 0,50 → chặn nhầm {Pct(CompanyFalseBlock(Far(0.50)))} mỗi lần đăng ký, bắt được {Pct(1 - Frr(0.50))} cặp cùng người.");

var report = sb.ToString();
Console.WriteLine(report);
if (outFile != null) File.WriteAllText(outFile, report);
return 0;
