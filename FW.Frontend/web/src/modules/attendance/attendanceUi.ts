import type { BadgeTone } from '@/components/ui/Feedback';
import type { AttendanceStatus } from '@/types/attendance';

export const attendanceStatusLabels: Record<AttendanceStatus, string> = {
  valid: 'Hợp lệ',
  pending_approval: 'Chờ duyệt',
  invalid: 'Không hợp lệ',
};

export const attendanceStatusTone: Record<AttendanceStatus, BadgeTone> = {
  valid: 'green',
  pending_approval: 'amber',
  invalid: 'red',
};

/** Kết quả lớp Wi-Fi của một lượt: đạt / không đạt / không áp dụng. */
export type LayerState = 'pass' | 'fail' | 'skip';

export function wifiState(matched: boolean, requiresWifi: boolean): LayerState {
  if (matched) return 'pass';
  return requiresWifi ? 'fail' : 'skip';
}

export function wifiText(matched: boolean, bssid: string | null, requiresWifi: boolean): string {
  if (matched) return 'Đúng Wi-Fi nội bộ của chi nhánh';
  if (!requiresWifi) return 'Chi nhánh không khai báo Wi-Fi — bỏ qua';
  return bssid ? 'Wi-Fi không thuộc chi nhánh' : 'Không đọc được Wi-Fi (tắt Wi-Fi, thiếu quyền hoặc iPhone)';
}

/** "12 m" / "1,2 km". */
export function formatDistance(meters: number | null | undefined): string {
  if (meters == null) return '—';
  if (meters < 1000) return `${Math.round(meters)} m`;
  return `${(meters / 1000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} km`;
}

/** Độ tương đồng khuôn mặt (cosine 0–1) → "71%". */
export const formatScore = (score: number | null | undefined) => (score == null ? '—' : `${Math.round(score * 100)}%`);

/** "7,5 giờ" */
export const formatHours = (hours: number) => `${hours.toLocaleString('vi-VN', { maximumFractionDigits: 2 })} giờ`;

/** "HH:mm" từ chuỗi ISO; kèm "dd/MM" khi khác ngày công (ca đêm ra về hôm sau). */
export function timeOf(iso: string | null | undefined, workDate?: string): string {
  if (!iso) return '—';
  const time = iso.slice(11, 16);
  if (workDate && iso.slice(0, 10) !== workDate.slice(0, 10)) {
    const [, m, d] = iso.slice(0, 10).split('-');
    return `${time} (${d}/${m})`;
  }
  return time;
}
