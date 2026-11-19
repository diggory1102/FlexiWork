-- =====================================================================
-- Migration 015 — Task 6.6 (bước 2): ngoại lệ định biên ca theo chi nhánh, theo ngày cụ thể hoặc theo thứ trong tuần
--   Bảng shift_staffing_rules ghi đè số người tối thiểu / tối đa mặc định của ca (shifts.min_staff / max_staff, migration 014)
--   cho một ngày (scope_key = 'date:2026-12-24') hoặc mọi thứ trong tuần (scope_key = 'dow:6'). Mỗi (chi nhánh, ca, phạm vi) một dòng.
--   Không có dòng nào = mọi ca giữ định biên mặc định như trước.
--
-- CHẠY LẠI AN TOÀN: CREATE TABLE IF NOT EXISTS. Khóa ngoại khai báo ngay trong CREATE TABLE (cùng cách với migration 011).
-- Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

CREATE TABLE IF NOT EXISTS shift_staffing_rules (
    id INT AUTO_INCREMENT PRIMARY KEY,
    branch_id INT NOT NULL,
    shift_id INT NOT NULL,
    scope_key VARCHAR(20) NOT NULL COMMENT 'date:yyyy-MM-dd (một ngày cụ thể) hoặc dow:1..7 (mọi thứ Hai … Chủ nhật hằng tuần)',
    min_staff INT NULL COMMENT 'Thay cho shifts.min_staff trong phạm vi này; NULL = không yêu cầu',
    max_staff INT NULL COMMENT 'Thay cho shifts.max_staff trong phạm vi này; NULL = không giới hạn',
    note VARCHAR(255) NULL COMMENT 'Lý do, VD Tiệc đặt bàn công ty ABC',
    created_by INT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_staffing_rule (branch_id, shift_id, scope_key),
    CONSTRAINT fk_staffing_rule_branch FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE CASCADE,
    CONSTRAINT fk_staffing_rule_shift FOREIGN KEY (shift_id) REFERENCES shifts(id) ON DELETE CASCADE,
    CONSTRAINT fk_staffing_rule_user FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Ngoại lệ định biên ca theo chi nhánh (ngày cụ thể / thứ trong tuần)';
