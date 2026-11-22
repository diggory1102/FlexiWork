-- =====================================================================
-- Migration 016 — Cách giao việc trong ca: "mỗi người một bản" hoặc "làm chung, một người xong là cả ca xong"
--   shift_tasks.assign_mode              : 'each' (mặc định, như trước đây) hoặc 'shared'
--   shift_task_assignments.completed_by  : người đã bấm hoàn thành (việc làm chung: những người còn lại thấy tên người này)
--
-- Việc cũ giữ nguyên 'each' nên checklist đang chạy không đổi.
-- MỖI CÂU ALTER CHỈ LÀM MỘT VIỆC. Không thêm khóa ngoại bằng ALTER (TiDB không cho ở mọi phiên bản; ứng dụng tự kiểm tra).
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra đã có cột chưa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'assign_mode') = 0,
    'ALTER TABLE shift_tasks ADD COLUMN assign_mode VARCHAR(10) NOT NULL DEFAULT ''each'' COMMENT ''each: mỗi người một bản; shared: một người xong là cả ca xong'' AFTER requires_photo',
    'SELECT ''shift_tasks.assign_mode: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'shift_task_assignments' AND column_name = 'completed_by') = 0,
    'ALTER TABLE shift_task_assignments ADD COLUMN completed_by INT NULL COMMENT ''Người đã bấm hoàn thành (việc làm chung có thể khác assigned_to)'' AFTER completed_at',
    'SELECT ''shift_task_assignments.completed_by: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- ---------------------------------------------------------------- kiểm tra
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'assign_mode') AS co_assign_mode_1,
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'shift_task_assignments' AND column_name = 'completed_by') AS co_completed_by_1;
