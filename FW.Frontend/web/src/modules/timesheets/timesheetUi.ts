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
  missingCheckout: 'Thiếu check-out',
  upcoming: 'Chưa tới',
};

export const outcomeTone: Record<ShiftOutcome, BadgeTone> = {
  worked: 'green',
  absent: 'red',
  leave: 'blue',
  pending: 'amber',
  rejected: 'red',
  missingCheckout: 'amber',
  upcoming: 'gray',
};

/** Số thập phân hiển thị gọn (tối đa 2 số lẻ, bỏ số 0 thừa): 21.5 → "21,5". */
export const num = (value: number) => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value);
