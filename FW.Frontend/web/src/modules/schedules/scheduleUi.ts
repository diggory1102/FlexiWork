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
