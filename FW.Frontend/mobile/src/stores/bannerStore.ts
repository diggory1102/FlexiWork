import { create } from 'zustand';
import type { Href } from 'expo-router';

export interface BannerItem {
  id: number;
  title: string;
  body?: string;
  /** Màn hình mở khi chạm vào banner. */
  href?: Href;
}

interface BannerState {
  current: BannerItem | null;
  show: (banner: Omit<BannerItem, 'id'>) => void;
  hide: () => void;
}

let nextId = 1;

/** Banner thông báo trong ứng dụng (tin nhắn mới, thông báo mới khi app đang mở). */
export const useBannerStore = create<BannerState>((set) => ({
  current: null,
  show: (banner) => set({ current: { ...banner, id: nextId++ } }),
  hide: () => set({ current: null }),
}));
