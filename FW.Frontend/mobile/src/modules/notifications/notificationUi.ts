import type { ComponentProps } from 'react';
import type { Ionicons } from '@expo/vector-icons';
import type { Href } from 'expo-router';
import type { AppNotification, NotificationType } from '@/types/notification';

type IconName = ComponentProps<typeof Ionicons>['name'];

export const notificationIcons: Record<NotificationType, { icon: IconName; tint: string }> = {
  attendance: { icon: 'finger-print-outline', tint: '#2563EB' },
  leave_approval: { icon: 'checkmark-done-outline', tint: '#16A34A' },
  leave_request: { icon: 'mail-unread-outline', tint: '#D97706' },
  schedule_new: { icon: 'calendar-outline', tint: '#7C3AED' },
  task_assigned: { icon: 'clipboard-outline', tint: '#0891B2' },
  payslip: { icon: 'receipt-outline', tint: '#DB2777' },
};

/** Màn hình mở khi chạm vào thông báo. */
export function notificationTarget(n: Pick<AppNotification, 'type' | 'referenceId'>): Href | null {
  switch (n.type) {
    case 'attendance':
      return '/attendance-history';
    case 'leave_approval':
    case 'leave_request': {
      const id = Number(n.referenceId);
      return Number.isInteger(id) && id > 0 ? { pathname: '/request/[id]', params: { id: String(id) } } : '/requests';
    }
    case 'schedule_new':
      return '/schedule';
    case 'task_assigned':
      return '/tasks';
    case 'payslip':
      return '/payslips';
    default:
      return null;
  }
}

/** "vừa xong", "5 phút trước", "2 giờ trước", "hôm qua", hoặc "17/11/2026". */
export function timeAgo(iso: string, now: Date = new Date()): string {
  const minutes = Math.floor((now.getTime() - new Date(iso).getTime()) / 60_000);
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
