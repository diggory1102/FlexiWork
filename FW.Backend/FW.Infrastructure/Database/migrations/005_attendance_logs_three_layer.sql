-- =====================================================================
-- Migration 005 — Task 3.1: nhật ký chấm công 3 lớp (GPS + Wi-Fi BSSID + Face ID)
--   Thiết kế lại bảng attendance_logs: ngày công, sai số GPS, kết quả từng lớp ở cả lượt vào và lượt ra,
--   độ tương đồng khuôn mặt (cosine 0–1), mã thiết bị, thông tin duyệt; mỗi ca tối đa một bản ghi.
--   Bảng cũ chưa có dữ liệu (chưa có chức năng nào ghi vào) → đổi tên thành bảng sao lưu rồi tạo bảng mới,
--   thay vì ALTER từng cột. Khóa ngoại của bảng mới đặt tên riêng (fk_attendance_*) để không trùng với bảng sao lưu.
-- Chạy MỘT lần trên CSDL đã khởi tạo trước đó (TiDB Cloud / MySQL dev) — xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

-- Số dòng của bảng cũ (kỳ vọng 0)
SELECT COUNT(*) AS so_dong_bang_cu FROM attendance_logs;

RENAME TABLE attendance_logs TO attendance_logs_backup_005;

CREATE TABLE attendance_logs (
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

-- Sau khi kiểm tra so_dong_bang_cu = 0 (hoặc đã chuyển xong dữ liệu), xóa bảng sao lưu:
-- DROP TABLE attendance_logs_backup_005;
