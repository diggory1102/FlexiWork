# Dữ liệu demo 30–80 nhân sự (Task 6.4)

Công cụ **`FW.Backend/tools/DemoDataSeeder`** nạp một bộ dữ liệu mô phỏng một chuỗi cửa hàng đang vận hành, để demo và để đo số liệu cho Chương 4.
Nó chạy trên MySQL local lẫn TiDB Cloud, **chạy lại được bao nhiêu lần cũng được** (mỗi lần xóa bộ demo cũ rồi tạo lại, không đụng dữ liệu khác).

## 1. Bộ dữ liệu gồm những gì (mặc định 60 nhân viên)

| Thành phần | Nội dung |
|---|---|
| Chi nhánh | 4: `CN-HN-01` Cầu Giấy, `CN-HN-02` Hoàn Kiếm, `CN-HN-03` Đống Đa, `CN-HCM-01` Quận 1 (có sẵn thì dùng lại, thiếu thì tạo kèm Wi-Fi). Nhân viên chia 20 / 15 / 12 / 13 |
| Tài khoản | 4 Quản lý (`DMQ1`–`DMQ4`, mỗi chi nhánh một người) + 60 nhân viên (`DM001`–`DM060`): 60% toàn thời gian (có lương cơ bản), 40% bán thời gian; 2 người đã nghỉ việc, 1 người bị khóa (không có lịch) |
| Lịch làm việc | ~12 tuần (10 tuần trước, tuần này, 2 tuần sau) ≈ 5.000 ca bằng ba khung ca demo `DM-SANG` 8–12h, `DM-CHIEU` 13–17h, `DM-TOI` 18–22h (mỗi ca nửa ngày = **0,5 công**). Toàn thời gian: 5 ngày/tuần, hai ca sáng + chiều. Bán thời gian: 3–4 ngày/tuần, một ca. Lịch tới 7 ngày sau đã công bố, xa hơn là bản nháp (Quản lý có việc để công bố) |
| Chấm công | ≈ 4.200 lượt cho mọi ca đã qua, đủ ba lớp xác minh (GPS trong bán kính, Wi-Fi của chi nhánh, khuôn mặt), phút muộn / về sớm / giờ làm tính bằng **đúng hàm của backend** nên bảng công khớp. Gồm ~24 lượt chờ duyệt (Wi-Fi lạ), vài lượt đã bị từ chối, vài lượt quên check-out, ca đang diễn ra hôm nay |
| Đơn từ | ≈ 70: nghỉ phép năm / nghỉ ốm có ảnh (đã duyệt, từ chối, chờ duyệt), làm thêm giờ, giải trình đi muộn (chờ duyệt), xin đổi ca nhờ đồng nghiệp làm thay (chờ duyệt). Đơn nghỉ đã duyệt chuyển ca thành "Nghỉ phép" và trừ quỹ phép |

### Nhân viên "xấu dần" (làm sẵn dữ liệu cho cảnh báo nghỉ việc ở Task 7.4)

5–8 nhân viên (mặc định 6 người, rải đều các chi nhánh) có hành vi: **7 tuần đầu như người bình thường**, tuần thứ 3 từ cuối bắt đầu nhích lên, **2 tuần gần nhất** thì đi muộn nhiều gấp ~5 lần và muộn lâu gấp ~4 lần, bắt đầu vắng, xin nghỉ và xin đổi ca. Ví dụ đo trên bộ mặc định:

| Tuần | Nhóm "xấu dần" (lượt muộn / phút TB / ca vắng) | Nhóm còn lại |
|---|---|---|
| 5 tuần đầu | 1–8 lượt, 6–9 phút, 0–1 ca vắng | ~30 lượt trên ~340 ca, 9–14 phút, 0–5 ca vắng |
| tuần 09/11 | **25 lượt, 26,7 phút, 6 ca vắng** | 31 lượt, 10,5 phút, 2 ca vắng |
| tuần 16/11 | **25 lượt, 18,4 phút, 5 ca vắng** | 27 lượt, 8,6 phút, 3 ca vắng |

Báo cáo vi phạm của chính hệ thống (Web → Báo cáo) đã xếp những người này đầu danh sách (tỉ lệ ca vi phạm 71–74%). Mã của họ **cố định theo hạt giống** (mặc định: `DM017, DM025, DM031, DM034, DM043, DM051` với 60 nhân viên); chạy lại in danh sách ở cuối.

## 2. Đăng nhập demo

- Quản lý: `dmq1@demo.flexiwork.vn` … `dmq4@demo.flexiwork.vn`; nhân viên: `dm001@demo.flexiwork.vn` … `dm060@demo.flexiwork.vn`.
- **Mật khẩu chung do bạn đặt khi chạy** (xem mục 3). Trên CSDL local mặc định là `Admin@123`. Repo này **công khai**, nên với CSDL trên mạng (TiDB) công cụ **từ chối** `Admin@123` và mật khẩu ngắn hơn 8 ký tự; đừng đưa mật khẩu demo vào Git.
- Tài khoản demo **chưa có khuôn mặt đăng ký** (không có ảnh thật): muốn chấm công bằng app phải đăng ký Face ID trước. Dùng tài khoản nhân viên thật cho thực nghiệm chấm công.

## 3. Cách chạy

**Yêu cầu:** đã chạy migration tới `015` trên CSDL đích (công cụ ghi cả cột `min_staff / max_staff` của ca), .NET 8 SDK.

### Trên MySQL local
```powershell
dotnet run -c Release --project FW.Backend/tools/DemoDataSeeder -- --connection "Server=localhost;Port=3306;Database=flexiwork_db;User=root;Password=...;CharSet=utf8mb4;"
```

### Trên TiDB Cloud (làm trên máy bạn, vì cần mật khẩu CSDL)
```powershell
# Chuỗi kết nối: lấy đúng giá trị của biến ConnectionStrings__DefaultConnection trên Render (có SslMode=Required)
$env:FW_SEED_DB = '<chuỗi kết nối TiDB>'
# Mật khẩu chung cho các tài khoản demo — tự đặt, >= 8 ký tự, KHÔNG phải Admin@123
$env:FW_DEMO_PASSWORD = '<mật khẩu demo>'
dotnet run -c Release --project FW.Backend/tools/DemoDataSeeder -- --yes
# Xong thì xóa biến khỏi phiên làm việc:
Remove-Item Env:FW_SEED_DB, Env:FW_DEMO_PASSWORD
```
`--yes` bắt buộc khi CSDL không phải localhost (chống nạp nhầm). Toàn bộ chạy trong **một transaction**: lỗi giữa chừng thì hoàn tác hết. Trên máy local mất vài giây (≈ 10.000 dòng được ghi); qua mạng tới TiDB Cloud lâu hơn nhưng vẫn trong khoảng một phút. Lượng ghi nhỏ so với hạn mức của gói TiDB Starter.

### Tùy chọn
`--employees N` (30–80, mặc định 60) · `--weeks-back N` (mặc định 10) · `--weeks-ahead N` (mặc định 2) · `--seed N` (hạt giống ngẫu nhiên) · `--today yyyy-MM-dd` · `--reset-only` (chỉ xóa bộ demo cũ).

**Nên chạy lại ngay trước buổi demo/báo cáo** để lịch "hôm nay", "tuần này" và các đơn chờ duyệt luôn mới (dữ liệu tính theo ngày chạy).

### Xóa dữ liệu demo
```powershell
dotnet run -c Release --project FW.Backend/tools/DemoDataSeeder -- --reset-only --yes
```
Xóa tài khoản `DM…` cùng lịch, chấm công, đơn, quỹ phép, bảng công, phiếu lương của họ. **Giữ lại** 4 chi nhánh demo, 3 khung ca `DM-*` và chức vụ đã tạo (xóa tay trong Web Admin nếu không cần).

## 4. Gợi ý kịch bản demo trên bộ dữ liệu này

1. **HR → Chấm công → Bảng công**: bấm **Tính** cho tháng 10 và 11/2026 → 57 bảng công; xem chi tiết một người, **Xác nhận / Khóa** theo chi nhánh, **Xuất Excel**.
2. **Báo cáo → Vi phạm chấm công**: 2 tuần gần nhất, các nhân viên "xấu dần" đứng đầu; so với 8 tuần trước.
3. **Quản lý (dmq1) → Duyệt đơn**: hàng chờ có đơn nghỉ, đổi ca, giải trình; **Nhật ký chấm công**: lượt chờ duyệt Wi-Fi lạ, chỉnh giờ ra cho lượt quên check-out.
4. **Xếp lịch**: lưới 12 tuần, **Công bố** lịch nháp, thử **Định biên ca**.
5. **Lương**: cấu hình lương đã có sẵn cho nhân viên toàn thời gian (8–12 triệu); bán thời gian dùng đơn giá theo chức vụ. Đã thử tính kỳ lương 10/2026 trên bộ mặc định: **57 phiếu, tổng thực nhận ≈ 329 triệu** (toàn thời gian trung bình ≈ 7,8 triệu với ≈ 19 công, bán thời gian ≈ 2,3 triệu với ≈ 60 giờ). Chốt kỳ cần **khóa bảng công** của mọi nhân viên có bảng công và cấu hình lương đủ — nếu CSDL còn nhân viên khác chưa cấu hình, hệ thống sẽ nêu rõ người nào còn thiếu.

## 5. Giới hạn

- Không có tin nhắn chat và thông báo mẫu; không có ảnh khuôn mặt / ảnh chấm công thật (ảnh minh họa dùng ảnh công khai của Cloudinary).
- `created_at` của lượt chấm công là lúc nạp dữ liệu (cột do CSDL tự gán); ngày gửi của đơn từ đã được chỉnh cho hợp lý.
- Bảng công và phiếu lương **không** được tạo sẵn: để HR tự tính trên giao diện (cho thấy luồng thật).
- Mô hình hành vi (xác suất muộn, vắng…) là giả định để minh họa, không phải số liệu đo thật; số liệu thực nghiệm thật là việc của Task 6.5.
