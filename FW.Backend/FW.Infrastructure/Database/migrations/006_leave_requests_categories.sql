-- =====================================================================
-- Migration 006 — Task 4.1: đơn từ (nghỉ, đổi ca, OT, giải trình)
--   leave_types    : thêm nhóm đơn (category), trừ quỹ phép, mô tả, bật/tắt, thời gian tạo/sửa
--                    + loại "Giải trình đi muộn / về sớm"
--   leave_requests : thêm chi nhánh xử lý, giờ bắt đầu/kết thúc + số giờ OT, lượt chấm công được giải trình,
--                    phản hồi của đồng nghiệp khi đổi ca, mã tệp đính kèm, thời gian sửa
--   system_settings: annual_leave_days (số ngày phép năm mặc định)
--
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra cột / khóa ngoại / dữ liệu đã có chưa rồi mới chạy — đã chạy rồi thì bỏ qua,
-- không ghi đè cấu hình HR đã sửa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

-- ---------------------------------------------------------------- leave_types
SET @types_has_category = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND column_name = 'category'
);
SET @step = IF(@types_has_category = 0,
    'ALTER TABLE leave_types
        ADD COLUMN category VARCHAR(20) NOT NULL DEFAULT ''leave'' COMMENT ''leave, shift_swap, overtime, explanation'' AFTER name,
        ADD COLUMN description VARCHAR(255) NULL AFTER category,
        ADD COLUMN deducts_balance TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''1: Trừ quỹ phép năm khi được duyệt'' AFTER is_paid,
        ADD COLUMN is_active TINYINT(1) NOT NULL DEFAULT 1 COMMENT ''0: Ngừng nhận đơn mới loại này'' AFTER requires_attachment,
        ADD COLUMN created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
        ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
        ADD INDEX idx_leave_type_category (category)',
    'SELECT ''leave_types: đã có cột category — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- Gán nhóm cho các loại đơn mặc định — chỉ ở lần chạy đầu (vừa thêm cột), để không ghi đè cấu hình HR sửa sau này
SET @step = IF(@types_has_category = 0,
    'UPDATE leave_types SET
        category = CASE
            WHEN id = 4 AND name = ''Đơn xin đổi ca làm việc'' THEN ''shift_swap''
            WHEN id = 5 AND name = ''Đơn xin làm thêm giờ (OT)'' THEN ''overtime''
            ELSE category END,
        deducts_balance = IF(id = 1 AND name = ''Nghỉ phép năm (Annual Leave)'', 1, deducts_balance)',
    'SELECT ''leave_types: đã gán nhóm trước đó — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

INSERT INTO leave_types (name, category, is_paid, deducts_balance, requires_attachment, description)
SELECT 'Giải trình đi muộn / về sớm', 'explanation', 1, 0, 0, 'Giải trình cho một lượt chấm công đi muộn, về sớm hoặc đang chờ duyệt'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE category = 'explanation');

-- ---------------------------------------------------------------- leave_requests
SET @requests_has_start_at = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'start_at'
);
SET @step = IF(@requests_has_start_at = 0,
    'ALTER TABLE leave_requests
        ADD COLUMN branch_id INT NULL COMMENT ''Chi nhánh xử lý đơn'' AFTER user_id,
        MODIFY COLUMN duration_days DECIMAL(3, 1) NOT NULL DEFAULT 0.0 COMMENT ''Số ngày nghỉ (chỉ đơn nghỉ)'',
        ADD COLUMN start_at DATETIME NULL COMMENT ''Giờ bắt đầu OT'' AFTER duration_days,
        ADD COLUMN end_at DATETIME NULL COMMENT ''Giờ kết thúc OT'' AFTER start_at,
        ADD COLUMN duration_hours DECIMAL(5, 2) NULL COMMENT ''Số giờ OT'' AFTER end_at,
        ADD COLUMN peer_responded_at DATETIME NULL AFTER target_schedule_id,
        ADD COLUMN peer_note VARCHAR(255) NULL AFTER peer_responded_at,
        ADD COLUMN attendance_log_id BIGINT NULL COMMENT ''Lượt chấm công được giải trình'' AFTER peer_note,
        ADD COLUMN attachment_public_id VARCHAR(255) NULL AFTER attachment_url,
        MODIFY COLUMN status VARCHAR(20) NOT NULL DEFAULT ''pending'' COMMENT ''pending_peer, pending, approved, rejected, peer_declined, cancelled'',
        ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
        ADD INDEX idx_leave_branch_status (branch_id, status),
        ADD INDEX idx_leave_target_status (target_user_id, status)',
    'SELECT ''leave_requests: đã có cột start_at — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- Khóa ngoại thêm riêng từng câu (đặt tên để kiểm tra được đã có hay chưa)
SET @step = IF((SELECT COUNT(*) FROM information_schema.table_constraints
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND constraint_name = 'fk_leave_request_branch') = 0,
    'ALTER TABLE leave_requests ADD CONSTRAINT fk_leave_request_branch FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE SET NULL',
    'SELECT ''fk_leave_request_branch: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.table_constraints
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND constraint_name = 'fk_leave_request_attendance') = 0,
    'ALTER TABLE leave_requests ADD CONSTRAINT fk_leave_request_attendance FOREIGN KEY (attendance_log_id) REFERENCES attendance_logs(id) ON DELETE SET NULL',
    'SELECT ''fk_leave_request_attendance: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- ---------------------------------------------------------------- system_settings
INSERT INTO system_settings (setting_key, setting_value, description) VALUES
('annual_leave_days', '12', 'Số ngày phép năm mặc định của mỗi nhân viên (khi chưa khởi tạo quỹ phép riêng)')
ON DUPLICATE KEY UPDATE setting_key = setting_key;

-- ---------------------------------------------------------------- kiểm tra
SELECT id, name, category, deducts_balance, is_active FROM leave_types WHERE is_deleted = 0 ORDER BY id;
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'leave_requests'
       AND column_name IN ('branch_id', 'start_at', 'end_at', 'duration_hours', 'peer_responded_at', 'peer_note',
                           'attendance_log_id', 'attachment_public_id', 'updated_at')) AS so_cot_moi_leave_requests_9,
    (SELECT COUNT(*) FROM information_schema.table_constraints
     WHERE table_schema = DATABASE() AND table_name = 'leave_requests'
       AND constraint_name IN ('fk_leave_request_branch', 'fk_leave_request_attendance')) AS so_khoa_ngoai_moi_2;
