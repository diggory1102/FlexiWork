-- =====================================================================
-- Migration 004 — Task 2.4: tên mô hình mặc định của face_profiles là 'buffalo_sc'
-- (bộ mô hình thực sự dùng: SCRFD det_500m + ArcFace w600k_mbf; migration 002 ghi nhầm 'buffalo_l').
-- Chạy MỘT lần trên CSDL đã khởi tạo trước đó — xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

ALTER TABLE face_profiles ALTER COLUMN model_name SET DEFAULT 'buffalo_sc';
UPDATE face_profiles SET model_name = 'buffalo_sc' WHERE model_name = 'buffalo_l';
