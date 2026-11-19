import { create } from 'zustand';
import { notificationApi } from '@/services/notificationApi';

interface NotificationState {
  /** Số thông báo chưa đọc (huy hiệu trên chuông ở Trang chủ). */
  unread: number;
  /** Nạp lại số chưa đọc (khi mở app, khi kết nối lại và mỗi khi có thông báo mới). */
  refresh: () => Promise<void>;
  setUnread: (count: number) => void;
  reset: () => void;
}

export const useNotificationStore = create<NotificationState>((set) => ({
  unread: 0,
  refresh: async () => {
    const { data } = await notificationApi.unreadCount();
    set({ unread: data.count });
  },
  setUnread: (unread) => set({ unread: Math.max(0, unread) }),
  reset: () => set({ unread: 0 }),
}));
