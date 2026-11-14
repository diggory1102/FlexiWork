-- =====================================================================
-- Migration 001 — Thu hồi phiên đăng nhập khi đổi mật khẩu
-- Thêm cột users.password_changed_at: JWT phát hành trước thời điểm này bị từ chối.
-- Chạy MỘT lần trên CSDL đã khởi tạo trước đó (TiDB Cloud / MySQL dev) — xem docs/DEPLOY.md.
-- Cơ sở dữ liệu mới tạo từ flexiwork_schema.sql đã có sẵn cột này.
-- =====================================================================
USE flexiwork_db;

ALTER TABLE users
    ADD COLUMN password_changed_at DATETIME NULL
        COMMENT 'Lần đổi mật khẩu gần nhất (UTC) — JWT cấp trước thời điểm này bị thu hồi'
        AFTER must_change_password;
