# Triển khai FlexiWork lên cloud

| Thành phần | Nền tảng | Gói |
|---|---|---|
| CSDL MySQL | **TiDB Cloud** (tương thích MySQL 8) | Starter (free) — 5 GiB, 50 triệu RU/tháng |
| Backend API (Docker) + Redis | **Render** (Blueprint `render.yaml`) | Free |
| Web Admin | **Vercel** | Hobby (free) |
| Gửi email | **Brevo** (HTTP API) | Free — 300 email/ngày |
| Lưu ảnh | **Cloudinary** | Free |
| App di động | **Expo EAS Build** | Free — làm ở Task 3.4 |
| AI Service — Face ID (InsightFace, Docker) | **Hugging Face Spaces** (Space riêng tư) | CPU basic (free) — 2 vCPU, 16 GB RAM — làm ở Task 2.4 |
| Giữ Backend luôn thức | **UptimeRobot** | Free — kiểm tra 5 phút/lần |
| Trợ lý AI (chatbot) | **Claude API** (Anthropic) | Trả theo lượng dùng (nạp trước ~5–10 USD) — làm ở Cấp 7 |

> **Phân bổ chức năng AI (đã chốt 15/11/2026):** chỉ phần cần chạy mô hình trên máy chủ (InsightFace) mới đặt ở Hugging Face. Chatbot do backend .NET điều phối (mô hình Claude chạy trên máy chủ Anthropic; các công cụ đọc CSDL và kiểm tra quyền ngay trong backend, cùng vùng Singapore với TiDB). Chấm điểm nguy cơ nghỉ việc là tác vụ nền của backend. Render chỉ chạy **một** web service (750 giờ free/tháng ≈ đủ cho 1 service chạy 24/7).

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

## Bước 7 — AI Service trên Hugging Face Spaces (Task 2.4)

Vì sao không dùng Render: InsightFace cần ~300–450 MB RAM (sát giới hạn 512 MB của Render free) và service thứ hai trên Render làm vượt 750 giờ/tháng → phải ngủ, lần chấm công đầu chờ 1–2 phút. Space free có 16 GB RAM, chỉ ngủ sau ~48 giờ không dùng.

1. Đăng ký <https://huggingface.co> (đăng nhập bằng GitHub được).
2. **New Space**: tên `flexiwork-ai`, SDK **Docker** (Blank), Hardware **CPU basic · Free**, Visibility **Private** → **Create Space**.
3. **Settings → Access Tokens** (trang tài khoản) tạo 2 token:
   - `flexiwork-deploy` — quyền **Write** (GitHub Actions đẩy code lên Space).
   - `flexiwork-backend` — quyền **Read** (backend gọi vào Space riêng tư).
4. Trong Space → **Settings → Variables and secrets → New secret**: `AI_API_KEY` = một chuỗi ngẫu nhiên dài (tự sinh, VD 40 ký tự).
5. GitHub repo → **Settings → Secrets and variables → Actions**:
   - Secret `HF_TOKEN` = token `flexiwork-deploy`.
   - Variable `HF_SPACE_ID` = `<tên-tài-khoản-hf>/flexiwork-ai`.
   Từ đây mỗi lần push có thay đổi trong `FW.AiService/`, workflow `deploy-ai-space.yml` tự cập nhật Space.
6. Render → `flexiwork-api` → **Environment** thêm:

   | Biến | Giá trị |
   |---|---|
   | `AiService__BaseUrl` | `https://<tên-tài-khoản-hf>-flexiwork-ai.hf.space` |
   | `AiService__HfToken` | token `flexiwork-backend` |
   | `AiService__ApiKey` | trùng giá trị `AI_API_KEY` ở bước 4 |
   | `AiService__TimeoutSeconds` | `60` (chờ Space thức dậy) |

7. Kiểm tra: Render Logs không báo lỗi gọi AI; đăng ký Face ID thử trên app.

> Trước buổi demo/bảo vệ: mở Space một lần (Space ngủ sau ~48 giờ không dùng). Khi chạy trên máy: `docker run -p 8000:8000` AI Service, backend mặc định gọi `http://localhost:8000`, không cần token.

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
& $mysql --host=<HOST> --port=4000 --user=<USERNAME> -p --ssl-mode=REQUIRED --default-character-set=utf8mb4 -e "source 003_violation_rules_system_settings.sql;"   # thay bằng tên file cần chạy
```

| File | Nội dung | TiDB |
|---|---|---|
| `001_users_password_changed_at.sql` | Cột `users.password_changed_at` — đổi mật khẩu thì đăng xuất các thiết bị khác | ✅ |
| `002_shifts_schedules_face_profiles.sql` | Khung ca theo chi nhánh + số phút cho phép đi muộn/về sớm; lịch nháp/công bố; Face ID lưu mã ảnh & tên mô hình | ✅ |
| `003_violation_rules_system_settings.sql` | Quy tắc trừ công / phạt theo bậc + số công chuẩn tháng (kèm giá trị mặc định) | ✅ |

## Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| Web báo "Không thể kết nối tới máy chủ" | Backend đang ngủ (chờ 1 phút) hoặc sai `VITE_API_BASE_URL` (phải kết thúc bằng `/api`) |
| Trình duyệt báo lỗi CORS | `Cors__AllowedOrigins__0` chưa đúng domain Vercel (không có `/` ở cuối) |
| Render Logs: `Chưa cấu hình gửi email` | Thiếu `Brevo__ApiKey` |
| Render Logs: lỗi kết nối MySQL | Thiếu `SslMode=Required`, sai cổng (TiDB là `4000`), hoặc username thiếu phần tiền tố `xxxxxxxx.root` |
| Không tạo được Admin đầu tiên | Mật khẩu Bootstrap không đạt quy tắc, hoặc chưa chạy `flexiwork_seed_production.sql` (thiếu bảng vai trò) |
| Email không tới | Email người gửi chưa được xác minh trên Brevo; kiểm tra thư mục Spam |
| Chấm công / đăng ký Face ID báo "hệ thống nhận diện đang khởi động" | Space đang ngủ — chờ 30–60 giây rồi thử lại |
| Render Logs: AI Service trả 401 / 404 | Sai `AiService__HfToken` (token Read) hoặc sai `AiService__BaseUrl`; 403 = `AiService__ApiKey` khác `AI_API_KEY` của Space |
| Space build lỗi | Xem tab **Logs → Build** trên Space; kiểm tra secret `HF_TOKEN` và variable `HF_SPACE_ID` trên GitHub |
