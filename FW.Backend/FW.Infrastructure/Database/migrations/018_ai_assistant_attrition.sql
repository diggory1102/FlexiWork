-- =====================================================================
-- Migration 018 — Trợ lý nhân sự AI (Task 7.1–7.2) + Cảnh báo nguy cơ nghỉ việc (Task 7.4)
--   Thêm 4 bảng MỚI, không sửa bảng hiện có:
--     assistant_conversations, assistant_messages, assistant_drafts, attrition_risk_snapshots
--   Nguồn: module FW.AI v1.0.0 (database/001_ai_assistant_attrition.sql), đã đưa vào schema chính.
--
-- Khóa ngoại nằm ngay trong CREATE TABLE (TiDB không cho thêm khóa ngoại bằng ALTER).
-- CHẠY LẠI AN TOÀN (CREATE TABLE IF NOT EXISTS). Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

-- Hội thoại với trợ lý (mỗi người một danh sách riêng)
CREATE TABLE IF NOT EXISTS assistant_conversations (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    title VARCHAR(200) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_asst_conv_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_asst_conv_user (user_id, updated_at)
) ENGINE=InnoDB COMMENT='Hội thoại với Trợ lý nhân sự AI';

-- Tin nhắn; role = user | assistant. tool_trace = JSON các công cụ đã gọi (kiểm toán); draft_ids = các bản nháp đi kèm
CREATE TABLE IF NOT EXISTS assistant_messages (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    conversation_id BIGINT NOT NULL,
    role VARCHAR(20) NOT NULL,
    content TEXT NOT NULL,
    tool_trace MEDIUMTEXT NULL,
    draft_ids VARCHAR(600) NULL COMMENT 'Danh sách GUID bản nháp, cách nhau bởi dấu phẩy',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_asst_msg_conv FOREIGN KEY (conversation_id) REFERENCES assistant_conversations(id) ON DELETE CASCADE,
    INDEX idx_asst_msg_conv (conversation_id, id),
    INDEX idx_asst_msg_created (created_at)
) ENGINE=InnoDB COMMENT='Tin nhắn hội thoại với Trợ lý AI; cũng dùng đếm số câu hỏi mỗi ngày (giới hạn chi phí)';

-- Bản nháp đơn do trợ lý soạn — chỉ thành đơn thật (leave_requests) khi nhân viên xác nhận
CREATE TABLE IF NOT EXISTS assistant_drafts (
    id CHAR(36) PRIMARY KEY,
    conversation_id BIGINT NULL,
    user_id INT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending' COMMENT 'pending, confirmed, cancelled, expired',
    card_json MEDIUMTEXT NOT NULL COMMENT 'Nội dung thẻ xác nhận hiển thị cho người dùng',
    submission_json MEDIUMTEXT NOT NULL COMMENT 'Dữ liệu đơn sẽ gửi khi xác nhận',
    request_id INT NULL COMMENT 'leave_requests.id sau khi xác nhận',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at DATETIME NOT NULL,
    CONSTRAINT fk_asst_draft_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_asst_draft_conv FOREIGN KEY (conversation_id) REFERENCES assistant_conversations(id) ON DELETE SET NULL,
    INDEX idx_asst_draft_user (user_id, status)
) ENGINE=InnoDB COMMENT='Bản nháp đơn từ do Trợ lý AI tạo';

-- Điểm nguy cơ nghỉ việc hằng tuần (mỗi nhân viên một dòng mỗi tuần)
CREATE TABLE IF NOT EXISTS attrition_risk_snapshots (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    week_start DATE NOT NULL COMMENT 'Thứ Hai của tuần chấm điểm; cửa sổ gần = 2 tuần ngay trước ngày này',
    score INT NOT NULL COMMENT '0–100',
    risk_level VARCHAR(10) NOT NULL COMMENT 'low, medium, high',
    data_sufficient TINYINT(1) NOT NULL DEFAULT 1 COMMENT '0: chưa đủ dữ liệu để kết luận (nhân viên mới)',
    previous_score INT NULL COMMENT 'Điểm tuần trước, để hiện xu hướng',
    reasons_json TEXT NOT NULL COMMENT 'Danh sách lý do (tiếng Việt)',
    signals_json MEDIUMTEXT NOT NULL COMMENT 'Chi tiết từng tín hiệu: giá trị gần/nền, điểm con, trọng số',
    computed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_attrition_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    UNIQUE KEY uq_attrition_user_week (user_id, week_start),
    INDEX idx_attrition_week (week_start, score)
) ENGINE=InnoDB COMMENT='Điểm nguy cơ nghỉ việc có giải thích — tín hiệu tham khảo cho Quản lý, không dùng để kỷ luật';

SELECT
    (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE()
       AND table_name IN ('assistant_conversations', 'assistant_messages', 'assistant_drafts', 'attrition_risk_snapshots')) AS co_4_bang_ai_1;
