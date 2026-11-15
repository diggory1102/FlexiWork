-- =====================================================================
-- Migration 005 — Task 3.1: nhật ký chấm công 3 lớp (GPS + Wi-Fi BSSID + Face ID)
--   Thiết kế lại bảng attendance_logs: ngày công, sai số GPS, kết quả từng lớp ở cả lượt vào và lượt ra,
--   độ tương đồng khuôn mặt (cosine 0–1), mã thiết bị, thông tin duyệt; mỗi ca tối đa một bản ghi.
--   Bảng cũ chưa có dữ liệu (chưa có chức năng nào ghi vào) → đổi tên thành bảng sao lưu rồi tạo bảng mới,
--   thay vì ALTER từng cột. Khóa ngoại của bảng mới đặt tên riêng (fk_attendance_*) để không trùng với bảng sao lưu.
--
-- CHẠY LẠI AN TOÀN: script tự nhận biết trạng thái —
--   * bảng attendance_logs còn là bản cũ (chưa có cột work_date) → đổi tên thành attendance_logs_backup_005 rồi tạo bảng mới;
--   * đã chạy migration trước đó (bảng đã có work_date) → bỏ qua, KHÔNG đụng tới dữ liệu chấm công đang có.
-- Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

-- Bảng attendance_logs hiện tại đã là thiết kế mới chưa (có cột work_date)?
SET @attendance_is_new = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'attendance_logs' AND column_name = 'work_date'
);
SET @attendance_exists = (
    SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_name = 'attendance_logs'
);
SET @backup_exists = (
    SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_name = 'attendance_logs_backup_005'
);

-- Chỉ đổi tên khi bảng còn là bản cũ và chưa có bảng sao lưu
SET @step = IF(@attendance_exists = 1 AND @attendance_is_new = 0 AND @backup_exists = 0,
    'RENAME TABLE attendance_logs TO attendance_logs_backup_005',
    'SELECT ''Bỏ qua bước đổi tên: attendance_logs đã là bảng mới (migration 005 đã chạy trước đó)'' AS thong_bao');
PREPARE migration_step FROM @step;
EXECUTE migration_step;
DEALLOCATE PREPARE migration_step;

CREATE TABLE IF NOT EXISTS attendance_logs (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    schedule_id BIGINT NULL COMMENT 'Ca được chấm công (mỗi ca tối đa 1 bản ghi); NULL nếu lịch đã bị xóa',
    user_id INT NOT NULL,
    branch_id INT NOT NULL COMMENT 'Chi nhánh nơi làm ca',
    work_date DATE NOT NULL COMMENT 'Ngày công = ngày bắt đầu ca (ca qua đêm tính vào ngày bắt đầu)',

    -- Check-in: minh chứng 3 lớp
    checkin_time DATETIME NOT NULL,
    checkin_image_url VARCHAR(255) NOT NULL COMMENT 'Ảnh selfie lúc vào (Cloudinary)',
    checkin_lat DECIMAL(12, 8) NOT NULL,
    checkin_lng DECIMAL(12, 8) NOT NULL,
    checkin_accuracy DOUBLE NULL COMMENT 'Sai số GPS điện thoại báo cáo (m)',
    checkin_distance DOUBLE NOT NULL COMMENT 'Khoảng cách Haversine tới tâm chi nhánh (m)',
    checkin_bssid VARCHAR(17) NULL COMMENT 'BSSID Wi-Fi điện thoại bắt được lúc vào',
    checkin_wifi_matched TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'BSSID khớp Wi-Fi nội bộ của chi nhánh',
    checkin_face_matched TINYINT(1) NOT NULL DEFAULT 0,
    checkin_face_score DOUBLE NULL COMMENT 'Độ tương đồng cosine với khuôn mặt mẫu (0–1)',
    checkin_device_id VARCHAR(100) NULL COMMENT 'Mã thiết bị — phát hiện 1 điện thoại chấm công cho nhiều người',

    -- Check-out
    checkout_time DATETIME NULL,
    checkout_image_url VARCHAR(255) NULL,
    checkout_lat DECIMAL(12, 8) NULL,
    checkout_lng DECIMAL(12, 8) NULL,
    checkout_accuracy DOUBLE NULL,
    checkout_distance DOUBLE NULL,
    checkout_bssid VARCHAR(17) NULL,
    checkout_wifi_matched TINYINT(1) NOT NULL DEFAULT 0,
    checkout_face_matched TINYINT(1) NOT NULL DEFAULT 0,
    checkout_face_score DOUBLE NULL,
    checkout_device_id VARCHAR(100) NULL,

    -- Đối soát
    late_minutes INT NOT NULL DEFAULT 0 COMMENT 'Số phút đi muộn so với giờ bắt đầu ca',
    early_minutes INT NOT NULL DEFAULT 0 COMMENT 'Số phút về sớm so với giờ kết thúc ca',
    actual_work_hours DECIMAL(4, 2) NOT NULL DEFAULT 0.00 COMMENT 'Giờ làm thực tế (đã trừ giờ nghỉ)',
    status VARCHAR(20) NOT NULL DEFAULT 'valid' COMMENT 'valid, pending_approval, invalid',
    note VARCHAR(255) NULL COMMENT 'Lý do hệ thống ghi lại (VD: Wi-Fi không khớp → chờ duyệt)',
    reviewed_by INT NULL COMMENT 'Quản lý / HR duyệt bản ghi chờ duyệt',
    reviewed_at DATETIME NULL,
    review_note VARCHAR(255) NULL,

    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_attendance_schedule FOREIGN KEY (schedule_id) REFERENCES schedules(id) ON DELETE SET NULL,
    CONSTRAINT fk_attendance_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_attendance_branch FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_reviewer FOREIGN KEY (reviewed_by) REFERENCES users(id) ON DELETE SET NULL,
    UNIQUE KEY uq_attendance_schedule (schedule_id),
    INDEX idx_attendance_user_date (user_id, work_date),
    INDEX idx_attendance_branch_date (branch_id, work_date),
    INDEX idx_attendance_status (status),
    INDEX idx_attendance_device_date (checkin_device_id, work_date)
) ENGINE=InnoDB COMMENT='Nhật ký đối soát chấm công đa lớp: GPS + Wi-Fi + Face ID';

-- Kiểm tra kết quả: co_cot_work_date = 1 là bảng mới; so_dong_bang_sao_luu = 0 thì xóa được bảng sao lưu (lệnh cuối file)
SELECT
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'attendance_logs' AND column_name = 'work_date') AS co_cot_work_date,
    (SELECT COUNT(*) FROM attendance_logs) AS so_dong_cham_cong,
    (SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema = DATABASE() AND table_name = 'attendance_logs_backup_005') AS con_bang_sao_luu;

-- Khi con_bang_sao_luu = 1, kiểm tra bảng cũ không có dữ liệu cần giữ:
--   SELECT COUNT(*) AS so_dong_bang_sao_luu FROM attendance_logs_backup_005;
-- rồi xóa bảng sao lưu (chỉ bảng _backup_005, KHÔNG xóa attendance_logs):
--   DROP TABLE attendance_logs_backup_005;
