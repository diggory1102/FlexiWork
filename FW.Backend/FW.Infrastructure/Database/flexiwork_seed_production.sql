-- =============================================================================
-- SEED TỐI THIỂU CHO MÔI TRƯỜNG TRIỂN KHAI (Aiven / cloud)
-- Chỉ gồm danh mục hệ thống bắt buộc. KHÔNG có tài khoản người dùng:
-- Quản trị viên đầu tiên được backend tự tạo khi khởi động từ biến môi trường
--   Bootstrap__AdminEmail / Bootstrap__AdminPassword
-- (tránh để lộ mật khẩu mẫu "Admin@123" của flexiwork_seed.sql trong repo công khai).
-- Chạy SAU flexiwork_schema.sql. An toàn khi chạy lại (ON DUPLICATE KEY UPDATE).
-- =============================================================================

USE flexiwork_db;

-- Vai trò hệ thống (RBAC) — tên khớp FW.Domain.Enums.RoleNames
INSERT INTO roles (id, role_name, description) VALUES
(1, 'ROLE_ADMIN', 'Quản trị viên toàn hệ thống, toàn quyền cấu hình'),
(2, 'ROLE_MANAGER', 'Quản lý chi nhánh / Trưởng bộ phận, duyệt đơn và phân ca'),
(3, 'ROLE_EMPLOYEE', 'Nhân viên ca kíp, thực hiện chấm công và báo cáo công việc'),
(4, 'ROLE_HR', 'Bộ phận Nhân sự, quản lý hồ sơ, đơn từ và bảng công toàn doanh nghiệp')
ON DUPLICATE KEY UPDATE role_name = VALUES(role_name), description = VALUES(description);

-- Loại đơn từ mặc định (HR có thể sửa trên Web Admin ở Cấp 4)
INSERT INTO leave_types (id, name, is_paid, requires_attachment) VALUES
(1, 'Nghỉ phép năm (Annual Leave)', 1, 0),
(2, 'Nghỉ ốm / Khám chữa bệnh', 1, 1),
(3, 'Nghỉ việc riêng không hưởng lương', 0, 0),
(4, 'Đơn xin đổi ca làm việc', 1, 0),
(5, 'Đơn xin làm thêm giờ (OT)', 1, 0)
ON DUPLICATE KEY UPDATE name = VALUES(name);
