-- =====================================================================
-- Migration 008 — Task 4.3: checklist công việc theo ca
--   shift_tasks            : chi nhánh (NULL = mọi chi nhánh), chức vụ (NULL = mọi nhân viên), giai đoạn đầu / trong / cuối ca,
--                            thứ tự, bắt buộc ảnh hiện trường, thời gian sửa
--   shift_task_assignments : mã ảnh (để xóa khi bỏ tích), thời gian sửa, UNIQUE(schedule_id, task_id) — mỗi việc giao 1 lần / ca
--
-- MỖI CÂU ALTER CHỈ LÀM MỘT VIỆC (TiDB không cho một câu vừa thêm cột vừa tham chiếu cột đó).
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra đã có chưa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'branch_id') = 0,
    'ALTER TABLE shift_tasks ADD COLUMN branch_id INT NULL COMMENT ''NULL = mọi chi nhánh'' AFTER shift_id',
    'SELECT ''shift_tasks.branch_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'position_id') = 0,
    'ALTER TABLE shift_tasks ADD COLUMN position_id INT NULL COMMENT ''NULL = mọi nhân viên'' AFTER branch_id',
    'SELECT ''shift_tasks.position_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'phase') = 0,
    'ALTER TABLE shift_tasks ADD COLUMN phase VARCHAR(10) NOT NULL DEFAULT ''during'' COMMENT ''start, during, end'' AFTER description',
    'SELECT ''shift_tasks.phase: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'sort_order') = 0,
    'ALTER TABLE shift_tasks ADD COLUMN sort_order INT NOT NULL DEFAULT 0 AFTER phase',
    'SELECT ''shift_tasks.sort_order: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'requires_photo') = 0,
    'ALTER TABLE shift_tasks ADD COLUMN requires_photo TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''1: Phải chụp ảnh hiện trường'' AFTER is_mandatory',
    'SELECT ''shift_tasks.requires_photo: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND column_name = 'updated_at') = 0,
    'ALTER TABLE shift_tasks ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP',
    'SELECT ''shift_tasks.updated_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.statistics
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND index_name = 'idx_shift_task_shift_branch') = 0,
    'ALTER TABLE shift_tasks ADD INDEX idx_shift_task_shift_branch (shift_id, branch_id)',
    'SELECT ''idx_shift_task_shift_branch: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.table_constraints
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND constraint_name = 'fk_shift_task_branch') = 0,
    'ALTER TABLE shift_tasks ADD CONSTRAINT fk_shift_task_branch FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE CASCADE',
    'SELECT ''fk_shift_task_branch: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.table_constraints
                WHERE table_schema = DATABASE() AND table_name = 'shift_tasks' AND constraint_name = 'fk_shift_task_position') = 0,
    'ALTER TABLE shift_tasks ADD CONSTRAINT fk_shift_task_position FOREIGN KEY (position_id) REFERENCES positions(id) ON DELETE SET NULL',
    'SELECT ''fk_shift_task_position: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_task_assignments' AND column_name = 'proof_image_public_id') = 0,
    'ALTER TABLE shift_task_assignments ADD COLUMN proof_image_public_id VARCHAR(255) NULL AFTER proof_image_url',
    'SELECT ''shift_task_assignments.proof_image_public_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'shift_task_assignments' AND column_name = 'updated_at') = 0,
    'ALTER TABLE shift_task_assignments ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP',
    'SELECT ''shift_task_assignments.updated_at: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.statistics
                WHERE table_schema = DATABASE() AND table_name = 'shift_task_assignments' AND index_name = 'uq_task_assignment') = 0,
    'ALTER TABLE shift_task_assignments ADD UNIQUE KEY uq_task_assignment (schedule_id, task_id)',
    'SELECT ''uq_task_assignment: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- ---------------------------------------------------------------- kiểm tra
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'shift_tasks'
       AND column_name IN ('branch_id', 'position_id', 'phase', 'sort_order', 'requires_photo', 'updated_at')) AS so_cot_moi_shift_tasks_6,
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'shift_task_assignments'
       AND column_name IN ('proof_image_public_id', 'updated_at')) AS so_cot_moi_assignments_2,
    (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
     WHERE table_schema = DATABASE() AND table_name = 'shift_task_assignments' AND index_name = 'uq_task_assignment') AS co_unique_1;
