-- =====================================================================
-- Migration 006 — Task 4.1: đơn từ (nghỉ, đổi ca, OT, giải trình)
--   leave_types    : thêm nhóm đơn (category), trừ quỹ phép, mô tả, bật/tắt, thời gian tạo/sửa
--                    + loại "Giải trình đi muộn / về sớm"
--   leave_requests : thêm chi nhánh xử lý, giờ bắt đầu/kết thúc + số giờ OT, lượt chấm công được giải trình,
--                    phản hồi của đồng nghiệp khi đổi ca, mã tệp đính kèm, thời gian sửa
--   system_settings: annual_leave_days (số ngày phép năm mặc định)
--
-- MỖI CÂU ALTER CHỈ LÀM MỘT VIỆC: TiDB không cho một câu ALTER vừa thêm cột vừa tham chiếu cột đó
-- (VD "ADD COLUMN a ..., ADD COLUMN b ... AFTER a" hay "ADD INDEX (a)") → lỗi 1054 Unknown column.
--
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra cột / chỉ mục / khóa ngoại / dữ liệu đã có chưa rồi mới chạy — kể cả khi
-- lần chạy trước dừng giữa chừng. Không ghi đè cấu hình HR đã sửa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

-- ================================================================ leave_types

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND column_name = 'category') = 0,
    'ALTER TABLE leave_types ADD COLUMN category VARCHAR(20) NOT NULL DEFAULT ''leave'' COMMENT ''leave, shift_swap, overtime, explanation'' AFTER name',
    'SELECT ''leave_types.category: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND column_name = 'description') = 0,
    'ALTER TABLE leave_types ADD COLUMN description VARCHAR(255) NULL AFTER category',
    'SELECT ''leave_types.description: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND column_name = 'deducts_balance') = 0,
    'ALTER TABLE leave_types ADD COLUMN deducts_balance TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''1: Trừ quỹ phép năm khi được duyệt'' AFTER is_paid',
    'SELECT ''leave_types.deducts_balance: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND column_name = 'is_active') = 0,
    'ALTER TABLE leave_types ADD COLUMN is_active TINYINT(1) NOT NULL DEFAULT 1 COMMENT ''0: Ngừng nhận đơn mới loại này'' AFTER requires_attachment',
    'SELECT ''leave_types.is_active: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND column_name = 'created_at') = 0,
    'ALTER TABLE leave_types ADD COLUMN created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP',
    'SELECT ''leave_types.created_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND column_name = 'updated_at') = 0,
    'ALTER TABLE leave_types ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP',
    'SELECT ''leave_types.updated_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.statistics
                WHERE table_schema = DATABASE() AND table_name = 'leave_types' AND index_name = 'idx_leave_type_category') = 0,
    'ALTER TABLE leave_types ADD INDEX idx_leave_type_category (category)',
    'SELECT ''idx_leave_type_category: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- Gán nhóm cho các loại đơn mặc định — chỉ khi chưa có loại nào thuộc nhóm đó (không ghi đè cấu hình HR sửa sau này)
SET @has_swap = (SELECT COUNT(*) FROM leave_types WHERE category = 'shift_swap');
UPDATE leave_types SET category = 'shift_swap'
WHERE id = 4 AND name = 'Đơn xin đổi ca làm việc' AND @has_swap = 0;

SET @has_overtime = (SELECT COUNT(*) FROM leave_types WHERE category = 'overtime');
UPDATE leave_types SET category = 'overtime'
WHERE id = 5 AND name = 'Đơn xin làm thêm giờ (OT)' AND @has_overtime = 0;

SET @has_deducting = (SELECT COUNT(*) FROM leave_types WHERE deducts_balance = 1);
UPDATE leave_types SET deducts_balance = 1
WHERE id = 1 AND name = 'Nghỉ phép năm (Annual Leave)' AND @has_deducting = 0;

INSERT INTO leave_types (name, category, is_paid, deducts_balance, requires_attachment, description)
SELECT 'Giải trình đi muộn / về sớm', 'explanation', 1, 0, 0, 'Giải trình cho một lượt chấm công đi muộn, về sớm hoặc đang chờ duyệt'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE category = 'explanation');

-- ================================================================ leave_requests

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'branch_id') = 0,
    'ALTER TABLE leave_requests ADD COLUMN branch_id INT NULL COMMENT ''Chi nhánh xử lý đơn'' AFTER user_id',
    'SELECT ''leave_requests.branch_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- Đổi mặc định / chú thích (chạy lại không sao)
ALTER TABLE leave_requests MODIFY COLUMN duration_days DECIMAL(3, 1) NOT NULL DEFAULT 0.0 COMMENT 'Số ngày nghỉ (chỉ đơn nghỉ)';

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'start_at') = 0,
    'ALTER TABLE leave_requests ADD COLUMN start_at DATETIME NULL COMMENT ''Giờ bắt đầu OT'' AFTER duration_days',
    'SELECT ''leave_requests.start_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'end_at') = 0,
    'ALTER TABLE leave_requests ADD COLUMN end_at DATETIME NULL COMMENT ''Giờ kết thúc OT'' AFTER start_at',
    'SELECT ''leave_requests.end_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'duration_hours') = 0,
    'ALTER TABLE leave_requests ADD COLUMN duration_hours DECIMAL(5, 2) NULL COMMENT ''Số giờ OT'' AFTER end_at',
    'SELECT ''leave_requests.duration_hours: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'peer_responded_at') = 0,
    'ALTER TABLE leave_requests ADD COLUMN peer_responded_at DATETIME NULL AFTER target_schedule_id',
    'SELECT ''leave_requests.peer_responded_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'peer_note') = 0,
    'ALTER TABLE leave_requests ADD COLUMN peer_note VARCHAR(255) NULL AFTER peer_responded_at',
    'SELECT ''leave_requests.peer_note: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'attendance_log_id') = 0,
    'ALTER TABLE leave_requests ADD COLUMN attendance_log_id BIGINT NULL COMMENT ''Lượt chấm công được giải trình'' AFTER peer_note',
    'SELECT ''leave_requests.attendance_log_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'attachment_public_id') = 0,
    'ALTER TABLE leave_requests ADD COLUMN attachment_public_id VARCHAR(255) NULL AFTER attachment_url',
    'SELECT ''leave_requests.attachment_public_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

ALTER TABLE leave_requests MODIFY COLUMN status VARCHAR(20) NOT NULL DEFAULT 'pending'
    COMMENT 'pending_peer, pending, approved, rejected, peer_declined, cancelled';

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND column_name = 'updated_at') = 0,
    'ALTER TABLE leave_requests ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP',
    'SELECT ''leave_requests.updated_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.statistics
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND index_name = 'idx_leave_branch_status') = 0,
    'ALTER TABLE leave_requests ADD INDEX idx_leave_branch_status (branch_id, status)',
    'SELECT ''idx_leave_branch_status: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.statistics
                WHERE table_schema = DATABASE() AND table_name = 'leave_requests' AND index_name = 'idx_leave_target_status') = 0,
    'ALTER TABLE leave_requests ADD INDEX idx_leave_target_status (target_user_id, status)',
    'SELECT ''idx_leave_target_status: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

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

-- ================================================================ system_settings
INSERT INTO system_settings (setting_key, setting_value, description) VALUES
('annual_leave_days', '12', 'Số ngày phép năm mặc định của mỗi nhân viên (khi chưa khởi tạo quỹ phép riêng)')
ON DUPLICATE KEY UPDATE setting_key = setting_key;

-- ================================================================ kiểm tra
SELECT id, name, category, deducts_balance, is_active FROM leave_types WHERE is_deleted = 0 ORDER BY id;
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'leave_types'
       AND column_name IN ('category', 'description', 'deducts_balance', 'is_active', 'created_at', 'updated_at')) AS so_cot_moi_leave_types_6,
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'leave_requests'
       AND column_name IN ('branch_id', 'start_at', 'end_at', 'duration_hours', 'peer_responded_at', 'peer_note',
                           'attendance_log_id', 'attachment_public_id', 'updated_at')) AS so_cot_moi_leave_requests_9,
    (SELECT COUNT(*) FROM information_schema.table_constraints
     WHERE table_schema = DATABASE() AND table_name = 'leave_requests'
       AND constraint_name IN ('fk_leave_request_branch', 'fk_leave_request_attendance')) AS so_khoa_ngoai_moi_2;
