import { create } from 'zustand';

interface UiState {
  /** Màn hình đang ở chế độ toàn màn hình (VD camera chấm công) → ẩn tiêu đề và thanh tab. */
  immersive: boolean;
  setImmersive: (immersive: boolean) => void;
}

export const useUiStore = create<UiState>((set) => ({
  immersive: false,
  setImmersive: (immersive) => set({ immersive }),
}));
