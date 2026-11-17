-- =====================================================================
-- Migration 013 — Task 6.1: Quản lý / HR điều chỉnh giờ ra thủ công (nhân viên quên check-out)
--   attendance_logs.checkout_adjusted_by   : người điều chỉnh
--   attendance_logs.checkout_adjusted_at   : thời điểm điều chỉnh
--   attendance_logs.checkout_adjust_note   : lý do
--
-- MỖI CÂU ALTER CHỈ LÀM MỘT VIỆC. Không thêm khóa ngoại bằng ALTER (TiDB không cho ở mọi phiên bản; ứng dụng tự kiểm tra).
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra đã có cột chưa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'attendance_logs' AND column_name = 'checkout_adjusted_by') = 0,
    'ALTER TABLE attendance_logs ADD COLUMN checkout_adjusted_by INT NULL COMMENT ''Quản lý / HR điều chỉnh giờ ra thủ công (nhân viên quên check-out)'' AFTER review_note',
    'SELECT ''attendance_logs.checkout_adjusted_by: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'attendance_logs' AND column_name = 'checkout_adjusted_at') = 0,
    'ALTER TABLE attendance_logs ADD COLUMN checkout_adjusted_at DATETIME NULL AFTER checkout_adjusted_by',
    'SELECT ''attendance_logs.checkout_adjusted_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'attendance_logs' AND column_name = 'checkout_adjust_note') = 0,
    'ALTER TABLE attendance_logs ADD COLUMN checkout_adjust_note VARCHAR(255) NULL COMMENT ''Lý do điều chỉnh giờ ra'' AFTER checkout_adjusted_at',
    'SELECT ''attendance_logs.checkout_adjust_note: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;
