import type { Role } from '@/types/auth';
import type { EmploymentType, UserStatus } from '@/types/user';
import type { BranchStatus } from '@/types/organization';

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });

export const formatCurrency = (value: number | null | undefined) => (value == null ? '—' : currency.format(value));

/** Định dạng ngày dd/MM/yyyy từ chuỗi ISO (yyyy-MM-dd hoặc yyyy-MM-ddTHH:mm:ss). */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const [y, m, d] = value.slice(0, 10).split('-');
  return `${d}/${m}/${y}`;
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  return `${formatDate(value)} ${value.slice(11, 16)}`;
}

/** Ngày hôm nay dạng yyyy-MM-dd theo giờ máy người dùng. */
export function todayIso(): string {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
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

export const branchStatusLabels: Record<BranchStatus, string> = {
  active: 'Đang hoạt động',
  inactive: 'Ngừng hoạt động',
};

export const employmentTypeLabels: Record<EmploymentType, string> = {
  full_time: 'Toàn thời gian',
  part_time: 'Bán thời gian',
};

/** Chữ cái đầu của họ tên (hiển thị avatar mặc định). */
export function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/);
  const last = parts[parts.length - 1] ?? '';
  const first = parts.length > 1 ? parts[0] : '';
  return ((first[0] ?? '') + (last[0] ?? '')).toUpperCase();
}
