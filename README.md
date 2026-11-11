# FlexiWork

Hệ thống quản lý nhân viên, phân ca và chấm công online đa lớp (GPS + Wi-Fi BSSID + Face ID) tích hợp duyệt nghỉ phép.
Khóa luận tốt nghiệp — Trường Đại học Thăng Long.

## Cấu trúc thư mục

```
FlexiWork/
├── FW.Backend/                 # ASP.NET Core 8 Web API — Clean Architecture
│   ├── FW.Domain/              #   Entities, Enums, interface repository (không phụ thuộc tầng nào)
│   ├── FW.Application/         #   Nghiệp vụ: Features/<Module>, Common (Models, Exceptions, Interfaces, Settings)
│   ├── FW.Infrastructure/      #   Dapper + MySQL, Repositories, Redis, MailKit, Cloudinary, client AI Service
│   │   └── Database/           #     flexiwork_schema.sql, flexiwork_seed.sql
│   ├── FW.API/                 #   Controllers, Hubs (SignalR), Middlewares, Extensions
│   ├── FW.UnitTests/           #   xUnit — kiểm thử logic tính công, đối soát ca
│   └── Dockerfile
├── FW.AiService/               # Python FastAPI + InsightFace — trích xuất & đối soát khuôn mặt
├── FW.Frontend/
│   ├── web/                    # Web Admin: React 18 + TypeScript + Vite + Tailwind + Zustand + Axios + SignalR
│   └── mobile/                 # Mobile App: Expo (React Native) + TypeScript + Expo Router
├── docs/                       # PROGRESS.md (tiến độ, phân quyền), UML.md (use case)
└── docker-compose.yml          # MySQL + Redis + AI Service + API cho môi trường cục bộ
```

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API, Dapper, MySQL 8, JWT Bearer, SignalR, Redis, MailKit (SMTP), Cloudinary, Swagger |
| AI Service | Python 3.10, FastAPI, InsightFace (ArcFace 512D), OpenCV |
| Web Admin | React 18, TypeScript, Vite, Tailwind CSS, Zustand, Axios, React Router, @microsoft/signalr |
| Mobile | Expo SDK 58, React Native, Expo Router, expo-camera, expo-location, NetInfo (BSSID), expo-secure-store |
| Triển khai | Docker, Vercel (web), Render (backend), Aiven (MySQL) |

## Chạy dự án cục bộ

### 1. Hạ tầng (MySQL, Redis, AI Service) bằng Docker

```bash
cp .env.example .env            # chỉnh MYSQL_ROOT_PASSWORD, JWT_SECRET
docker compose up -d mysql redis ai-service
```

Lần chạy đầu, MySQL tự động chạy `flexiwork_schema.sql` và `flexiwork_seed.sql`.

### 2. Backend API

```bash
cd FW.Backend/FW.API
cp appsettings.Development.example.json appsettings.Development.json   # điền chuỗi kết nối, JWT secret...
dotnet run --launch-profile http
```

Swagger: http://localhost:5010 — kiểm tra nhanh: `GET /api/healthcheck`.

**Tài khoản mẫu** (từ `flexiwork_seed.sql`, mật khẩu chung `Admin@123`):

| Email | Vai trò | Chi nhánh |
|---|---|---|
| `admin@flexiwork.vn` | Admin | Cầu Giấy |
| `hr@flexiwork.vn` | HR | — (toàn công ty) |
| `manager.cg@flexiwork.vn` | Manager | Cầu Giấy |
| `nhanvien.cg@flexiwork.vn` | Employee | Cầu Giấy |

Khi phát triển (`ASPNETCORE_ENVIRONMENT=Development`):
- Để trống `Redis:ConnectionString` → dùng cache trong bộ nhớ thay Redis.
- Để trống `Smtp:Host` → email (kể cả mã OTP quên mật khẩu) được **ghi ra console** thay vì gửi thật.

### Kiểm thử

```bash
cd FW.Backend
dotnet test                      # unit test; test tích hợp MySQL tự bỏ qua (Skipped)
```

Test tích hợp (`FW.IntegrationTests`) chạy trên MySQL thật với **CSDL riêng** — tự tạo lại từ schema + seed mỗi lần chạy, không đụng tới `flexiwork_db`:

```powershell
$env:FW_TEST_DB = "Server=localhost;Port=3306;Database=flexiwork_test;User=root;Password=<mat_khau>;"
dotnet test
```

### 3. Web Admin

```bash
cd FW.Frontend/web
cp .env.example .env.local
npm install
npm run dev                     # http://localhost:5173
```

### 4. Mobile App

```bash
# Backend phải lắng nghe trên mạng LAN để điện thoại gọi được (mặc định chỉ localhost)
cd FW.Backend/FW.API
dotnet run --launch-profile http --urls http://0.0.0.0:5010

# Terminal khác
cd FW.Frontend/mobile
cp .env.example .env.local      # sửa IP thành IP LAN của máy chạy backend (ipconfig)
npm install
npx expo start                  # quét QR bằng Expo Go; nhấn "w" để xem thử trên trình duyệt
```

| Thiết bị | `EXPO_PUBLIC_API_BASE_URL` |
| --- | --- |
| Điện thoại thật (cùng Wi-Fi với máy tính) | `http://<IP LAN>:5010/api` — mở cổng 5010 trên Windows Firewall nếu bị chặn |
| Android Emulator | `http://10.0.2.2:5010/api` |
| Trình duyệt (`w`) — chỉ để xem giao diện | `http://localhost:5010/api` (đã cho phép CORS `localhost:8081` trong cấu hình dev) |

Đã có (Task 1.7): đăng nhập, quên mật khẩu bằng OTP, bắt buộc đổi mật khẩu tạm, phiên lưu trong SecureStore (Keychain/Keystore) và tự đồng bộ khi mở lại app, khung 5 tab, hồ sơ cá nhân (ảnh đại diện, số điện thoại).

> Đọc Wi-Fi BSSID và chấm công cần **development build** (`npx expo run:android` hoặc `eas build --profile development`), không chạy đầy đủ trên Expo Go.
> iOS cần quyền "Access Wi-Fi Information" (đã khai báo trong `app.json`) và tài khoản Apple Developer.

### Toàn bộ hệ thống bằng Docker

```bash
docker compose up -d --build
```

## Bảo mật cấu hình

Không commit thông tin bí mật. Các file sau đã nằm trong `.gitignore`:
`.env`, `.env.local`, `appsettings.Development.json`, `appsettings.Production.json`.
Khi triển khai (Render), đặt biến môi trường theo dạng `JwtSettings__Secret`, `ConnectionStrings__DefaultConnection`, `Cloudinary__ApiSecret`...
