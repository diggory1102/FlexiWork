import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type { AppNotification, NotificationFilter } from '@/types/notification';

export const notificationApi = {
  list: (filter: NotificationFilter) => api.get<PaginatedResult<AppNotification>>('/notifications', filter),
  unreadCount: () => api.get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: number) => api.post<void>(`/notifications/${id}/read`),
  markAllRead: () => api.post<void>('/notifications/read-all'),
};
