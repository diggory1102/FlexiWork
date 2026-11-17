import type { BadgeTone } from '@/components/ui/Feedback';
import type { ShiftOutcome, TimesheetStatus } from '@/types/timesheets';

export const timesheetStatusLabels: Record<TimesheetStatus, string> = {
  draft: 'Nháp',
  confirmed: 'Đã xác nhận',
  locked: 'Đã khóa',
  finalized: 'Đã chốt lương',
};

export const timesheetStatusTone: Record<TimesheetStatus, BadgeTone> = {
  draft: 'gray',
  confirmed: 'blue',
  locked: 'purple',
  finalized: 'green',
};

export const outcomeLabels: Record<ShiftOutcome, string> = {
  worked: 'Đã làm',
  absent: 'Vắng mặt',
  leave: 'Nghỉ phép',
  pending: 'Chờ duyệt',
  rejected: 'Bị từ chối',
  missing_checkout: 'Thiếu check-out',
  upcoming: 'Chưa tới',
};

export const outcomeTone: Record<ShiftOutcome, BadgeTone> = {
  worked: 'green',
  absent: 'red',
  leave: 'blue',
  pending: 'amber',
  rejected: 'red',
  missing_checkout: 'amber',
  upcoming: 'gray',
};

export { formatNumber as num } from '@/lib/format';
