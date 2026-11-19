import type { ShiftOutcome, TimesheetStatus } from '@/types/timesheet';

type Tone = 'info' | 'success' | 'warning' | 'danger';

export const outcomeBadges: Record<ShiftOutcome, { label: string; tone: Tone }> = {
  worked: { label: 'Đã làm', tone: 'success' },
  absent: { label: 'Vắng', tone: 'danger' },
  leave: { label: 'Nghỉ phép', tone: 'info' },
  pending: { label: 'Chờ duyệt', tone: 'warning' },
  rejected: { label: 'Bị từ chối', tone: 'danger' },
  missing_checkout: { label: 'Thiếu check-out', tone: 'warning' },
  upcoming: { label: 'Sắp tới', tone: 'info' },
};

export const statusLabels: Record<TimesheetStatus, string> = {
  draft: 'Nháp',
  confirmed: 'Đã xác nhận',
  locked: 'Đã khóa',
  finalized: 'Đã chốt lương',
};

/** "21,5" — số ngày công (tối đa 2 chữ số thập phân, dấu phẩy). */
export function formatDays(value: number): string {
  return Number(value.toFixed(2)).toString().replace('.', ',');
}
