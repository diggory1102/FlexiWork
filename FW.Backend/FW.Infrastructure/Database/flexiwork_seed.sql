-- =============================================================================
-- KỊCH BẢN SEED DATA DỮ LIỆU MẪU CHO FLEXIWORK (MySQL 8.0)
-- NGÀY TẠO: 2026-10-25
-- =============================================================================

USE flexiwork_db;

SET FOREIGN_KEY_CHECKS = 0;

-- 1. SEED PHÒNG BAN (Departments)
INSERT INTO departments (id, code, name, description) VALUES
(1, 'DEP-BOD', 'Ban Giám Đốc', 'Quản trị cấp cao và điều hành doanh nghiệp'),
(2, 'DEP-HR', 'Phòng Hành chính - Nhân sự', 'Tuyển dụng, đào tạo, quản lý chấm công & tính lương'),
(3, 'DEP-OPS', 'Khối Vận hành Chi nhánh', 'Quản lý các cơ sở, điểm bán hàng và đội ngũ nhân viên trực ca'),
(4, 'DEP-TECH', 'Phòng Kỹ thuật & Công nghệ', 'Phát triển và bảo trì hệ thống phần mềm')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- 2. SEED CHỨC VỤ (Positions)
INSERT INTO positions (id, title, base_salary_per_hour, description) VALUES
(1, 'Tổng Giám Đốc', 250000.00, 'Lãnh đạo toàn diện'),
(2, 'Trưởng phòng Nhân sự', 120000.00, 'Quản lý chính sách nhân sự & bảng công'),
(3, 'Quản lý Cửa hàng (Store Manager)', 80000.00, 'Phân ca, duyệt đơn và giám sát chi nhánh'),
(4, 'Nhân viên Bán hàng / Thu ngân', 35000.00, 'Làm việc theo ca trực tiếp tại quầy'),
(5, 'Nhân viên Pha chế / Kỹ thuật', 40000.00, 'Thực hiện nghiệp vụ ca chuyên môn')
ON DUPLICATE KEY UPDATE title=VALUES(title);

-- 3. SEED CHI NHÁNH (Branches)
-- Chi nhánh mẫu: FlexiWork Cầu Giấy (Tọa độ thực tế khu vực Duy Tân / Cầu Giấy, Hà Nội)
INSERT INTO branches (id, branch_code, name, address, phone, email, latitude, longitude, radius, status) VALUES
(1, 'CN-HN-01', 'FlexiWork Cầu Giấy', 'Tòa nhà FPT Cầu Giấy, Phố Duy Tân, Dịch Vọng Hậu, Cầu Giấy, Hà Nội', '02437689999', 'caugiay@flexiwork.vn', 21.03158400, 105.78311500, 50, 'active'),
(2, 'CN-HN-02', 'FlexiWork Hoàn Kiếm', 'Số 25 Lý Thường Kiệt, Phan Chu Trinh, Hoàn Kiếm, Hà Nội', '02439878888', 'hoankiem@flexiwork.vn', 21.02341200, 105.85214000, 60, 'active')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- 4. SEED ACCESS POINT WI-FI (Branch Wifis)
INSERT INTO branch_wifis (id, branch_id, ssid, bssid, is_active) VALUES
(1, 1, 'FlexiWork_CauGiay_Office', '00:1A:2B:3C:4D:5E', 1),
(2, 1, 'FlexiWork_CauGiay_Guest', '00:1A:2B:3C:4D:5F', 1),
(3, 2, 'FlexiWork_HoanKiem_Staff', 'AA:BB:CC:DD:EE:01', 1)
ON DUPLICATE KEY UPDATE ssid=VALUES(ssid);

-- 5. SEED VAI TRÒ (Roles)
INSERT INTO roles (id, role_name, description) VALUES
(1, 'ROLE_ADMIN', 'Quản trị viên toàn hệ thống, toàn quyền cấu hình'),
(2, 'ROLE_MANAGER', 'Quản lý chi nhánh / Trưởng bộ phận, duyệt đơn và phân ca'),
(3, 'ROLE_EMPLOYEE', 'Nhân viên ca kíp, thực hiện chấm công và báo cáo công việc')
ON DUPLICATE KEY UPDATE role_name=VALUES(role_name);

-- 6. SEED TÀI KHOẢN NGƯỜI DÙNG (Users)
-- Mật khẩu băm BCrypt mặc định cho tất cả tài khoản là: "Admin@123" ($2a$11$0FfXf1k9e5R6h7x0a3A7OuUaXzQ5P... hoặc giả lập hash hợp lệ)
-- Dưới đây là chuỗi băm chuẩn BCrypt của mật khẩu "Admin@123":
-- $2a$12$e8Y6bFp58r3y6d7yW4F3beWq3qG8X0kL7P6V3S1N9V0I2O3U4Y5Q2 (ví dụ placeholder chuẩn)
INSERT INTO users (id, employee_code, email, password_hash, full_name, phone_number, branch_id, department_id, position_id, status) VALUES
(1, 'EMP001', 'admin@flexiwork.vn', '$2a$11$qRzN5z3mK.e6jB8xV6QZe.Xy0eQZ0g8GzW8qZ7v5O8u3K2l1P4v5W', 'Nguyễn Quản Trị', '0901234567', 1, 1, 1, 'active'),
(2, 'EMP002', 'manager.cg@flexiwork.vn', '$2a$11$qRzN5z3mK.e6jB8xV6QZe.Xy0eQZ0g8GzW8qZ7v5O8u3K2l1P4v5W', 'Trần Quản Lý', '0912345678', 1, 3, 3, 'active'),
(3, 'EMP003', 'nhanvien.cg@flexiwork.vn', '$2a$11$qRzN5z3mK.e6jB8xV6QZe.Xy0eQZ0g8GzW8qZ7v5O8u3K2l1P4v5W', 'Lê Văn Nhân Viên', '0923456789', 1, 3, 4, 'active')
ON DUPLICATE KEY UPDATE full_name=VALUES(full_name);

-- 7. GÁN VAI TRÒ CHO NGƯỜI DÙNG (User Roles)
INSERT INTO user_roles (user_id, role_id) VALUES
(1, 1), -- EMP001 -> ADMIN
(2, 2), -- EMP002 -> MANAGER
(3, 3)  -- EMP003 -> EMPLOYEE
ON DUPLICATE KEY UPDATE assigned_at=NOW();

-- 8. SEED HỒ SƠ FACE ID (Face Profiles)
-- Vector mẫu 128 số float giả lập cho nhân viên EMP003
INSERT INTO face_profiles (id, user_id, face_encoding, sample_image_url) VALUES
(1, 3, '[-0.1245, 0.0892, 0.2311, -0.0543, 0.1122, 0.0431, -0.1982, 0.0765, 0.1342, -0.0871, 0.0219, -0.0432]', 'https://res.cloudinary.com/flexiwork/image/upload/v1/samples/faces/emp003_sample.jpg')
ON DUPLICATE KEY UPDATE updated_at=NOW();

-- 9. SEED KHUNG CA LÀM VIỆC (Shifts)
INSERT INTO shifts (id, shift_code, name, start_time, end_time, break_minutes, work_units, is_night_shift, is_cross_day) VALUES
(1, 'CA-SANG', 'Ca sáng chuẩn', '08:00:00', '12:00:00', 0, 1.00, 0, 0),
(2, 'CA-CHIEU', 'Ca chiều chuẩn', '13:00:00', '17:00:00', 0, 1.00, 0, 0),
(3, 'CA-TOI', 'Ca tối bán thời gian', '18:00:00', '22:00:00', 0, 1.00, 0, 0),
(4, 'CA-DEM', 'Ca đêm bảo vệ & vệ sinh', '22:00:00', '06:00:00', 60, 1.50, 1, 1)
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- 10. SEED DANH MỤC LOẠI ĐƠN (Leave Types)
INSERT INTO leave_types (id, name, is_paid, requires_attachment) VALUES
(1, 'Nghỉ phép năm (Annual Leave)', 1, 0),
(2, 'Nghỉ ốm / Khám chữa bệnh', 1, 1),
(3, 'Nghỉ việc riêng không hưởng lương', 0, 0),
(4, 'Đơn xin đổi ca làm việc', 1, 0),
(5, 'Đơn xin làm thêm giờ (OT)', 1, 0)
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- 11. SEED QUỸ PHÉP NĂM (Leave Balances)
INSERT INTO leave_balances (id, user_id, year, total_days, used_days, remaining_days) VALUES
(1, 3, 2026, 12.0, 1.0, 11.0)
ON DUPLICATE KEY UPDATE remaining_days=VALUES(remaining_days);

-- 12. SEED ĐẦU VIỆC CHUẨN THEO CA (Shift Tasks)
INSERT INTO shift_tasks (id, shift_id, title, description, is_mandatory) VALUES
(1, 1, 'Kiểm tra vệ sinh và mở cửa cửa hàng', 'Bật đèn, điều hòa, kiểm tra khu vực bàn đón khách', 1),
(2, 1, 'Kiểm đếm tiền đầu ca tại két', 'Đếm tiền mặt và ghi vào biên bản bàn giao đầu ca', 1),
(3, 2, 'Bàn giao quỹ tiền mặt cuối ngày', 'Chốt số liệu doanh thu POS và niêm phong túi tiền', 1),
(4, 4, 'Khóa cổng và kiểm tra hệ thống PCCC', 'Đảm bảo cửa cuốn hạ hết và ngắt nguồn điện không thiết yếu', 1)
ON DUPLICATE KEY UPDATE title=VALUES(title);

SET FOREIGN_KEY_CHECKS = 1;
