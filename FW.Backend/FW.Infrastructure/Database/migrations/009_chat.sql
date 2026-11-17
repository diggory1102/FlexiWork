-- =====================================================================
-- Migration 009 — Task 5.2: chat nội bộ
--   conversations         : unique_key (direct:{id nhỏ}:{id lớn} / branch:{id}) UNIQUE — chống tạo trùng phòng chat khi hai người
--                           cùng bấm nhắn tin một lúc; mỗi chi nhánh đúng một nhóm
--   conversation_members  : last_read_message_id — tính số tin chưa đọc của từng người
--   messages              : image_public_id (xóa ảnh khi thu hồi tin nhắn) + chỉ mục (conversation_id, id) cho phân trang lịch sử
--
-- MỖI CÂU ALTER CHỈ LÀM MỘT VIỆC (TiDB không cho một câu vừa thêm cột vừa tham chiếu cột đó).
-- CHẠY LẠI AN TOÀN: mỗi bước tự kiểm tra đã có chưa. Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'conversations' AND column_name = 'unique_key') = 0,
    'ALTER TABLE conversations ADD COLUMN unique_key VARCHAR(60) NULL COMMENT ''direct:{id nhỏ}:{id lớn} hoặc branch:{id}'' AFTER branch_id',
    'SELECT ''conversations.unique_key: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'conversations' AND index_name = 'uq_conversation_key') = 0,
    'ALTER TABLE conversations ADD UNIQUE KEY uq_conversation_key (unique_key)',
    'SELECT ''conversations.uq_conversation_key: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'conversation_members' AND column_name = 'last_read_message_id') = 0,
    'ALTER TABLE conversation_members ADD COLUMN last_read_message_id BIGINT NOT NULL DEFAULT 0 COMMENT ''Tin nhắn mới nhất đã đọc''',
    'SELECT ''conversation_members.last_read_message_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'messages' AND column_name = 'image_public_id') = 0,
    'ALTER TABLE messages ADD COLUMN image_public_id VARCHAR(255) NULL AFTER image_url',
    'SELECT ''messages.image_public_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

SET @step = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'messages' AND index_name = 'idx_messages_conv_id') = 0,
    'ALTER TABLE messages ADD INDEX idx_messages_conv_id (conversation_id, id)',
    'SELECT ''messages.idx_messages_conv_id: đã có — bỏ qua'' AS thong_bao');
PREPARE migration_step FROM @step; EXECUTE migration_step; DEALLOCATE PREPARE migration_step;

-- ---------------------------------------------------------------- kiểm tra
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE()
       AND ((table_name = 'conversations' AND column_name = 'unique_key')
         OR (table_name = 'conversation_members' AND column_name = 'last_read_message_id')
         OR (table_name = 'messages' AND column_name = 'image_public_id'))) AS so_cot_moi_3,
    (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
     WHERE table_schema = DATABASE()
       AND index_name IN ('uq_conversation_key', 'idx_messages_conv_id')) AS so_chi_muc_moi_2;
