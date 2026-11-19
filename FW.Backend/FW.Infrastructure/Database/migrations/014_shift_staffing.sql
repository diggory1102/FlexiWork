-- =====================================================================
-- Migration 014 — Định biên ca: số người tối thiểu / tối đa mỗi ca (mỗi chi nhánh, mỗi ngày)
--   shifts.min_staff : số người tối thiểu — chưa đủ thì giao diện chỉ cảnh báo (NULL = không yêu cầu)
--   shifts.max_staff : số người tối đa — đủ rồi thì không xếp thêm được (NULL = không giới hạn, như trước đây)
--
-- Ca cũ giữ nguyên (cả hai cột NULL) nên lịch đang chạy không bị ảnh hưởng.
-- MỖI CÂU ALTER CHỈ LÀM MỘT VIỆC. Không thêm khóa ngoại bằng ALTER (TiDB không cho ở mọi phiên bản; ứng dụng tự kiểm tra).
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra đã có cột chưa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'shifts' AND column_name = 'min_staff') = 0,
    'ALTER TABLE shifts ADD COLUMN min_staff INT NULL COMMENT ''Số người tối thiểu mỗi ngày (mỗi chi nhánh); NULL = không yêu cầu'' AFTER early_leave_grace_minutes',
    'SELECT ''shifts.min_staff: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'shifts' AND column_name = 'max_staff') = 0,
    'ALTER TABLE shifts ADD COLUMN max_staff INT NULL COMMENT ''Số người tối đa mỗi ngày (mỗi chi nhánh); NULL = không giới hạn'' AFTER min_staff',
    'SELECT ''shifts.max_staff: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;
