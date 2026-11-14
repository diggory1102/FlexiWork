-- =====================================================================
-- Migration 002 — Task 2.1: khung ca, lịch phân ca, Face ID
--   shifts        : ca riêng theo chi nhánh (branch_id NULL = dùng chung), số phút cho phép đi muộn/về sớm, updated_at
--   schedules     : lịch nháp / công bố, người xếp lịch, updated_at; bỏ xóa mềm (lịch nháp xóa hẳn,
--                   lịch đã công bố chuyển trạng thái 'cancelled')
--   face_profiles : mã ảnh Cloudinary (xóa ảnh cũ), tên mô hình trích xuất vector, created_at
-- Chạy MỘT lần trên CSDL đã khởi tạo trước đó (TiDB Cloud / MySQL dev) — xem docs/DEPLOY.md.
-- Mỗi thay đổi một câu ALTER để tương thích TiDB.
-- =====================================================================
USE flexiwork_db;

-- shifts
ALTER TABLE shifts ADD COLUMN branch_id INT NULL COMMENT 'Chi nhánh sở hữu ca; NULL = ca dùng chung mọi chi nhánh' AFTER name;
ALTER TABLE shifts ADD COLUMN late_grace_minutes INT NOT NULL DEFAULT 5 COMMENT 'Số phút check-in trễ được phép, chưa tính đi muộn' AFTER is_cross_day;
ALTER TABLE shifts ADD COLUMN early_leave_grace_minutes INT NOT NULL DEFAULT 5 COMMENT 'Số phút check-out sớm được phép, chưa tính về sớm' AFTER late_grace_minutes;
ALTER TABLE shifts ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP AFTER created_at;
ALTER TABLE shifts ADD INDEX idx_shifts_branch (branch_id);
ALTER TABLE shifts ADD CONSTRAINT fk_shifts_branch FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE RESTRICT;

-- schedules
ALTER TABLE schedules ADD COLUMN is_published TINYINT(1) NOT NULL DEFAULT 0 COMMENT '0: Lịch nháp (nhân viên chưa thấy), 1: Đã công bố' AFTER status;
ALTER TABLE schedules ADD COLUMN published_at DATETIME NULL AFTER is_published;
ALTER TABLE schedules ADD COLUMN created_by INT NULL COMMENT 'Người xếp lịch (Quản lý / HR)' AFTER published_at;
ALTER TABLE schedules ADD COLUMN updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP AFTER created_at;
ALTER TABLE schedules DROP COLUMN is_deleted;
ALTER TABLE schedules ADD INDEX idx_schedules_created_by (created_by);
ALTER TABLE schedules ADD CONSTRAINT fk_schedules_created_by FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL;

-- face_profiles
ALTER TABLE face_profiles ADD COLUMN sample_image_public_id VARCHAR(255) NULL COMMENT 'Mã ảnh trên Cloudinary — xóa ảnh cũ khi đăng ký lại' AFTER sample_image_url;
ALTER TABLE face_profiles ADD COLUMN model_name VARCHAR(50) NOT NULL DEFAULT 'buffalo_l' COMMENT 'Mô hình trích xuất vector (vector khác mô hình không so khớp được)' AFTER sample_image_public_id;
ALTER TABLE face_profiles ADD COLUMN created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER model_name;
