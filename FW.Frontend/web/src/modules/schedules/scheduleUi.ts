import type { Schedule, ScheduleStatus, Shift } from '@/types/scheduling';

type TokenColor = 'default' | 'red' | 'orange' | 'yellow' | 'green' | 'teal' | 'cyan' | 'blue' | 'purple' | 'pink' | 'gray';

export const scheduleStatusLabels: Record<ScheduleStatus, string> = {
  scheduled: 'Chưa vào ca',
  in_progress: 'Đang làm',
  completed: 'Đã hoàn thành',
  absent: 'Vắng mặt',
  cancelled: 'Đã hủy',
  on_leave: 'Nghỉ phép',
};

/** Màu thẻ ca trên lưới: nháp (cam) → đã công bố (xanh dương) → kết quả chấm công. */
export function scheduleColor(s: Schedule): TokenColor {
  if (s.status === 'cancelled') return 'gray';
  if (!s.isPublished) return 'orange';
  switch (s.status) {
    case 'in_progress':
      return 'teal';
    case 'completed':
      return 'green';
    case 'absent':
      return 'red';
    case 'on_leave':
      return 'purple';
    default:
      return 'blue';
  }
}

export function scheduleStateLabel(s: Schedule): string {
  if (s.status === 'cancelled') return 'Đã hủy';
  return s.isPublished ? `Đã công bố · ${scheduleStatusLabels[s.status]}` : 'Bản nháp (nhân viên chưa thấy)';
}

export const legend: { color: TokenColor; label: string }[] = [
  { color: 'orange', label: 'Bản nháp' },
  { color: 'blue', label: 'Đã công bố' },
  { color: 'green', label: 'Đã hoàn thành' },
  { color: 'red', label: 'Vắng mặt' },
  { color: 'purple', label: 'Nghỉ phép' },
  { color: 'gray', label: 'Đã hủy' },
];

export const shiftLabel = (s: Pick<Shift, 'shiftCode' | 'startTime' | 'endTime'>) => `${s.shiftCode} · ${s.startTime}–${s.endTime}`;

// ---------------------------------------------------------------- Định biên ca

/** "2–4 người", "Tối đa 3", "Tối thiểu 2" hoặc "Không giới hạn". */
export function staffingLabel(s: Pick<Shift, 'minStaff' | 'maxStaff'>): string {
  const { minStaff: min, maxStaff: max } = s;
  if (min != null && max != null) return min === max ? `${max} người` : `${min}–${max} người`;
  if (max != null) return `Tối đa ${max}`;
  if (min != null) return `Tối thiểu ${min}`;
  return 'Không giới hạn';
}

/** Ca có khai báo định biên (tối thiểu hoặc tối đa) không. */
export const hasStaffing = (s: Pick<Shift, 'minStaff' | 'maxStaff'>) => s.minStaff != null || s.maxStaff != null;

/** Số người đang giữ chỗ của ca trong từng ngày (bỏ ca đã hủy và ca nghỉ phép — giống cách backend đếm): khóa `${shiftId}|${ngày}`. */
export function countStaffed(schedules: Schedule[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const s of schedules) {
    if (s.status === 'cancelled' || s.status === 'on_leave') continue;
    const key = `${s.shiftId}|${s.workDate}`;
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

export type StaffingState = 'short' | 'ok' | 'full' | 'over';

/** short: chưa đủ tối thiểu · ok: trong khoảng · full: chạm tối đa · over: vượt tối đa (sau khi hạ định biên). */
export function staffingState(count: number, s: Pick<Shift, 'minStaff' | 'maxStaff'>): StaffingState {
  if (s.maxStaff != null && count > s.maxStaff) return 'over';
  if (s.maxStaff != null && count === s.maxStaff) return 'full';
  if (s.minStaff != null && count < s.minStaff) return 'short';
  return 'ok';
}
