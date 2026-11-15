# Triển khai FlexiWork lên cloud

| Thành phần | Nền tảng | Gói |
|---|---|---|
| CSDL MySQL | **TiDB Cloud** (tương thích MySQL 8) | Starter (free) — 5 GiB, 50 triệu RU/tháng |
| Backend API (Docker) + Redis | **Render** (Blueprint `render.yaml`) | Free |
| Web Admin | **Vercel** | Hobby (free) |
| Gửi email | **Brevo** (HTTP API) | Free — 300 email/ngày |
| Lưu ảnh | **Cloudinary** | Free |
| App di động | **Expo EAS Build** | Free — làm ở Task 3.4 |
| Nhận diện khuôn mặt (Face ID) | **Chạy ngay trong Backend** (ONNX Runtime + mô hình InsightFace buffalo_sc) | Không cần dịch vụ riêng — mô hình tự tải khi build Docker |
| Giữ Backend luôn thức | **UptimeRobot** | Free — kiểm tra 5 phút/lần |
| Trợ lý AI (chatbot) | **Claude API** (Anthropic) | Trả theo lượng dùng (nạp trước ~5–10 USD) — làm ở Cấp 7 |

> **Phân bổ chức năng AI (đã chốt 15/11/2026):** Hugging Face Spaces không còn chạy được Docker miễn phí (cần gói PRO) → nhận diện khuôn mặt chạy **trong backend** bằng ONNX Runtime (đo với giới hạn của Render free 0,1 CPU / 512 MB: ~0,6–0,85 giây mỗi lần, backend dùng ~151 MB RAM). Chatbot do backend gọi Claude API. Chấm điểm nguy cơ nghỉ việc là tác vụ nền của backend. Render chỉ chạy **một** web service (750 giờ free/tháng ≈ đủ cho 1 service chạy 24/7).

> **Nguyên tắc bảo mật:** repo GitHub đang để công khai → mọi mật khẩu, khóa API chỉ được dán vào trang quản trị của từng dịch vụ, **không** ghi vào file trong repo.

Làm lần lượt theo thứ tự dưới đây (bước sau cần thông tin của bước trước). Đánh dấu ✅ khi xong.

---

## Bước 1 — CSDL MySQL trên TiDB Cloud

> Aiven đã ngừng gói MySQL miễn phí (11/2026). **TiDB Cloud Starter** dùng giao thức MySQL 8 nên backend giữ nguyên `MySqlConnector` và các câu SQL — bộ test tích hợp (33/33) đã chạy đạt trên TiDB v8.5.

1. Đăng ký <https://tidbcloud.com> (đăng nhập bằng GitHub/Google).
2. **Create Instance** (một số giao diện ghi *Create Cluster*) → chọn gói **Starter** (Free) → Cloud Provider **AWS**, Region **Singapore (ap-southeast-1)** → tên `flexiwork` → **Create**. Chờ trạng thái **Available** (vài chục giây).
3. Trong trang instance bấm **Connect** (góc trên bên phải):
   - Connection Type: **Public**, Connect With: **General**.
   - Bấm **Generate Password** → ghi lại ngay `HOST` (dạng `gateway01.ap-southeast-1.prod.aws.tidbcloud.com`), `PORT` (**4000**), `USERNAME` (dạng `xxxxxxxx.root` — **có cả phần tiền tố trước `.root`**) và `PASSWORD`. Mật khẩu **chỉ hiện một lần**; quên thì bấm *Reset Password*.
4. Khởi tạo bảng bằng MySQL client trên máy (PowerShell, đứng ở thư mục gốc repo). Thay `<HOST>`, `<USERNAME>`:

   ```powershell
   $mysql = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
   cd FW.Backend\FW.Infrastructure\Database
   & $mysql --host=<HOST> --port=4000 --user=<USERNAME> -p --ssl-mode=REQUIRED --default-character-set=utf8mb4 `
       -e "source flexiwork_schema.sql; source flexiwork_seed_production.sql;"
   ```
   Nhập mật khẩu TiDB khi được hỏi. Script tự tạo database `flexiwork_db`. Kiểm tra:
   ```powershell
   & $mysql --host=<HOST> --port=4000 --user=<USERNAME> -p --ssl-mode=REQUIRED -e "USE flexiwork_db; SHOW TABLES; SELECT role_name FROM roles;"
   ```
   *Cách khác — MySQL Workbench:* **+** tạo kết nối mới → Hostname `<HOST>`, Port `4000`, Username `<USERNAME>` → tab **SSL**: *Use SSL* = **Require** → **Test Connection** → mở (File → Open SQL Script) và chạy (⚡) lần lượt `flexiwork_schema.sql`, `flexiwork_seed_production.sql`.

   > ⚠️ `flexiwork_schema.sql` **xóa và tạo lại toàn bộ bảng** — chỉ chạy **một lần** lúc khởi tạo. `flexiwork_seed_production.sql` chỉ thêm vai trò và loại đơn, **không có tài khoản mẫu** (tài khoản `Admin@123` của `flexiwork_seed.sql` chỉ dùng trên máy dev).

5. Chuỗi kết nối cho Render (Bước 4):
   ```
   Server=<HOST>;Port=4000;Database=flexiwork_db;User=<USERNAME>;Password=<PASSWORD>;SslMode=Required;AllowUserVariables=True;CharSet=utf8mb4;
   ```

   Khác biệt nhỏ so với MySQL cần biết: TiDB **bắt buộc SSL** (thiếu `SslMode=Required` sẽ bị từ chối); ID tự tăng có thể **nhảy cóc** (1, 2, 30001…) — đúng thiết kế phân tán của TiDB, không phải lỗi, nên không dùng ID để sinh mã hiển thị.

## Bước 2 — Email trên Brevo

Render gói miễn phí chặn cổng SMTP, nên backend gửi email qua HTTP API của Brevo.

1. Đăng ký <https://www.brevo.com> (gói Free).
2. **Senders, Domains & Dedicated IPs → Senders → Add a sender**: dùng email của bạn (VD Gmail riêng cho dự án) và bấm link xác minh Brevo gửi về.
3. **SMTP & API → API Keys → Generate a new API key**, đặt tên `flexiwork-render`, sao chép khóa (`xkeysib-…`) — chỉ hiện một lần.

## Bước 3 — Lưu ảnh trên Cloudinary

1. Đăng ký <https://cloudinary.com> (gói Free).
2. Trang **Dashboard / API Keys** ghi lại `Cloud name`, `API Key`, `API Secret`.

## Bước 4 — Backend trên Render

1. Đăng ký <https://render.com> bằng tài khoản GitHub có repo `FlexiWork`.
2. **New → Blueprint** → chọn repo `diggory1102/FlexiWork` → Render đọc `render.yaml` và đề xuất tạo `flexiwork-api` (web) + `flexiwork-cache` (Key Value / Redis).
3. Điền các biến được hỏi:

   | Biến | Giá trị |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | Chuỗi kết nối ở Bước 1.5 |
   | `Cors__AllowedOrigins__0` | Tạm điền `https://flexiwork.vercel.app` — sửa lại sau Bước 5 nếu Vercel cấp domain khác |
   | `Brevo__ApiKey` | Khóa API Bước 2.3 |
   | `Brevo__SenderEmail` | Email người gửi đã xác minh ở Bước 2.2 |
   | `Cloudinary__CloudName` / `__ApiKey` / `__ApiSecret` | Bước 3 |
   | `Bootstrap__AdminEmail` | Email đăng nhập Quản trị viên đầu tiên (nên là email thật của bạn) |
   | `Bootstrap__AdminPassword` | Mật khẩu mạnh **khác** `Admin@123` (≥ 8 ký tự, có chữ và số) |

   `JwtSettings__Secret` được Render tự sinh ngẫu nhiên; `Redis__ConnectionString` tự lấy từ `flexiwork-cache`.
4. **Apply** → chờ build Docker (lần đầu ~5–8 phút) → trạng thái **Live**.
5. Kiểm tra: mở `https://flexiwork-api.onrender.com/api/healthcheck` (thay bằng URL Render cấp) → thấy `"Healthy"`. Swagger: `https://flexiwork-api.onrender.com/`.
6. Tab **Logs** phải có dòng `Đã tạo tài khoản Quản trị viên đầu tiên: <email>` ở lần khởi động đầu.

> Gói free tự **ngủ sau ~15 phút** không có truy cập; lần truy cập kế tiếp chờ 30–60 giây. Trước buổi demo/bảo vệ hãy mở trước vài phút.

## Bước 5 — Web Admin trên Vercel

1. Đăng ký <https://vercel.com> bằng GitHub → **Add New → Project** → import repo `FlexiWork`.
2. **Root Directory**: `FW.Frontend/web` (Framework tự nhận **Vite**).
3. **Environment Variables**:

   | Biến | Giá trị |
   |---|---|
   | `VITE_API_BASE_URL` | `https://flexiwork-api.onrender.com/api` (URL Render của bạn + `/api`) |
   | `VITE_HUB_BASE_URL` | `https://flexiwork-api.onrender.com/hubs` |

4. **Deploy**. Ghi lại domain Vercel cấp (VD `https://flexiwork-xxxx.vercel.app`).
5. Nếu domain khác giá trị đã điền ở Bước 4.3: vào Render → `flexiwork-api` → **Environment** → sửa `Cors__AllowedOrigins__0` → **Save** (Render tự deploy lại).
6. Mở domain Vercel → đăng nhập bằng tài khoản Bootstrap ở Bước 4.3 → tạo chi nhánh, phòng ban, chức vụ, nhân viên. Nhân viên mới sẽ nhận email mời từ Brevo.

Từ đây mỗi lần `git push` lên `main`: Vercel tự build Web; Render tự build Backend khi có thay đổi trong `FW.Backend/`.

## Bước 6 — Giữ Backend luôn thức (UptimeRobot) — làm được ngay

Render gói free ngủ sau ~15 phút không có truy cập. Một service chạy cả tháng ≈ 744 giờ, vừa trong 750 giờ free — nên **chỉ để duy nhất `flexiwork-api` trên Render** (không tạo thêm web service khác).

1. Đăng ký <https://uptimerobot.com> (gói Free).
2. **New Monitor** → Type **HTTP(s)** → URL `https://flexiwork-api.onrender.com/api/healthcheck` → Interval **5 minutes** → **Create**.
3. Nếu Render báo sắp hết giờ free trong tháng (Billing → Usage): tạm **Pause** monitor; API vẫn chạy, chỉ quay lại cơ chế ngủ.

## Bước 7 — Nhận diện khuôn mặt (Task 2.4) — không cần cấu hình thêm

Nhận diện khuôn mặt chạy ngay trong `flexiwork-api` bằng ONNX Runtime với bộ mô hình InsightFace **buffalo_sc** (SCRFD phát hiện mặt + ArcFace MobileFaceNet trích vector 512 chiều). Dockerfile tự tải mô hình (~16 MB, kiểm tra mã băm SHA-256) ở stage `models` — Render build lâu hơn khoảng 1 phút, không cần biến môi trường nào.

- **Chạy trên máy:** tải mô hình một lần: `powershell -ExecutionPolicy Bypass -File FW.Backend\scripts\download-face-models.ps1` (file `.onnx` không lưu trong repo).
- **Tham số** (section `FaceRecognition`, đổi trên Render bằng biến `FaceRecognition__MatchThreshold`…): ngưỡng khớp mặc định `0.40` (hiệu chỉnh ở thực nghiệm Chương 4), kích thước ảnh phát hiện `640`.
- **Phương án tách dịch vụ:** đặt `FaceRecognition__Engine=AiService` + `AiService__BaseUrl` để gọi microservice `FW.AiService` (FastAPI + InsightFace) thay vì chạy trong backend.
- Mô hình InsightFace chỉ dùng cho nghiên cứu / phi thương mại — phù hợp phạm vi khóa luận.

## Bước 8 — Claude API cho Trợ lý AI (Cấp 7)

1. Đăng ký <https://console.anthropic.com> → **Billing**: nạp trước 5–10 USD, đặt **spend limit** theo tháng (tránh bị trừ quá tay).
2. **API Keys → Create Key** tên `flexiwork-render` — chỉ hiện một lần.
3. Render → `flexiwork-api` → **Environment**: thêm `Anthropic__ApiKey`.

## Bước 9 — App di động (Task 3.4)

Hiện tại dùng **Expo Go** là đủ (trỏ API về Render bằng `.env.local`):
```
EXPO_PUBLIC_API_BASE_URL=https://flexiwork-api.onrender.com/api
EXPO_PUBLIC_HUB_BASE_URL=https://flexiwork-api.onrender.com/hubs
```
Khi làm chấm công (đọc Wi-Fi BSSID cần bản build riêng):
```powershell
npm i -g eas-cli
cd FW.Frontend\mobile
eas login
eas build --profile preview --platform android   # tạo file APK cài trực tiếp lên điện thoại
```
URL API cho bản build nằm trong `FW.Frontend/mobile/eas.json` — sửa nếu Render cấp domain khác.

---

## Khi cập nhật CSDL về sau

Không có migration tự động (theo thiết kế Chương 2). Mỗi thay đổi schema kèm một file `FW.Backend/FW.Infrastructure/Database/migrations/NNN_*.sql` chỉ chứa `ALTER TABLE`/`CREATE TABLE` — chạy file đó lên TiDB giống Bước 1.4, **không** chạy lại `flexiwork_schema.sql`. CSDL tạo mới từ `flexiwork_schema.sql` đã có sẵn mọi thay đổi nên không cần chạy migration.

**Chạy migration *trước* khi `git push`** — code mới trên Render đọc cột mới ngay khi khởi động:

```powershell
$mysql = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
cd FW.Backend\FW.Infrastructure\Database\migrations
& $mysql --host=<HOST> --port=4000 --user=<USERNAME> -p --ssl-mode=REQUIRED --default-character-set=utf8mb4 -e "source 004_face_profiles_model_buffalo_sc.sql;"   # thay bằng tên file cần chạy
```

| File | Nội dung | TiDB |
|---|---|---|
| `001_users_password_changed_at.sql` | Cột `users.password_changed_at` — đổi mật khẩu thì đăng xuất các thiết bị khác | ✅ |
| `002_shifts_schedules_face_profiles.sql` | Khung ca theo chi nhánh + số phút cho phép đi muộn/về sớm; lịch nháp/công bố; Face ID lưu mã ảnh & tên mô hình | ✅ |
| `003_violation_rules_system_settings.sql` | Quy tắc trừ công / phạt theo bậc + số công chuẩn tháng (kèm giá trị mặc định) | ✅ |
| `004_face_profiles_model_buffalo_sc.sql` | Tên mô hình mặc định của `face_profiles` là `buffalo_sc` | ☐ |
| `005_attendance_logs_three_layer.sql` | Thiết kế lại `attendance_logs` cho chấm công 3 lớp (bảng cũ đổi tên thành `attendance_logs_backup_005` — kiểm tra `so_dong_bang_cu = 0` rồi xóa bằng lệnh ở cuối file) | ☐ |

## Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| Web báo "Không thể kết nối tới máy chủ" | Backend đang ngủ (chờ 1 phút) hoặc sai `VITE_API_BASE_URL` (phải kết thúc bằng `/api`) |
| Trình duyệt báo lỗi CORS | `Cors__AllowedOrigins__0` chưa đúng domain Vercel (không có `/` ở cuối) |
| Render Logs: `Chưa cấu hình gửi email` | Thiếu `Brevo__ApiKey` |
| Render Logs: lỗi kết nối MySQL | Thiếu `SslMode=Required`, sai cổng (TiDB là `4000`), hoặc username thiếu phần tiền tố `xxxxxxxx.root` |
| Không tạo được Admin đầu tiên | Mật khẩu Bootstrap không đạt quy tắc, hoặc chưa chạy `flexiwork_seed_production.sql` (thiếu bảng vai trò) |
| Email không tới | Email người gửi chưa được xác minh trên Brevo; kiểm tra thư mục Spam |
| Render Logs: `Chưa có mô hình nhận diện khuôn mặt` | Build Docker không tải được mô hình (mạng / sai mã băm) — Manual Deploy lại; chạy trên máy thì chạy `scripts/download-face-models.ps1` |
| Đăng ký Face ID báo "Ảnh có nhiều khuôn mặt" / "Khuôn mặt quá nhỏ" | Chụp một mình, chính diện, đưa điện thoại lại gần |
