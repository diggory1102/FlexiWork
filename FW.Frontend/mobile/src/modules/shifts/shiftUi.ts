import type { Schedule, ScheduleStatus } from '@/types/scheduling';

type Tone = 'info' | 'success' | 'warning' | 'danger';

/** Nhãn trạng thái ca (chỉ hiện khi khác "chưa vào ca"). */
export const scheduleStatusBadges: Partial<Record<ScheduleStatus, { label: string; tone: Tone }>> = {
  in_progress: { label: 'Đang làm', tone: 'info' },
  completed: { label: 'Đã hoàn thành', tone: 'success' },
  absent: { label: 'Vắng mặt', tone: 'danger' },
  cancelled: { label: 'Đã hủy', tone: 'warning' },
};

/** "08:00 – 12:00" hoặc "22:00 – 06:00 (hôm sau)" với ca qua đêm. */
export const shiftHours = (s: Pick<Schedule, 'startTime' | 'endTime' | 'isCrossDay'>) =>
  `${s.startTime} – ${s.endTime}${s.isCrossDay ? ' (hôm sau)' : ''}`;
