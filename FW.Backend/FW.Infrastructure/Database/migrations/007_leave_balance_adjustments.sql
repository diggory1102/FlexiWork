-- =====================================================================
-- Migration 007 — Task 4.2: duyệt đơn & quỹ phép
--   leave_balance_adjustments: lịch sử HR cộng / trừ quỹ phép năm kèm lý do (UC 5.5)
--   (Trạng thái lịch mới "on_leave" — nghỉ theo đơn đã duyệt — chỉ là giá trị mới của cột schedules.status
--    VARCHAR(20), không cần đổi cấu trúc bảng.)
--
-- CHẠY LẠI AN TOÀN (CREATE TABLE IF NOT EXISTS). Xem docs/DEPLOY.md.
-- =====================================================================
USE flexiwork_db;

CREATE TABLE IF NOT EXISTS leave_balance_adjustments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    year INT NOT NULL,
    delta_days DECIMAL(4, 1) NOT NULL COMMENT 'Số ngày cộng (dương) / trừ (âm) vào tổng quỹ phép',
    reason VARCHAR(255) NOT NULL,
    created_by INT NULL COMMENT 'Người điều chỉnh',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_leave_adjust_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_leave_adjust_creator FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL,
    INDEX idx_leave_adjust_user_year (user_id, year)
) ENGINE=InnoDB COMMENT='Lịch sử HR điều chỉnh quỹ phép năm';

-- Kiểm tra: phải in 1
SELECT COUNT(*) AS co_bang_leave_balance_adjustments_1 FROM information_schema.tables
WHERE table_schema = DATABASE() AND table_name = 'leave_balance_adjustments';
