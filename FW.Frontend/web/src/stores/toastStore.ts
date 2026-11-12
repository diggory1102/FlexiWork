import { create } from 'zustand';

export type ToastType = 'success' | 'error' | 'info';

export interface PendingToast {
  id: number;
  type: ToastType;
  message: string;
}

interface ToastState {
  /** Thông báo chờ hiển thị — ToastBridge lấy ra và chuyển cho hệ thống Toast của Astryx. */
  pending: PendingToast[];
  show: (type: ToastType, message: string) => void;
  take: () => PendingToast[];
}

let nextId = 1;

export const useToastStore = create<ToastState>((set, get) => ({
  pending: [],
  show: (type, message) => set({ pending: [...get().pending, { id: nextId++, type, message }] }),
  take: () => {
    const items = get().pending;
    if (items.length) set({ pending: [] });
    return items;
  },
}));

/** Gọi nhanh từ bất kỳ đâu (kể cả ngoài component). */
export const toast = {
  success: (message: string) => useToastStore.getState().show('success', message),
  error: (message: string) => useToastStore.getState().show('error', message),
  info: (message: string) => useToastStore.getState().show('info', message),
};
