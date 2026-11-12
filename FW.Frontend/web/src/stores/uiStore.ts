import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type ColorMode = 'light' | 'dark';

interface UiState {
  colorMode: ColorMode;
  sideNavCollapsed: boolean;
  toggleColorMode: () => void;
  setSideNavCollapsed: (collapsed: boolean) => void;
}

/** Tùy chọn giao diện của từng người dùng (lưu localStorage): chế độ sáng/tối, thu gọn menu. */
export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      colorMode: 'light',
      sideNavCollapsed: false,
      toggleColorMode: () => set((s) => ({ colorMode: s.colorMode === 'light' ? 'dark' : 'light' })),
      setSideNavCollapsed: (sideNavCollapsed) => set({ sideNavCollapsed }),
    }),
    { name: 'flexiwork-ui' },
  ),
);
