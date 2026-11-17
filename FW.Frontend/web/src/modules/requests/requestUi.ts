import type { BadgeTone } from '@/components/ui/Feedback';
import { formatDate } from '@/lib/format';
import { formatDayMonth } from '@/lib/dates';
import type { LeaveRequest, RequestCategory, RequestSchedule, RequestStatus } from '@/types/requests';

export const categoryLabels: Record<RequestCategory, string> = {
  leave: 'Nghỉ',
  shift_swap: 'Đổi ca',
  overtime: 'Làm thêm giờ (OT)',
  explanation: 'Giải trình',
};

export const categoryTone: Record<RequestCategory, BadgeTone> = {
  leave: 'blue',
  shift_swap: 'purple',
  overtime: 'amber',
  explanation: 'green',
};

export const statusLabels: Record<RequestStatus, string> = {
  pending_peer: 'Chờ đồng nghiệp',
  pending: 'Chờ duyệt',
  approved: 'Đã duyệt',
  rejected: 'Bị từ chối',
  peer_declined: 'Đồng nghiệp từ chối',
  cancelled: 'Đã rút',
};

export const statusTone: Record<RequestStatus, BadgeTone> = {
  pending_peer: 'amber',
  pending: 'amber',
  approved: 'green',
  rejected: 'red',
  peer_declined: 'red',
  cancelled: 'gray',
};

/** "1,5" */
export const days = (value: number) => Number(value.toFixed(1)).toLocaleString('vi-VN');
export const hours = (value: number) => Number(value.toFixed(2)).toLocaleString('vi-VN');

/** "HH:mm" từ chuỗi ISO. */
export const timeOf = (iso: string | null | undefined) => (iso ? iso.slice(11, 16) : '—');

export const scheduleLabel = (s: RequestSchedule | null) =>
  s ? `${s.shiftName ?? 'Ca'} ${formatDayMonth(s.workDate)} (${s.startTime}–${s.endTime})` : '—';

/** Dòng tóm tắt nội dung đơn theo nhóm. */
export function summarize(r: LeaveRequest): string {
  switch (r.category) {
    case 'leave':
      return r.startDate === r.endDate
        ? `${formatDate(r.startDate)} · ${days(r.durationDays)} ngày`
        : `${formatDate(r.startDate)} → ${formatDate(r.endDate)} · ${days(r.durationDays)} ngày`;
    case 'overtime': {
      const overnight = r.endAt?.slice(0, 10) !== r.startAt?.slice(0, 10);
      return `${formatDate(r.startAt)} · ${timeOf(r.startAt)}–${timeOf(r.endAt)}${overnight ? ' (hôm sau)' : ''} · ${hours(r.durationHours ?? 0)} giờ`;
    }
    case 'shift_swap':
      return r.targetSchedule
        ? `${scheduleLabel(r.sourceSchedule)} ⇄ ${scheduleLabel(r.targetSchedule)}`
        : `${scheduleLabel(r.sourceSchedule)} · ${r.targetUser?.fullName ?? ''} làm thay`;
    case 'explanation': {
      const log = r.attendanceLog;
      if (!log) return formatDate(r.startDate);
      const issues = [log.lateMinutes > 0 && `muộn ${log.lateMinutes} phút`, log.earlyMinutes > 0 && `sớm ${log.earlyMinutes} phút`].filter(Boolean);
      return `${log.shiftName ?? 'Ca'} ${formatDate(log.workDate)}${issues.length ? ` · ${issues.join(', ')}` : ''}`;
    }
  }
}
