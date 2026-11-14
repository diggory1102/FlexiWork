-- =====================================================================
-- Migration 003 — Task 2.2: quy tắc vi phạm giờ giấc & tham số hệ thống
--   violation_rules : bậc trừ công / phạt tiền khi đi muộn, về sớm (toàn công ty)
--   system_settings : tham số dạng khóa - giá trị (số công chuẩn tháng...)
-- Kèm dữ liệu mặc định. Chạy MỘT lần trên CSDL đã khởi tạo trước đó — xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

CREATE TABLE IF NOT EXISTS violation_rules (
    id INT AUTO_INCREMENT PRIMARY KEY,
    violation_type VARCHAR(20) NOT NULL COMMENT 'late, early_leave',
    min_minutes INT NOT NULL COMMENT 'Áp dụng khi số phút vi phạm từ mức này trở lên (chọn bậc cao nhất thỏa mãn)',
    deduct_work_units DECIMAL(4, 2) NOT NULL DEFAULT 0.00 COMMENT 'Số công bị trừ',
    penalty_amount DECIMAL(12, 0) NOT NULL DEFAULT 0 COMMENT 'Tiền phạt (VNĐ)',
    description VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_violation_tier (violation_type, min_minutes)
) ENGINE=InnoDB COMMENT='Bậc trừ công / phạt tiền khi đi muộn, về sớm';

CREATE TABLE IF NOT EXISTS system_settings (
    setting_key VARCHAR(50) PRIMARY KEY,
    setting_value VARCHAR(255) NOT NULL,
    description VARCHAR(255) NULL,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB COMMENT='Tham số cấu hình toàn hệ thống (VD: standard_work_days = số công chuẩn tháng)';

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
('standard_work_days', '26', 'Số công chuẩn trong tháng (dùng tính lương nhân viên toàn thời gian)')
ON DUPLICATE KEY UPDATE setting_key = setting_key;
