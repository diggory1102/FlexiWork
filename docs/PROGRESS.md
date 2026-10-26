# FlexiWork - Theo dõi tiến độ dự án (AI Context)

> **Lưu ý quan trọng:** File này đóng vai trò là "bộ nhớ dài hạn" của dự án. Bất cứ khi nào bắt đầu làm việc, AI Agent hãy đọc file này trước tiên để nắm bối cảnh, hiểu những gì đã làm và biết cần làm gì tiếp theo.

## 1. Bối cảnh dự án (Project Context)
- **Dự án:** FlexiWork
- **Mô tả:** Hệ thống phần mềm quản lý lịch trình làm việc, nhân sự, điểm danh, tính lương, công việc và giao tiếp nội bộ.
- **Kiến trúc:** Clean Architecture (Chia thành các lớp: Domain, Application, Infrastructure, API).
- **Tech Stack:** C# .NET 8, Entity Framework Core.

## 2. Đã hoàn thành (Done)
- [x] Khởi tạo Solution và cấu trúc project Clean Architecture (.NET 8).
- [x] **Database & ERD Design**:
  - [x] Chuẩn hóa thiết kế ERD 3NF cho 18 thực thể cốt lõi.
  - [x] Tạo file kịch bản DDL MySQL 8.0 hoàn chỉnh: `docs/database/flexiwork_schema.sql` (hỗ trợ Geofencing, Wi-Fi BSSID, Face ID vector JSON, Ca đêm qua ngày, Soft Delete toàn diện, Indexing tối ưu).
  - [x] Tạo file kịch bản Seed Data mẫu: `docs/database/flexiwork_seed.sql` (Phòng ban, Chức vụ, Chi nhánh kèm tọa độ/Wi-Fi, Tài khoản Admin/Manager/Employee, Khung ca, Loại đơn, Quỹ phép, Checklist ca).
- [x] **App.Domain Synchronization**:
  - [x] Nâng cấp `BaseEntity` hỗ trợ khóa chính generic `BaseEntity<TKey>` (`int` và `long/bigint`), tích hợp `ISoftDeletable` và `IAuditableEntity`.
  - [x] Đồng bộ đầy đủ 18 Entities tương ứng 1:1 với CSDL: `Branch`, `BranchWifi`, `Department`, `Position`, `User`, `Role`, `UserRole`, `FaceProfile`, `Shift`, `Schedule`, `AttendanceLog`, `LeaveType`, `LeaveRequest`, `LeaveBalance`, `ShiftTask`, `ShiftTaskAssignment`, `Conversation`, `ConversationMember`, `Message`, `Notification`, `MonthlyTimesheet`.
  - [x] Định nghĩa chuẩn hóa các Enums trạng thái: `BranchStatus`, `UserStatus`, `ScheduleStatus`, `AttendanceStatus`, `LeaveRequestStatus`, `ConversationType`, `NotificationType`, `TimesheetStatus`.
  - [x] Cập nhật toàn bộ `AppDbContext` (18 `DbSet`, Fluent API quan hệ, precision, index, khóa ngoại) và biên dịch Solution thành công 100% (0 lỗi, 0 warning).
- [x] **App.Infrastructure (Tích hợp Micro-ORM Dapper Toàn diện)**:
  - [x] Cài đặt package `Dapper` (v2.1.35) và `MySqlConnector` (v2.3.7).
  - [x] Xây dựng `IDbConnectionFactory` & `MySqlConnectionFactory` cho pool kết nối MySQL tốc độ cao.
  - [x] Xây dựng `JsonTypeHandler<T>` hỗ trợ map tự động vector Face ID `float[]` và JSON objects trong MySQL.
  - [x] Triển khai `IBranchRepository` & `BranchRepository`: Lấy chi nhánh, tọa độ Geofence và danh sách BSSID Wi-Fi.
  - [x] Triển khai `IUserRepository` & `UserRepository`: Tra cứu nhân viên, phân quyền RBAC và đối soát vector Face ID.
  - [x] Triển khai `IScheduleRepository` & `ScheduleRepository`: Phân ca, kiểm tra xung đột trùng ca, truy vấn lịch làm việc theo chi nhánh/ngày với Dapper multi-mapping.
  - [x] Triển khai `IAttendanceRepository` & `AttendanceRepository`: Pipeline Check-in/Check-out đa lớp toàn diện (GPS + Wi-Fi + Face ID), đối soát giờ công.
- [x] **App.Api Integration**:
  - [x] Cấu hình chuỗi kết nối MySQL `flexiwork_db` trong `appsettings.json`.
  - [x] Đăng ký đầy đủ DI cho `AppDbContext`, `IDbConnectionFactory`, `IBranchRepository`, `IUserRepository`, `IScheduleRepository`, `IAttendanceRepository` trong `Program.cs`.
  - [x] Đăng ký tự động `JsonTypeHandler<float[]>` cho Dapper khi khởi chạy ứng dụng.
  - [x] Tạo `BranchesController` kiểm chứng luồng đọc dữ liệu chi nhánh và Wi-Fi qua Dapper.
  - [x] Biên dịch toàn bộ Solution thành công 100% (`0 Warning, 0 Error`).
- [x] **AI Face Recognition Microservice (Python FastAPI + InsightFace ArcFace 512D)**:
  - [x] Tạo service độc lập tại `App/AiService`: `requirements.txt` (FastAPI, InsightFace, ONNXRuntime, OpenCV).
  - [x] Viết `FaceEngine`: trích xuất vector đặc trưng 512 chiều (L2-normalized) và tính Cosine Similarity (kèm cơ chế Dev Fallback an toàn).
  - [x] Cung cấp 2 API RESTful: `POST /api/face/extract` (đăng ký khuôn mặt) và `POST /api/face/verify` (đối soát chấm công).
  - [x] Tạo `Dockerfile` đóng gói container độc lập.
  - [x] Xây dựng `IFaceRecognitionService` & `FaceRecognitionService` (C# .NET 8) kết nối qua `HttpClient` với AI Service.
  - [x] Đăng ký HttpClient trong `Program.cs` và cấu hình BaseUrl trong `appsettings.json`.
- [x] **Frontend Web (React 18 + TypeScript + Vite + Tailwind CSS + Zustand)**:
  - [x] Chuyển đổi và chuẩn hóa cấu trúc thư mục sang **Feature-based Modules**:
    - `src/features/{feature_name}`: Độc lập theo nghiệp vụ HRM (Dashboard, Attendance, Shifts, Employees, Payroll, Leaves).
    - `src/layouts`: `MainLayout`, `Header`, `Sidebar` chuẩn iPOS.vn.
    - `src/stores` & `src/types`: Global session store & Shared entities.
  - [x] Thiết lập Tailwind CSS, Google Font Inter, custom scrollbar.
  - [x] Xây dựng Axios `apiClient` tập trung và Zustand Store phân tách theo Feature.
  - [x] Tái hiện 1:1 giao diện **Web Quản trị Nhân sự HRM phong cách iPOS.vn** (`DashboardView.tsx`):
    - Top Header xanh đậm (`#202538`) với logo HRM, nút Bảng giá, Hỗ trợ, Thông báo badge.
    - Sidebar chuẩn 210px với đầy đủ danh mục HRM (Tổng quan, Chờ duyệt, Nhân viên, Ca kíp, Chấm công, Lương...).
    - Hero Card tổng ca làm việc kèm vector nghệ thuật nhân viên F&B.
    - 8 thẻ KPI chỉ số chấm công: Đi muộn, Về sớm, Quên check-in/out, Nghỉ phép, Trùng thiết bị...
    - Banner tổng lương ngày dự kiến & lương thực tế thời gian thực.
    - Bảng phân ca hôm nay liên kết trực tiếp dữ liệu Seed (Ca sáng, Ca chiều, Ca đêm).
  - [x] Đã build thành công bản production (`vite build`) và đang chạy live server tại `http://localhost:5173`.
  - [x] Commit và push toàn bộ mã nguồn lên GitHub (`origin/main`, commit `6adb1bf`).
- [x] **Frontend Mobile (React Native / Expo - Employee App)**:
  - [x] Chuẩn hóa toàn diện cấu trúc thư mục sang **Feature-based Modules** hướng nghiệp vụ nhân viên:
    - `src/features/attendance`: Chấm công thông minh (Camera Face ID, GPS Geofence, Wi-Fi AP).
    - `src/features/shifts`: Lịch ca cá nhân, đổi ca trực.
    - `src/features/requests`: Đơn xin nghỉ phép, giải trình chấm công, làm thêm giờ.
    - `src/features/profile`: Hồ sơ cá nhân & ví tích lũy ngày công.
    - `src/features/notifications`: Thông báo nhắc ca và kết quả duyệt đơn.
    - `src/stores` & `src/services` & `src/types`: Quản lý Auth session, API client và Types chung.
  - [x] Dọn dẹp sạch sẽ 4 thư mục MVVM rỗng cũ (`Models`, `ViewModels`, `Views`, `Services`).

## 3. Đang thực hiện (WIP - Work in Progress)
- [ ] **Tính năng 1: Core Attendance Engine (Chấm công Đa lớp)**:
  - Xây dựng thuật toán Geofencing (Haversine Formula) tính khoảng cách mét giữa điện thoại và tâm chi nhánh.
  - Xây dựng dịch vụ so khớp Access Point Wi-Fi (BSSID).
  - Xây dựng Use Case Check-in / Check-out trong `App.Application`: Kết hợp kết quả từ Geofence + Wi-Fi + Face ID $\rightarrow$ tự động xác định trạng thái (`valid`, `pending_approval`, `invalid`).
  - Viết `AttendanceController` trên Backend để tiếp nhận request chấm công thực tế.
  - Đấu nối dữ liệu thật từ Backend lên Web Dashboard.

## 4. Kế hoạch tiếp theo (Next Steps / To-do)
- [ ] **Tính năng 2: Quản lý Phân ca & Lịch làm việc (Shifts & Schedules)**.
- [ ] **Tính năng 3: Quản lý Đơn từ & Phê duyệt (Leave Requests)**.
- [ ] **Tính năng 4: Tổng hợp Bảng công & Chốt lương tháng (Monthly Timesheets)**.
- [ ] Cấu hình bảo mật JWT Token Authentication & Phân quyền Role-based (Admin, Store Manager, Employee).
