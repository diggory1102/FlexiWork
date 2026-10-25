# FlexiWork - Theo dõi tiến độ dự án (AI Context)

> **Lưu ý quan trọng:** File này đóng vai trò là "bộ nhớ dài hạn" của dự án. Bất cứ khi nào bắt đầu làm việc, AI Agent hãy đọc file này trước tiên để nắm bối cảnh, hiểu những gì đã làm và biết cần làm gì tiếp theo.

## 1. Bối cảnh dự án (Project Context)
- **Dự án:** FlexiWork
- **Mô tả:** Hệ thống phần mềm quản lý lịch trình làm việc, nhân sự, điểm danh, tính lương, công việc và giao tiếp nội bộ.
- **Kiến trúc:** Clean Architecture (Chia thành các lớp: Domain, Application, Infrastructure, API).
- **Tech Stack:** C# .NET 9, Entity Framework Core.

## 2. Đã hoàn thành (Done)
- [x] Khởi tạo Solution và cấu trúc project Clean Architecture (.NET 9 / .NET 8).
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

## 3. Đang thực hiện (WIP - Work in Progress)
- [ ] **App.Application (Use Cases & Core Attendance Engine)**:
  - Xây dựng thuật toán kiểm tra Geofence (Haversine Formula) tính khoảng cách mét giữa thiết bị và chi nhánh.
  - Xây dựng dịch vụ so khớp Access Point Wi-Fi (BSSID).
  - Xây dựng dịch vụ so khớp vector Face ID (Cosine Similarity / Euclidean Distance).
  - Hoàn thiện luồng Use Case Check-in / Check-out tự động phân loại trạng thái (`valid`, `pending_approval`, `invalid`).

## 4. Kế hoạch tiếp theo (Next Steps / To-do)
- [ ] **App.Api**:
  - Cấu hình JWT Authentication & Authorization (Token Service, Claims).
  - Viết các Controller: `AuthController`, `SchedulesController`, `AttendanceController`.
