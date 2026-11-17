-- =====================================================================
-- Migration 010 — Task 5.3: bảng công tháng (monthly_timesheets)
--   Thiết kế lại bảng: thêm chi nhánh, số ca (đã làm / vắng / chờ duyệt / bị từ chối / thiếu check-out), giờ làm thực tế,
--   vi phạm (số lượt, công bị trừ, tiền phạt), ca đêm, ngày làm ca gãy, thời điểm tính, người xác nhận / khóa;
--   trạng thái draft → confirmed (Quản lý xác nhận) → locked (HR khóa) → finalized (đã chốt lương, Task 5.5);
--   nới độ rộng các cột số (TiDB không cho đổi độ rộng DECIMAL bằng ALTER nên tạo lại bảng).
--   Bảng cũ chưa có chức năng nào ghi vào → đổi tên thành bảng sao lưu rồi tạo bảng mới (như migration 005).
--
-- CHẠY LẠI AN TOÀN: bảng đã là thiết kế mới (có cột worked_shifts) thì bỏ qua, không đụng tới dữ liệu bảng công đang có.
-- Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @timesheet_is_new = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'monthly_timesheets' AND column_name = 'worked_shifts'
);
SET @timesheet_exists = (
    SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_name = 'monthly_timesheets'
);
SET @backup_exists = (
    SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_name = 'monthly_timesheets_backup_010'
);
SET @step = IF(@timesheet_exists = 1 AND @timesheet_is_new = 0 AND @backup_exists = 0,
    'RENAME TABLE monthly_timesheets TO monthly_timesheets_backup_010',
    'SELECT ''Bỏ qua bước đổi tên: bảng công đã là thiết kế mới hoặc đã có bảng sao lưu'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

CREATE TABLE IF NOT EXISTS monthly_timesheets (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    branch_id INT NULL COMMENT 'Chi nhánh của nhân viên lúc tính — phạm vi xác nhận của Quản lý',
    month INT NOT NULL COMMENT 'Tháng (1 - 12)',
    year INT NOT NULL COMMENT 'Năm (YYYY)',
    standard_work_days DECIMAL(5, 2) NOT NULL DEFAULT 26.00 COMMENT 'Số ngày công tiêu chuẩn của tháng (system_settings)',

    scheduled_shifts INT NOT NULL DEFAULT 0 COMMENT 'Số ca đã công bố trong tháng',
    worked_shifts INT NOT NULL DEFAULT 0 COMMENT 'Số ca đã làm (check-in + check-out hợp lệ)',
    absent_shifts INT NOT NULL DEFAULT 0 COMMENT 'Số ca đã qua mà không chấm công',
    pending_logs INT NOT NULL DEFAULT 0 COMMENT 'Số lượt chấm công còn chờ duyệt (chưa tính công, chặn xác nhận)',
    rejected_logs INT NOT NULL DEFAULT 0 COMMENT 'Số lượt chấm công bị từ chối (không tính công)',
    missing_checkouts INT NOT NULL DEFAULT 0 COMMENT 'Số ca có check-in nhưng quá hạn mà chưa check-out (không tính công)',

    actual_work_days DECIMAL(6, 2) NOT NULL DEFAULT 0.00 COMMENT 'Ngày công thực tế = tổng hệ số công các ca đã làm, đã trừ công vi phạm',
    actual_work_hours DECIMAL(7, 2) NOT NULL DEFAULT 0.00 COMMENT 'Tổng giờ làm thực tế',
    total_late_minutes INT NOT NULL DEFAULT 0 COMMENT 'Tổng phút đi muộn vượt mức cho phép (đã trừ lượt được giải trình)',
    total_early_minutes INT NOT NULL DEFAULT 0 COMMENT 'Tổng phút về sớm vượt mức cho phép',
    violation_count INT NOT NULL DEFAULT 0 COMMENT 'Số lượt chấm công vi phạm giờ giấc',
    deducted_work_days DECIMAL(6, 2) NOT NULL DEFAULT 0.00 COMMENT 'Số công bị trừ do vi phạm',
    penalty_amount DECIMAL(12, 0) NOT NULL DEFAULT 0 COMMENT 'Tổng tiền phạt vi phạm (VNĐ)',
    total_overtime_hours DECIMAL(6, 2) NOT NULL DEFAULT 0.00 COMMENT 'Tổng giờ làm thêm OT (đơn đã duyệt)',
    paid_leave_days DECIMAL(6, 2) NOT NULL DEFAULT 0.00 COMMENT 'Số công nghỉ có lương (hệ số công của ca nghỉ)',
    unpaid_leave_days DECIMAL(6, 2) NOT NULL DEFAULT 0.00 COMMENT 'Số công nghỉ không lương',
    night_shifts INT NOT NULL DEFAULT 0 COMMENT 'Số ca đêm đã làm (phụ cấp ca đêm)',
    split_shift_days INT NOT NULL DEFAULT 0 COMMENT 'Số ngày làm ca gãy: 2 ca trở lên trong ngày không nối tiếp nhau (phụ cấp ca gãy)',
    calculated_salary DECIMAL(14, 2) NOT NULL DEFAULT 0.00 COMMENT 'Lương quy đổi tạm tính (Task 5.5)',

    status VARCHAR(20) NOT NULL DEFAULT 'draft' COMMENT 'draft: hệ thống tính, confirmed: Quản lý xác nhận, locked: HR khóa, finalized: đã chốt lương',
    calculated_at DATETIME NULL COMMENT 'Lần tính gần nhất',
    confirmed_by INT NULL,
    confirmed_at DATETIME NULL,
    locked_by INT NULL,
    locked_at DATETIME NULL,
    note VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,

    UNIQUE KEY uq_user_month_year (user_id, month, year),
    CONSTRAINT fk_timesheet_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_timesheet_branch FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE SET NULL,
    CONSTRAINT fk_timesheet_confirmed_by FOREIGN KEY (confirmed_by) REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT fk_timesheet_locked_by FOREIGN KEY (locked_by) REFERENCES users(id) ON DELETE SET NULL,
    INDEX idx_timesheet_month (year, month, status),
    INDEX idx_timesheet_branch_month (branch_id, year, month)
) ENGINE=InnoDB COMMENT='Bảng tổng hợp công tháng phục vụ chốt lương';

-- ---------------------------------------------------------------- kiểm tra
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'monthly_timesheets' AND column_name = 'worked_shifts') AS co_cot_worked_shifts_1,
    (SELECT COUNT(*) FROM monthly_timesheets) AS so_dong_bang_cong,
    (SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema = DATABASE() AND table_name = 'monthly_timesheets_backup_010') AS con_bang_sao_luu;

-- Khi con_bang_sao_luu = 1: kiểm tra bảng sao lưu rỗng rồi xóa
--   SELECT COUNT(*) FROM monthly_timesheets_backup_010;   -- phải bằng 0
--   DROP TABLE monthly_timesheets_backup_010;
