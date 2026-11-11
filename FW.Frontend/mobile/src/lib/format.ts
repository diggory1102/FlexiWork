import type { Role } from '@/types/auth';
import type { EmploymentType, UserStatus } from '@/types/user';

/** Định dạng ngày dd/MM/yyyy từ chuỗi ISO (yyyy-MM-dd hoặc yyyy-MM-ddTHH:mm:ss). */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const [y, m, d] = value.slice(0, 10).split('-');
  return `${d}/${m}/${y}`;
}

const weekdays = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

/** VD: "Thứ năm, 12/11/2026". */
export function formatLongDate(date: Date): string {
  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  return `${weekdays[date.getDay()]}, ${dd}/${mm}/${date.getFullYear()}`;
}

/** Lời chào theo giờ trong ngày. */
export function greeting(date: Date): string {
  const h = date.getHours();
  if (h < 11) return 'Chào buổi sáng';
  if (h < 14) return 'Chào buổi trưa';
  if (h < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

export const roleLabels: Record<Role, string> = {
  ROLE_ADMIN: 'Quản trị viên',
  ROLE_HR: 'Nhân sự',
  ROLE_MANAGER: 'Quản lý chi nhánh',
  ROLE_EMPLOYEE: 'Nhân viên',
};

export const userStatusLabels: Record<UserStatus, string> = {
  active: 'Đang làm việc',
  suspended: 'Đã khóa',
  left: 'Đã nghỉ việc',
};

export const employmentTypeLabels: Record<EmploymentType, string> = {
  full_time: 'Toàn thời gian',
  part_time: 'Bán thời gian',
};

/** Chữ cái đầu của họ và tên (avatar mặc định). */
export function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/);
  const last = parts[parts.length - 1] ?? '';
  const first = parts.length > 1 ? parts[0] : '';
  return ((first[0] ?? '') + (last[0] ?? '')).toUpperCase();
}
