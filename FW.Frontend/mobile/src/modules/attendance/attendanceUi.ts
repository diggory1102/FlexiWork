import type { AttendanceShift, AttendanceStatus, WifiCheckStatus } from '@/types/attendance';
import { formatTime } from '@/lib/format';

type Tone = 'info' | 'success' | 'warning' | 'danger';

export const attendanceStatusBadges: Record<AttendanceStatus, { label: string; tone: Tone }> = {
  valid: { label: 'Hợp lệ', tone: 'success' },
  pending_approval: { label: 'Chờ duyệt', tone: 'warning' },
  invalid: { label: 'Không hợp lệ', tone: 'danger' },
};

export const wifiStatusLabels: Record<WifiCheckStatus, string> = {
  matched: 'Đúng Wi-Fi nội bộ của chi nhánh',
  not_matched: 'Wi-Fi không thuộc chi nhánh',
  no_wifi_info: 'Không đọc được Wi-Fi',
  not_required: 'Chi nhánh không yêu cầu Wi-Fi',
};

/** "08:00 – 17:00" hoặc "22:00 – 06:00 (hôm sau)". */
export function shiftRange(shift: Pick<AttendanceShift, 'startAt' | 'endAt'>): string {
  const nextDay = shift.endAt.slice(0, 10) !== shift.startAt.slice(0, 10);
  return `${formatTime(shift.startAt)} – ${formatTime(shift.endAt)}${nextDay ? ' (hôm sau)' : ''}`;
}

/** Giờ dạng ISO giờ Việt Nam (không múi giờ) → Date theo giờ điện thoại. */
export const parseLocal = (iso: string) => new Date(iso.length > 19 ? iso.slice(0, 19) : iso);
