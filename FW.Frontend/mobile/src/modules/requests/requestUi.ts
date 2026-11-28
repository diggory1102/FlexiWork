import type { ComponentProps } from 'react';
import type { Ionicons } from '@expo/vector-icons';
import { formatDate, formatTime } from '@/lib/format';
import { formatDayMonth } from '@/lib/dates';
import type { LeaveRequest, RequestCategory, RequestSchedule, RequestStatus } from '@/types/requests';
import { hues } from '@/theme';

type Tone = 'info' | 'success' | 'warning' | 'danger';
type IconName = ComponentProps<typeof Ionicons>['name'];

export const categoryInfo: Record<RequestCategory, { label: string; icon: IconName; tint: string; hint: string }> = {
  leave: { label: 'Nghỉ', icon: 'airplane-outline', tint: hues.blue, hint: 'Nghỉ phép, nghỉ ốm, việc riêng — theo ngày' },
  shift_swap: { label: 'Đổi ca', icon: 'swap-horizontal-outline', tint: hues.brown, hint: 'Đổi ca với đồng nghiệp hoặc nhờ làm thay' },
  overtime: { label: 'Làm thêm giờ', icon: 'time-outline', tint: hues.orange, hint: 'Đăng ký OT ngoài giờ ca đã xếp' },
  explanation: { label: 'Giải trình', icon: 'chatbox-ellipses-outline', tint: hues.green, hint: 'Giải trình đi muộn, về sớm hoặc lượt chấm công chờ duyệt' },
};

export const statusBadges: Record<RequestStatus, { label: string; tone: Tone }> = {
  pending_peer: { label: 'Chờ đồng nghiệp', tone: 'warning' },
  pending: { label: 'Chờ duyệt', tone: 'warning' },
  approved: { label: 'Đã duyệt', tone: 'success' },
  rejected: { label: 'Bị từ chối', tone: 'danger' },
  peer_declined: { label: 'Đồng nghiệp từ chối', tone: 'danger' },
  cancelled: { label: 'Đã rút', tone: 'info' },
};

/** "1,5" */
export const days = (value: number) => Number(value.toFixed(1)).toString().replace('.', ',');

export const scheduleLabel = (s: RequestSchedule | null) =>
  s ? `${s.shiftName ?? 'Ca'} ${formatDayMonth(s.workDate)} (${s.startTime}–${s.endTime})` : '—';

/** Dòng tóm tắt nội dung đơn theo nhóm. */
export function summarize(r: LeaveRequest): string {
  switch (r.category) {
    case 'leave':
      return r.startDate === r.endDate
        ? `${formatDate(r.startDate)} · ${days(r.durationDays)} ngày`
        : `${formatDate(r.startDate)} → ${formatDate(r.endDate)} · ${days(r.durationDays)} ngày`;
    case 'overtime':
      return `${formatDate(r.startAt)} · ${formatTime(r.startAt)}–${formatTime(r.endAt)}${
        r.endAt?.slice(0, 10) !== r.startAt?.slice(0, 10) ? ' (hôm sau)' : ''
      } · ${days(r.durationHours ?? 0)} giờ`;
    case 'shift_swap':
      return r.targetSchedule
        ? `${scheduleLabel(r.sourceSchedule)} ⇄ ${scheduleLabel(r.targetSchedule)}`
        : `${scheduleLabel(r.sourceSchedule)} · ${r.targetUser?.fullName ?? ''} làm thay`;
    case 'explanation': {
      const log = r.attendanceLog;
      if (!log) return formatDate(r.startDate);
      const issues = [log.lateMinutes > 0 && `muộn ${log.lateMinutes}'`, log.earlyMinutes > 0 && `sớm ${log.earlyMinutes}'`].filter(Boolean);
      return `${log.shiftName ?? 'Ca'} ${formatDate(log.workDate)}${issues.length ? ` · ${issues.join(', ')}` : ''}`;
    }
  }
}
