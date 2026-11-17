import type { LucideIcon } from 'lucide-react';
import { CalendarDays, ClipboardCheck, Fingerprint, Inbox, ReceiptText, ClipboardList } from 'lucide-react';
import type { AppNotification, NotificationType } from '@/types/notification';

export const notificationTypeLabels: Record<NotificationType, string> = {
  attendance: 'Chấm công',
  leave_approval: 'Kết quả duyệt đơn',
  leave_request: 'Đơn từ',
  schedule_new: 'Lịch làm việc',
  task_assigned: 'Công việc theo ca',
  payslip: 'Phiếu lương',
};

export const notificationIcons: Record<NotificationType, LucideIcon> = {
  attendance: Fingerprint,
  leave_approval: ClipboardCheck,
  leave_request: Inbox,
  schedule_new: CalendarDays,
  task_assigned: ClipboardList,
  payslip: ReceiptText,
};

/** Trang Web Admin tương ứng với thông báo (null = không có trang để mở, VD: phiếu lương với người không phải Admin / HR). */
export function notificationTarget(n: Pick<AppNotification, 'type'>, isAdminOrHr: boolean): string | null {
  switch (n.type) {
    case 'attendance':
      return '/attendance';
    case 'leave_approval':
    case 'leave_request':
      return '/requests';
    case 'schedule_new':
      return '/schedules';
    case 'task_assigned':
      return '/tasks';
    case 'payslip':
      return isAdminOrHr ? '/payroll' : null;
    default:
      return null;
  }
}

/** "vừa xong", "5 phút trước", "2 giờ trước", "hôm qua", hoặc "17/11/2026". */
export function timeAgo(iso: string, now: Date = new Date()): string {
  const then = new Date(iso);
  const minutes = Math.floor((now.getTime() - then.getTime()) / 60_000);
  if (minutes < 1) return 'vừa xong';
  if (minutes < 60) return `${minutes} phút trước`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} giờ trước`;
  const days = Math.floor(hours / 24);
  if (days === 1) return 'hôm qua';
  if (days < 7) return `${days} ngày trước`;
  const [y, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${y}`;
}
