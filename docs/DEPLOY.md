# Triển khai FlexiWork lên cloud

| Thành phần | Nền tảng | Gói |
|---|---|---|
| CSDL MySQL | **Aiven** | Free |
| Backend API (Docker) + Redis | **Render** (Blueprint `render.yaml`) | Free |
| Web Admin | **Vercel** | Hobby (free) |
| Gửi email | **Brevo** (HTTP API) | Free — 300 email/ngày |
| Lưu ảnh | **Cloudinary** | Free |
| App di động | **Expo EAS Build** | Free — làm ở Task 3.4 |
| AI Service (InsightFace) | — | Bổ sung ở Task 2.4 (cần kiểm tra RAM) |

> **Nguyên tắc bảo mật:** repo GitHub đang để công khai → mọi mật khẩu, khóa API chỉ được dán vào trang quản trị của từng dịch vụ, **không** ghi vào file trong repo.

Làm lần lượt theo thứ tự dưới đây (bước sau cần thông tin của bước trước). Đánh dấu ✅ khi xong.

---

## Bước 1 — CSDL MySQL trên Aiven

1. Đăng ký tại <https://console.aiven.io> (có thể đăng nhập bằng GitHub/Google).
2. **Create service → MySQL → Free plan**, chọn vùng gần Việt Nam nhất (Singapore nếu có). Đặt tên `flexiwork-mysql`, chờ trạng thái **Running**.
3. Trong trang service, mục **Connection information** ghi lại: `Host`, `Port`, `User` (`avnadmin`), `Password`.
4. Tab **Databases → Create database** tên `flexiwork_db`.
5. Khởi tạo bảng bằng MySQL client trên máy (PowerShell, đứng ở thư mục gốc repo). Thay `<HOST>`, `<PORT>`:

   ```powershell
   $mysql = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
   cd FW.Backend\FW.Infrastructure\Database
   & $mysql --host=<HOST> --port=<PORT> --user=avnadmin -p --ssl-mode=REQUIRED --default-character-set=utf8mb4 `
       -e "source flexiwork_schema.sql; source flexiwork_seed_production.sql;"
   ```
   Nhập mật khẩu Aiven khi được hỏi. (Hoặc dùng MySQL Workbench: tạo kết nối với SSL = *Require*, mở và chạy lần lượt 2 file.)

   > ⚠️ `flexiwork_schema.sql` **xóa và tạo lại toàn bộ bảng** — chỉ chạy **một lần** lúc khởi tạo. `flexiwork_seed_production.sql` chỉ thêm vai trò và loại đơn, **không có tài khoản mẫu** (tài khoản `Admin@123` của `flexiwork_seed.sql` chỉ dùng trên máy dev).

6. Chuỗi kết nối cho Render (Bước 4):
   ```
   Server=<HOST>;Port=<PORT>;Database=flexiwork_db;User=avnadmin;Password=<PASSWORD>;SslMode=Required;AllowUserVariables=True;CharSet=utf8mb4;
   ```

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
   | `ConnectionStrings__DefaultConnection` | Chuỗi kết nối ở Bước 1.6 |
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

## Bước 6 — App di động (Task 3.4)

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

Không có migration tự động (theo thiết kế Chương 2). Mỗi thay đổi schema ở các task sau sẽ kèm một file `migrations/NNN_*.sql` chỉ chứa `ALTER TABLE`/`CREATE TABLE` — chạy file đó lên Aiven giống Bước 1.5, **không** chạy lại `flexiwork_schema.sql`.

## Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| Web báo "Không thể kết nối tới máy chủ" | Backend đang ngủ (chờ 1 phút) hoặc sai `VITE_API_BASE_URL` (phải kết thúc bằng `/api`) |
| Trình duyệt báo lỗi CORS | `Cors__AllowedOrigins__0` chưa đúng domain Vercel (không có `/` ở cuối) |
| Render Logs: `Chưa cấu hình gửi email` | Thiếu `Brevo__ApiKey` |
| Render Logs: lỗi kết nối MySQL | Thiếu `SslMode=Required` hoặc sai host/port/mật khẩu Aiven |
| Không tạo được Admin đầu tiên | Mật khẩu Bootstrap không đạt quy tắc, hoặc chưa chạy `flexiwork_seed_production.sql` (thiếu bảng vai trò) |
| Email không tới | Email người gửi chưa được xác minh trên Brevo; kiểm tra thư mục Spam |
