import type { PagedQuery } from './api';

/** Khớp với FW.Application.Features.Notifications và FW.Domain.Enums.NotificationType. */
export type NotificationType = 'attendance' | 'leave_approval' | 'leave_request' | 'schedule_new' | 'task_assigned' | 'payslip';

export interface AppNotification {
  id: number;
  title: string;
  content: string;
  type: NotificationType;
  /** Cách hiểu tùy loại (mã lượt chấm công, mã đơn, ngày đầu của lịch, mã kỳ lương…). */
  referenceId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationFilter extends PagedQuery {
  /** true = chỉ thông báo chưa đọc. */
  unreadOnly?: boolean;
}
