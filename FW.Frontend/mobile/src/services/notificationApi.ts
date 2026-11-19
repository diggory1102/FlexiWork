import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type { AppNotification } from '@/types/notification';

export const notificationApi = {
  list: (params: { pageIndex?: number; pageSize?: number; unreadOnly?: boolean } = {}) => api.get<PaginatedResult<AppNotification>>('/notifications', params),
  unreadCount: () => api.get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: number) => api.post<void>(`/notifications/${id}/read`),
  markAllRead: () => api.post<void>('/notifications/read-all'),
};
