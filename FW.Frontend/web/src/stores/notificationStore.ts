import { create } from 'zustand';
import { notificationApi } from '@/services/notificationApi';
import type { AppNotification } from '@/types/notification';

const RECENT_COUNT = 8;

interface NotificationState {
  /** Số thông báo chưa đọc (huy hiệu trên chuông). */
  unread: number;
  /** Vài thông báo mới nhất cho khung chuông. */
  recent: AppNotification[];
  /** Nạp lại số chưa đọc và danh sách gần đây (khi vào trang và khi kết nối lại). */
  refresh: () => Promise<void>;
  markRead: (id: number) => Promise<void>;
  markAllRead: () => Promise<void>;
  reset: () => void;
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  unread: 0,
  recent: [],
  refresh: async () => {
    const [list, count] = await Promise.all([notificationApi.list({ pageIndex: 1, pageSize: RECENT_COUNT }), notificationApi.unreadCount()]);
    set({ recent: list.data.items, unread: count.data.count });
  },
  markRead: async (id) => {
    const target = get().recent.find((n) => n.id === id);
    if (target && !target.isRead) {
      set((s) => ({ recent: s.recent.map((n) => (n.id === id ? { ...n, isRead: true } : n)), unread: Math.max(0, s.unread - 1) }));
    }
    await notificationApi.markRead(id);
  },
  markAllRead: async () => {
    await notificationApi.markAllRead();
    set((s) => ({ recent: s.recent.map((n) => ({ ...n, isRead: true })), unread: 0 }));
  },
  reset: () => set({ unread: 0, recent: [] }),
}));
