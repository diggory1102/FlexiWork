-- =====================================================================
-- Migration 017 — Nhật ký mọi lượt bấm chấm công (kể cả lượt bị từ chối) — phục vụ thí nghiệm thực tế (Task 6.5)
--   attendance_logs chỉ giữ lượt ĐƯỢC ghi nhận; bảng mới này ghi lại cả lượt bị chặn (sai vị trí, Fake GPS, sai mặt...) cùng
--   kết quả từng lớp xác minh và thời gian xử lý, để thống kê tỉ lệ chặn đúng / chặn nhầm.
--
-- Chỉ TẠO BẢNG MỚI, không đụng bảng cũ. Không có khóa ngoại: nhật ký phải sống sót khi nhân viên / chi nhánh bị xóa.
-- CHẠY LẠI AN TOÀN (CREATE TABLE IF NOT EXISTS). Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

CREATE TABLE IF NOT EXISTS attendance_attempts (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    branch_id INT NULL COMMENT 'Chi nhánh của ca (NULL nếu bị chặn trước khi xác định được ca)',
    schedule_id BIGINT NULL,
    action VARCHAR(10) NOT NULL COMMENT 'check_in | check_out',
    attempted_at DATETIME NOT NULL,
    outcome VARCHAR(20) NOT NULL COMMENT 'accepted | pending_approval | rejected | error',
    stage VARCHAR(20) NOT NULL COMMENT 'Lớp xử lý khi kết thúc: validate | time | gps | face | wifi | save',
    message VARCHAR(255) NULL COMMENT 'Lý do từ chối / thông báo trả về',
    latitude DECIMAL(12, 8) NULL,
    longitude DECIMAL(12, 8) NULL,
    accuracy DOUBLE NULL COMMENT 'Sai số GPS điện thoại báo cáo (m)',
    distance DOUBLE NULL COMMENT 'Khoảng cách tới tâm chi nhánh (m)',
    is_mocked TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Hệ điều hành báo vị trí giả lập',
    bssid VARCHAR(17) NULL,
    wifi_status VARCHAR(20) NULL COMMENT 'matched | not_matched | no_wifi_info | not_required',
    face_score DOUBLE NULL COMMENT 'Độ tương đồng cosine (NULL nếu chưa tới lớp khuôn mặt)',
    face_threshold DOUBLE NULL,
    duration_ms INT NOT NULL DEFAULT 0 COMMENT 'Thời gian máy chủ xử lý toàn bộ lượt (ms)',
    device_id VARCHAR(100) NULL,
    INDEX idx_attempt_user_time (user_id, attempted_at),
    INDEX idx_attempt_time (attempted_at)
) ENGINE=InnoDB COMMENT='Nhật ký mọi lượt chấm công, kể cả bị từ chối (thí nghiệm Task 6.5)';

SELECT COUNT(*) AS co_bang_attendance_attempts_1 FROM information_schema.tables
WHERE table_schema = DATABASE() AND table_name = 'attendance_attempts';
