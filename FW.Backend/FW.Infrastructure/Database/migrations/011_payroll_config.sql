-- =====================================================================
-- Migration 011 — Task 5.4: cấu hình lương (UC 8.1) + bảng kỳ lương / phiếu lương cho Task 5.5
--   users             : base_salary (lương cơ bản tháng), hourly_rate (đơn giá giờ riêng, bán thời gian)
--   allowances        : danh mục phụ cấp (cố định tháng / mỗi ca đêm / mỗi ngày ca gãy)
--   user_allowances   : phụ cấp gán riêng cho nhân viên
--   payroll_periods, payslips, payslip_items : kỳ lương và phiếu lương (Task 5.5 dùng)
--   Dữ liệu mặc định: 3 phụ cấp mẫu, hệ số OT 1,5 (HR sửa được trên Web Admin)
--
-- MỖI CÂU ALTER CHỈ LÀM MỘT VIỆC (TiDB không cho một câu vừa thêm cột vừa tham chiếu cột đó).
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra đã có chưa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'users' AND column_name = 'base_salary') = 0,
    'ALTER TABLE users ADD COLUMN base_salary DECIMAL(14, 0) NULL COMMENT ''Lương cơ bản tháng (toàn thời gian); NULL = chưa cấu hình'' AFTER hire_date',
    'SELECT ''users.base_salary: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'users' AND column_name = 'hourly_rate') = 0,
    'ALTER TABLE users ADD COLUMN hourly_rate DECIMAL(12, 2) NULL COMMENT ''Đơn giá giờ riêng (bán thời gian); NULL = theo chức vụ'' AFTER base_salary',
    'SELECT ''users.hourly_rate: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

CREATE TABLE IF NOT EXISTS allowances (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    basis VARCHAR(20) NOT NULL DEFAULT 'monthly' COMMENT 'Cách tính: monthly (cố định tháng), night_shift (mỗi ca đêm), split_day (mỗi ngày ca gãy)',
    amount DECIMAL(12, 0) NOT NULL DEFAULT 0 COMMENT 'Mức tiền mặc định (VNĐ) cho một đơn vị của basis',
    apply_to_all TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: mọi nhân viên đều được hưởng; 0: chỉ người được gán',
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    description VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB COMMENT='Danh mục phụ cấp do HR cấu hình';

CREATE TABLE IF NOT EXISTS user_allowances (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    allowance_id INT NOT NULL,
    amount DECIMAL(12, 0) NULL COMMENT 'Mức tiền riêng; NULL = dùng mức của danh mục',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_user_allowance (user_id, allowance_id),
    CONSTRAINT fk_user_allowance_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_user_allowance_allowance FOREIGN KEY (allowance_id) REFERENCES allowances(id) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='Phụ cấp gán riêng cho từng nhân viên (có thể ghi đè mức tiền)';

CREATE TABLE IF NOT EXISTS payroll_periods (
    id INT AUTO_INCREMENT PRIMARY KEY,
    month TINYINT NOT NULL,
    year SMALLINT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'draft' COMMENT 'draft: tính lại được; finalized: đã chốt',
    standard_work_days DECIMAL(5, 2) NOT NULL DEFAULT 26.00 COMMENT 'Số công chuẩn tại lúc tính',
    overtime_multiplier DECIMAL(4, 2) NOT NULL DEFAULT 1.50 COMMENT 'Hệ số OT tại lúc tính',
    employee_count INT NOT NULL DEFAULT 0,
    total_net DECIMAL(16, 0) NOT NULL DEFAULT 0 COMMENT 'Tổng thực nhận của kỳ',
    calculated_at DATETIME NULL,
    created_by INT NULL,
    finalized_by INT NULL,
    finalized_at DATETIME NULL,
    note VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_payroll_period (year, month),
    CONSTRAINT fk_period_created_by FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT fk_period_finalized_by FOREIGN KEY (finalized_by) REFERENCES users(id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Kỳ lương tháng';

CREATE TABLE IF NOT EXISTS payslips (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    period_id INT NOT NULL,
    user_id INT NOT NULL,
    branch_id INT NULL,
    timesheet_id BIGINT NULL,
    employment_type VARCHAR(20) NOT NULL DEFAULT 'full_time',
    base_salary DECIMAL(14, 0) NOT NULL DEFAULT 0 COMMENT 'Lương cơ bản tháng tại lúc tính',
    hourly_rate DECIMAL(12, 2) NOT NULL DEFAULT 0 COMMENT 'Đơn giá giờ tại lúc tính',
    standard_work_days DECIMAL(5, 2) NOT NULL DEFAULT 0,
    work_days DECIMAL(6, 2) NOT NULL DEFAULT 0,
    work_hours DECIMAL(7, 2) NOT NULL DEFAULT 0,
    overtime_hours DECIMAL(7, 2) NOT NULL DEFAULT 0,
    basic_pay DECIMAL(14, 0) NOT NULL DEFAULT 0 COMMENT 'Lương theo công thực tế',
    allowance_total DECIMAL(14, 0) NOT NULL DEFAULT 0,
    overtime_pay DECIMAL(14, 0) NOT NULL DEFAULT 0,
    penalty_total DECIMAL(14, 0) NOT NULL DEFAULT 0 COMMENT 'Tổng tiền phạt (số dương)',
    net_pay DECIMAL(14, 0) NOT NULL DEFAULT 0 COMMENT 'Thực nhận = lương + phụ cấp + OT - phạt',
    status VARCHAR(20) NOT NULL DEFAULT 'draft' COMMENT 'draft: chưa gửi; sent: đã gửi nhân viên',
    sent_at DATETIME NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_payslip_period_user (period_id, user_id),
    CONSTRAINT fk_payslip_period FOREIGN KEY (period_id) REFERENCES payroll_periods(id) ON DELETE CASCADE,
    CONSTRAINT fk_payslip_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_payslip_branch FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE SET NULL,
    CONSTRAINT fk_payslip_timesheet FOREIGN KEY (timesheet_id) REFERENCES monthly_timesheets(id) ON DELETE SET NULL,
    INDEX idx_payslip_user (user_id, period_id)
) ENGINE=InnoDB COMMENT='Phiếu lương của một nhân viên trong kỳ (ảnh chụp số liệu lúc tính)';

CREATE TABLE IF NOT EXISTS payslip_items (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    payslip_id BIGINT NOT NULL,
    kind VARCHAR(20) NOT NULL COMMENT 'basic, allowance, overtime, penalty, adjustment',
    name VARCHAR(100) NOT NULL,
    quantity DECIMAL(10, 2) NOT NULL DEFAULT 1,
    unit_amount DECIMAL(14, 2) NOT NULL DEFAULT 0,
    amount DECIMAL(14, 0) NOT NULL DEFAULT 0 COMMENT 'Thành tiền; khoản trừ là số âm',
    note VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_payslip_item_payslip FOREIGN KEY (payslip_id) REFERENCES payslips(id) ON DELETE CASCADE,
    INDEX idx_payslip_items (payslip_id, kind)
) ENGINE=InnoDB COMMENT='Từng khoản trên phiếu lương';

-- ---------------------------------------------------------------- dữ liệu mặc định (chỉ thêm khi chưa có)
INSERT INTO allowances (name, basis, amount, apply_to_all, description)
SELECT 'Phụ cấp ca đêm', 'night_shift', 30000, 1, 'Mỗi ca đêm đã làm'
WHERE NOT EXISTS (SELECT 1 FROM allowances WHERE name = 'Phụ cấp ca đêm');

INSERT INTO allowances (name, basis, amount, apply_to_all, description)
SELECT 'Phụ cấp ca gãy', 'split_day', 20000, 1, 'Mỗi ngày làm ca gãy'
WHERE NOT EXISTS (SELECT 1 FROM allowances WHERE name = 'Phụ cấp ca gãy');

INSERT INTO allowances (name, basis, amount, apply_to_all, description)
SELECT 'Phụ cấp ăn trưa', 'monthly', 500000, 0, 'Cố định theo tháng, gán riêng cho từng nhân viên'
WHERE NOT EXISTS (SELECT 1 FROM allowances WHERE name = 'Phụ cấp ăn trưa');

INSERT INTO system_settings (setting_key, setting_value, description)
VALUES ('overtime_multiplier', '1.5', 'Hệ số tiền làm thêm giờ (OT) so với đơn giá giờ')
ON DUPLICATE KEY UPDATE setting_key = setting_key;

-- ---------------------------------------------------------------- kiểm tra
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'users' AND column_name IN ('base_salary', 'hourly_rate')) AS so_cot_users_2,
    (SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema = DATABASE()
       AND table_name IN ('allowances', 'user_allowances', 'payroll_periods', 'payslips', 'payslip_items')) AS so_bang_moi_5,
    (SELECT COUNT(*) FROM allowances WHERE is_deleted = 0) AS so_phu_cap,
    (SELECT COUNT(*) FROM system_settings WHERE setting_key = 'overtime_multiplier') AS co_he_so_ot_1;
