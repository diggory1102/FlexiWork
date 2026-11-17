-- =====================================================================
-- Migration 012 — Task 5.8 (UC 7.2): nhóm chat tùy chỉnh
--   conversations.created_by : người tạo nhóm tùy chỉnh (quản trị nhóm: đổi tên, thêm / xóa thành viên).
--                              NULL với chat 1-1 và nhóm chi nhánh (do hệ thống quản lý).
--
-- Không tạo khóa ngoại (TiDB không cho thêm khóa ngoại bằng ALTER ở mọi phiên bản); ứng dụng tự kiểm tra.
-- CHẠY LẠI AN TOÀN: tự kiểm tra đã có cột chưa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'conversations' AND column_name = 'created_by') = 0,
    'ALTER TABLE conversations ADD COLUMN created_by INT NULL COMMENT ''Người tạo nhóm chat tùy chỉnh (quản trị nhóm, UC 7.2); NULL với chat 1-1 và nhóm chi nhánh'' AFTER branch_id',
    'SELECT ''conversations.created_by: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;
