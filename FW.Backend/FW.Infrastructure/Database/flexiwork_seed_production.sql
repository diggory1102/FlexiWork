-- =============================================================================
-- SEED TỐI THIỂU CHO MÔI TRƯỜNG TRIỂN KHAI (TiDB Cloud / cloud)
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
INSERT INTO leave_types (id, name, category, is_paid, deducts_balance, requires_attachment, description) VALUES
(1, 'Nghỉ phép năm (Annual Leave)', 'leave', 1, 1, 0, 'Có hưởng lương, trừ vào quỹ phép năm'),
(2, 'Nghỉ ốm / Khám chữa bệnh', 'leave', 1, 0, 1, 'Cần ảnh giấy khám / đơn thuốc'),
(3, 'Nghỉ việc riêng không hưởng lương', 'leave', 0, 0, 0, NULL),
(4, 'Đơn xin đổi ca làm việc', 'shift_swap', 1, 0, 0, 'Đổi ca với đồng nghiệp hoặc nhờ đồng nghiệp làm thay — đồng nghiệp phải đồng ý trước'),
(5, 'Đơn xin làm thêm giờ (OT)', 'overtime', 1, 0, 0, NULL),
(6, 'Giải trình đi muộn / về sớm', 'explanation', 1, 0, 0, 'Giải trình cho một lượt chấm công đi muộn, về sớm hoặc đang chờ duyệt')
ON DUPLICATE KEY UPDATE name = VALUES(name), category = VALUES(category), deducts_balance = VALUES(deducts_balance);

-- Quy tắc vi phạm & số công chuẩn mặc định (HR sửa được trên Web Admin)
INSERT INTO violation_rules (violation_type, min_minutes, deduct_work_units, penalty_amount, description) VALUES
('late', 1, 0.00, 20000, 'Đi muộn quá số phút cho phép của ca'),
('late', 15, 0.00, 50000, 'Đi muộn từ 15 phút'),
('late', 60, 0.50, 50000, 'Đi muộn từ 60 phút: trừ nửa công'),
('early_leave', 1, 0.00, 20000, 'Về sớm quá số phút cho phép của ca'),
('early_leave', 15, 0.00, 50000, 'Về sớm từ 15 phút'),
('early_leave', 60, 0.50, 50000, 'Về sớm từ 60 phút: trừ nửa công')
ON DUPLICATE KEY UPDATE description = description;

INSERT INTO system_settings (setting_key, setting_value, description) VALUES
('standard_work_days', '26', 'Số công chuẩn trong tháng (dùng tính lương nhân viên toàn thời gian)'),
('annual_leave_days', '12', 'Số ngày phép năm mặc định của mỗi nhân viên (khi chưa khởi tạo quỹ phép riêng)')
ON DUPLICATE KEY UPDATE setting_key = setting_key;
