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

/** "08:05" từ chuỗi ISO giờ Việt Nam (yyyy-MM-ddTHH:mm:ss). */
export function formatTime(value: string | null | undefined): string {
  return value ? value.slice(11, 16) : '—';
}

/** "1.234.567 ₫" — số tiền VND (làm tròn đồng). */
export function formatCurrency(value: number): string {
  return `${Math.round(value).toLocaleString('vi-VN')} ₫`;
}

/** "08:05 12/11/2026" từ chuỗi ISO giờ Việt Nam. */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  return `${formatTime(value)} ${formatDate(value)}`;
}

/** Nhãn thời gian ngắn cho danh sách chat / thông báo: giờ nếu hôm nay, "Hôm qua", còn lại dd/MM. Dữ liệu là giờ Việt Nam không kèm múi giờ. */
export function formatShortTime(value: string | null | undefined, now: Date = new Date()): string {
  if (!value) return '';
  const date = value.slice(0, 10);
  const pad = (n: number) => String(n).padStart(2, '0');
  const today = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
  const y = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1);
  const yesterday = `${y.getFullYear()}-${pad(y.getMonth() + 1)}-${pad(y.getDate())}`;
  if (date === today) return formatTime(value);
  if (date === yesterday) return 'Hôm qua';
  const [yy, mm, dd] = date.split('-');
  return yy === String(now.getFullYear()) ? `${dd}/${mm}` : `${dd}/${mm}/${yy}`;
}

/** Nhãn ngày cho dòng phân cách trong chat: "Hôm nay", "Hôm qua" hoặc dd/MM/yyyy. */
export function formatDayLabel(value: string, now: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  const day = value.slice(0, 10);
  if (day === iso(now)) return 'Hôm nay';
  if (day === iso(new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1))) return 'Hôm qua';
  return formatDate(day);
}

/** "7,5 giờ" — số giờ làm (tối đa 2 chữ số thập phân, dấu phẩy thập phân). */
export function formatHours(hours: number): string {
  return `${Number(hours.toFixed(2)).toString().replace('.', ',')} giờ`;
}
