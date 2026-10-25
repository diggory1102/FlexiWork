-- =============================================================================
-- HỆ THỐNG QUẢN LÝ CHẤM CÔNG & NHÂN SỰ FLEXIWORK
-- CƠ SỞ DỮ LIỆU: MySQL 8.0 (InnoDB, utf8mb4_unicode_ci)
-- MÔ THỨC CHUẨN HÓA: 3NF (Third Normal Form)
-- TỐI ƯU HÓA: .NET Clean Architecture + Micro-ORM Dapper
-- NGÀY TẠO: 2026-10-25
-- =============================================================================

CREATE DATABASE IF NOT EXISTS flexiwork_db 
CHARACTER SET utf8mb4 
COLLATE utf8mb4_unicode_ci;

USE flexiwork_db;

-- Tắt kiểm tra khóa ngoại tạm thời để khởi tạo/reset an toàn
SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS monthly_timesheets;
DROP TABLE IF EXISTS notifications;
DROP TABLE IF EXISTS messages;
DROP TABLE IF EXISTS conversation_members;
DROP TABLE IF EXISTS conversations;
DROP TABLE IF EXISTS shift_task_assignments;
DROP TABLE IF EXISTS shift_tasks;
DROP TABLE IF EXISTS leave_balances;
DROP TABLE IF EXISTS leave_requests;
DROP TABLE IF EXISTS leave_types;
DROP TABLE IF EXISTS attendance_logs;
DROP TABLE IF EXISTS schedules;
DROP TABLE IF EXISTS shifts;
DROP TABLE IF EXISTS face_profiles;
DROP TABLE IF EXISTS user_roles;
DROP TABLE IF EXISTS roles;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS positions;
DROP TABLE IF EXISTS departments;
DROP TABLE IF EXISTS branch_wifis;
DROP TABLE IF EXISTS branches;

SET FOREIGN_KEY_CHECKS = 1;

-- =============================================================================
-- 1. NHÓM BẢNG CẤU HÌNH TỔ CHỨC & CHI NHÁNH
-- =============================================================================

-- 1.1. Chi nhánh & Tọa độ Geofence (Branches)
CREATE TABLE branches (
    id INT AUTO_INCREMENT PRIMARY KEY,
    branch_code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(150) NOT NULL,
    address VARCHAR(255) NOT NULL,
    phone VARCHAR(15) NULL,
    email VARCHAR(100) NULL,
    latitude DECIMAL(10, 8) NOT NULL COMMENT 'Vĩ độ tâm chi nhánh',
    longitude DECIMAL(11, 8) NOT NULL COMMENT 'Kinh độ tâm chi nhánh',
    radius INT NOT NULL DEFAULT 50 COMMENT 'Bán kính Geofence hợp lệ (mét)',
    status VARCHAR(20) NOT NULL DEFAULT 'active' COMMENT 'active, inactive',
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_branches_coords (latitude, longitude),
    INDEX idx_branches_status (status, is_deleted)
) ENGINE=InnoDB COMMENT='Thông tin chi nhánh & tọa độ Geofence';

-- 1.2. Danh sách Access Point Wi-Fi nội bộ (Branch Wifis)
CREATE TABLE branch_wifis (
    id INT AUTO_INCREMENT PRIMARY KEY,
    branch_id INT NOT NULL,
    ssid VARCHAR(100) NOT NULL COMMENT 'Tên mạng hiển thị',
    bssid VARCHAR(17) NOT NULL COMMENT 'Địa chỉ MAC Wi-Fi (XX:XX:XX:XX:XX:XX)',
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE CASCADE,
    INDEX idx_branch_wifis_lookup (bssid, is_active)
) ENGINE=InnoDB COMMENT='BSSID Wi-Fi hợp lệ cho chấm công từng chi nhánh';

-- 1.3. Phòng ban (Departments)
CREATE TABLE departments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB COMMENT='Phòng ban công ty';

-- 1.4. Chức vụ & Đơn giá lương giờ (Positions)
CREATE TABLE positions (
    id INT AUTO_INCREMENT PRIMARY KEY,
    title VARCHAR(100) NOT NULL,
    base_salary_per_hour DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
    description VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB COMMENT='Chức danh và mức lương cơ sở theo giờ';

-- =============================================================================
-- 2. NHÓM BẢNG NGƯỜI DÙNG, PHÂN QUYỀN & SINH TRẮC HỌC (FACE ID)
-- =============================================================================

-- 2.1. Tài khoản & Hồ sơ nhân sự (Users)
CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    employee_code VARCHAR(20) NOT NULL UNIQUE,
    email VARCHAR(120) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100) NOT NULL,
    phone_number VARCHAR(15) NULL,
    avatar_url VARCHAR(255) NULL,
    branch_id INT NULL,
    department_id INT NULL,
    position_id INT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'active' COMMENT 'active, suspended, left',
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE SET NULL,
    FOREIGN KEY (department_id) REFERENCES departments(id) ON DELETE SET NULL,
    FOREIGN KEY (position_id) REFERENCES positions(id) ON DELETE SET NULL,
    INDEX idx_users_branch (branch_id, status),
    INDEX idx_users_dept (department_id)
) ENGINE=InnoDB COMMENT='Hồ sơ nhân viên & tài khoản hệ thống';

-- 2.2. Danh mục vai trò (Roles)
CREATE TABLE roles (
    id INT AUTO_INCREMENT PRIMARY KEY,
    role_name VARCHAR(50) NOT NULL UNIQUE COMMENT 'ROLE_ADMIN, ROLE_MANAGER, ROLE_EMPLOYEE',
    description VARCHAR(255) NULL
) ENGINE=InnoDB COMMENT='Vai trò bảo mật RBAC';

-- 2.3. Bảng phân quyền User - Role (User Roles)
CREATE TABLE user_roles (
    user_id INT NOT NULL,
    role_id INT NOT NULL,
    assigned_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='Bảng liên kết quan hệ N-N Người dùng và Vai trò';

-- 2.4. Vector khuôn mặt Face ID (Face Profiles)
CREATE TABLE face_profiles (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL UNIQUE,
    face_encoding JSON NOT NULL COMMENT 'Mảng vector 128 hoặc 512 chiều nhận diện khuôn mặt',
    sample_image_url VARCHAR(255) NOT NULL COMMENT 'Ảnh gốc dùng để trích xuất vector',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='Hồ sơ sinh trắc học Face ID phục vụ đối soát chấm công';

-- =============================================================================
-- 3. NHÓM BẢNG KHUNG CA & PHÂN LỊCH LÀM VIỆC (SHIFTS & SCHEDULES)
-- =============================================================================

-- 3.1. Danh mục khung ca (Shifts)
CREATE TABLE shifts (
    id INT AUTO_INCREMENT PRIMARY KEY,
    shift_code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    break_minutes INT NOT NULL DEFAULT 0,
    work_units DECIMAL(3, 2) NOT NULL DEFAULT 1.00 COMMENT 'Hệ số công quy đổi (1.00, 0.50...)',
    is_night_shift TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: Ca đêm, có tính phụ cấp',
    is_cross_day TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: Ca kết thúc vào ngày hôm sau (start_time > end_time)',
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB COMMENT='Khai báo khung ca làm việc';

-- 3.2. Lịch phân ca chi tiết (Schedules)
CREATE TABLE schedules (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    shift_id INT NOT NULL,
    branch_id INT NOT NULL,
    work_date DATE NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'scheduled' COMMENT 'scheduled, in_progress, completed, absent, cancelled',
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (shift_id) REFERENCES shifts(id) ON DELETE RESTRICT,
    FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE RESTRICT,
    UNIQUE KEY uq_user_shift_date (user_id, shift_id, work_date),
    INDEX idx_schedule_lookup (work_date, branch_id),
    INDEX idx_schedule_user_date (user_id, work_date)
) ENGINE=InnoDB COMMENT='Lịch phân ca thực tế theo ngày của nhân viên';

-- =============================================================================
-- 4. NHÓM BẢNG CHẤM CÔNG ĐA LỚP (ATTENDANCE LOGS)
-- =============================================================================

-- 4.1. Nhật ký điểm danh đa lớp (Check-in & Check-out toàn diện)
CREATE TABLE attendance_logs (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    schedule_id BIGINT NULL COMMENT 'Liên kết lịch phân ca trước (nếu có)',
    user_id INT NOT NULL,
    branch_id INT NOT NULL,
    
    -- Check-in
    checkin_time DATETIME NOT NULL,
    checkin_image_url VARCHAR(255) NOT NULL,
    checkin_lat DECIMAL(10, 8) NOT NULL,
    checkin_lng DECIMAL(11, 8) NOT NULL,
    checkin_distance FLOAT NOT NULL COMMENT 'Khoảng cách GPS đến tâm chi nhánh (m)',
    checkin_bssid VARCHAR(17) NULL COMMENT 'Địa chỉ MAC Wi-Fi điện thoại bắt được lúc vào',
    is_face_matched TINYINT(1) NOT NULL DEFAULT 0,
    face_match_confidence FLOAT NULL COMMENT 'Tỷ lệ tương đồng Face ID (%)',
    
    -- Check-out
    checkout_time DATETIME NULL,
    checkout_image_url VARCHAR(255) NULL,
    checkout_lat DECIMAL(10, 8) NULL,
    checkout_lng DECIMAL(11, 8) NULL,
    checkout_distance FLOAT NULL COMMENT 'Khoảng cách GPS đến tâm chi nhánh lúc ra (m)',
    checkout_bssid VARCHAR(17) NULL COMMENT 'Địa chỉ MAC Wi-Fi điện thoại bắt được lúc ra',
    
    -- Đối soát & Tổng hợp
    late_minutes INT NOT NULL DEFAULT 0 COMMENT 'Số phút đi muộn so với ca',
    early_minutes INT NOT NULL DEFAULT 0 COMMENT 'Số phút về sớm so với ca',
    actual_work_hours DECIMAL(4, 2) NOT NULL DEFAULT 0.00 COMMENT 'Giờ làm việc thực tế tính được',
    status VARCHAR(20) NOT NULL DEFAULT 'valid' COMMENT 'valid, pending_approval, invalid',
    note VARCHAR(255) NULL COMMENT 'Ghi chú giải trình hoặc lý do vi phạm',
    
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (schedule_id) REFERENCES schedules(id) ON DELETE SET NULL,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE RESTRICT,
    INDEX idx_attendance_user_date (user_id, checkin_time),
    INDEX idx_attendance_schedule (schedule_id),
    INDEX idx_attendance_branch_date (branch_id, checkin_time)
) ENGINE=InnoDB COMMENT='Nhật ký đối soát chấm công đa lớp: Face ID + GPS + Wi-Fi';

-- =============================================================================
-- 5. NHÓM BẢNG QUẢN LÝ ĐƠN TỪ & NGHỈ PHÉP (LEAVE & REQUESTS)
-- =============================================================================

-- 5.1. Phân loại đơn từ (Leave Types)
CREATE TABLE leave_types (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL COMMENT 'Nghỉ phép năm, Nghỉ không lương, Đổi ca, Đi muộn/Về sớm, Làm thêm giờ OT',
    is_paid TINYINT(1) NOT NULL DEFAULT 1 COMMENT '1: Có hưởng lương',
    requires_attachment TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: Bắt buộc gửi kèm minh chứng',
    is_deleted TINYINT(1) NOT NULL DEFAULT 0
) ENGINE=InnoDB COMMENT='Danh mục các loại đơn từ';

-- 5.2. Đơn xin nghỉ / Yêu cầu phê duyệt (Leave Requests)
CREATE TABLE leave_requests (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL COMMENT 'Người tạo đơn',
    leave_type_id INT NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    duration_days DECIMAL(3, 1) NOT NULL DEFAULT 1.0,
    
    -- Dành riêng cho đơn đổi ca
    target_user_id INT NULL COMMENT 'Người được yêu cầu đổi ca',
    source_schedule_id BIGINT NULL COMMENT 'Ca muốn đổi của người tạo đơn',
    target_schedule_id BIGINT NULL COMMENT 'Ca muốn nhận của người được đổi',
    
    reason TEXT NOT NULL,
    attachment_url VARCHAR(255) NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending' COMMENT 'pending, approved, rejected, cancelled',
    approved_by INT NULL COMMENT 'Người duyệt',
    approved_at DATETIME NULL,
    approver_note VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (leave_type_id) REFERENCES leave_types(id) ON DELETE RESTRICT,
    FOREIGN KEY (target_user_id) REFERENCES users(id) ON DELETE SET NULL,
    FOREIGN KEY (source_schedule_id) REFERENCES schedules(id) ON DELETE SET NULL,
    FOREIGN KEY (target_schedule_id) REFERENCES schedules(id) ON DELETE SET NULL,
    FOREIGN KEY (approved_by) REFERENCES users(id) ON DELETE SET NULL,
    INDEX idx_leave_user_status (user_id, status),
    INDEX idx_leave_approver (approved_by)
) ENGINE=InnoDB COMMENT='Bảng tiếp nhận và phê duyệt đơn từ';

-- 5.3. Quỹ ngày phép năm của nhân viên (Leave Balances)
CREATE TABLE leave_balances (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    year INT NOT NULL COMMENT 'Năm áp dụng (VD: 2026)',
    total_days DECIMAL(3, 1) NOT NULL DEFAULT 12.0 COMMENT 'Tổng số ngày phép tiêu chuẩn',
    used_days DECIMAL(3, 1) NOT NULL DEFAULT 0.0 COMMENT 'Số ngày phép đã sử dụng',
    remaining_days DECIMAL(3, 1) NOT NULL DEFAULT 12.0 COMMENT 'Số ngày phép còn lại',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_user_leave_year (user_id, year),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='Quỹ ngày phép theo năm của từng nhân viên';

-- =============================================================================
-- 6. NHÓM BẢNG QUẢN LÝ ĐẦU VIỆC THEO CA (SHIFT TASKS)
-- =============================================================================

-- 6.1. Danh mục đầu việc chuẩn theo ca (Shift Tasks)
CREATE TABLE shift_tasks (
    id INT AUTO_INCREMENT PRIMARY KEY,
    shift_id INT NOT NULL,
    title VARCHAR(150) NOT NULL,
    description TEXT NULL,
    is_mandatory TINYINT(1) NOT NULL DEFAULT 1 COMMENT '1: Bắt buộc hoàn thành trước khi hết ca',
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (shift_id) REFERENCES shifts(id) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='Mẫu checklist công việc gắn theo từng loại ca';

-- 6.2. Phân công & Thực hiện checklist theo ca trực (Shift Task Assignments)
CREATE TABLE shift_task_assignments (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    task_id INT NOT NULL,
    schedule_id BIGINT NOT NULL,
    assigned_to INT NOT NULL,
    is_completed TINYINT(1) NOT NULL DEFAULT 0,
    completed_at DATETIME NULL,
    proof_image_url VARCHAR(255) NULL COMMENT 'Ảnh chụp bằng chứng hoàn thành nhiệm vụ',
    note VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (task_id) REFERENCES shift_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (schedule_id) REFERENCES schedules(id) ON DELETE CASCADE,
    FOREIGN KEY (assigned_to) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_task_schedule (schedule_id, is_completed)
) ENGINE=InnoDB COMMENT='Checklist công việc thực tế được giao trong ca trực';

-- =============================================================================
-- 7. NHÓM BẢNG TRUYỀN THÔNG NỘI BỘ & THÔNG BÁO (CHAT & NOTIFICATIONS)
-- =============================================================================

-- 7.1. Cuộc trò chuyện (Conversations)
CREATE TABLE conversations (
    id INT AUTO_INCREMENT PRIMARY KEY,
    type VARCHAR(20) NOT NULL DEFAULT 'direct' COMMENT 'direct, group',
    title VARCHAR(150) NULL COMMENT 'Tên nhóm chat nếu là group',
    branch_id INT NULL COMMENT 'Gắn với chi nhánh cụ thể (nếu là nhóm chat chi nhánh)',
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Phòng chat trực tiếp hoặc nhóm';

-- 7.2. Thành viên cuộc trò chuyện (Conversation Members)
CREATE TABLE conversation_members (
    conversation_id INT NOT NULL,
    user_id INT NOT NULL,
    joined_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (conversation_id, user_id),
    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='Thành viên tham gia phòng chat';

-- 7.3. Tin nhắn (Messages)
CREATE TABLE messages (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    conversation_id INT NOT NULL,
    sender_id INT NOT NULL,
    content TEXT NULL,
    image_url VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE,
    FOREIGN KEY (sender_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_messages_conv_date (conversation_id, created_at)
) ENGINE=InnoDB COMMENT='Tin nhắn nội bộ hỗ trợ SignalR realtime';

-- 7.4. Thông báo đẩy (Notifications)
CREATE TABLE notifications (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    title VARCHAR(150) NOT NULL,
    content TEXT NOT NULL,
    type VARCHAR(50) NOT NULL COMMENT 'attendance, leave_approval, schedule_new, task_assigned',
    reference_id VARCHAR(50) NULL COMMENT 'ID của thực thể liên quan để điều hướng',
    is_read TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_notifications_user_read (user_id, is_read, created_at)
) ENGINE=InnoDB COMMENT='Thông báo hệ thống gửi đến người dùng';

-- =============================================================================
-- 8. NHÓM BẢNG TỔNG HỢP CÔNG & LƯƠNG THÁNG (MONTHLY TIMESHEETS)
-- =============================================================================

-- 8.1. Bảng tổng hợp công & lương (Monthly Timesheets)
CREATE TABLE monthly_timesheets (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    month INT NOT NULL COMMENT 'Tháng (1 - 12)',
    year INT NOT NULL COMMENT 'Năm (YYYY)',
    standard_work_days DECIMAL(4, 1) NOT NULL DEFAULT 26.0 COMMENT 'Số ngày công tiêu chuẩn trong tháng',
    actual_work_days DECIMAL(4, 1) NOT NULL DEFAULT 0.0 COMMENT 'Số ngày công thực tế làm việc',
    total_late_minutes INT NOT NULL DEFAULT 0 COMMENT 'Tổng số phút đi muộn trong tháng',
    total_early_minutes INT NOT NULL DEFAULT 0 COMMENT 'Tổng số phút về sớm trong tháng',
    total_overtime_hours DECIMAL(4, 2) NOT NULL DEFAULT 0.00 COMMENT 'Tổng giờ làm thêm OT',
    paid_leave_days DECIMAL(3, 1) NOT NULL DEFAULT 0.0 COMMENT 'Số ngày nghỉ có lương',
    unpaid_leave_days DECIMAL(3, 1) NOT NULL DEFAULT 0.0 COMMENT 'Số ngày nghỉ không lương',
    calculated_salary DECIMAL(14, 2) NOT NULL DEFAULT 0.00 COMMENT 'Lương quy đổi tạm tính',
    status VARCHAR(20) NOT NULL DEFAULT 'draft' COMMENT 'draft, locked, finalized',
    note VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_user_month_year (user_id, month, year),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_timesheet_month (year, month, status)
) ENGINE=InnoDB COMMENT='Bảng tổng hợp công tháng phục vụ chốt lương';
