import { create } from 'zustand';

interface AssistantState {
  /** Hội thoại đang mở (null = hội thoại mới). Màn hình lịch sử đặt giá trị này rồi quay lại màn hình trò chuyện. */
  conversationId: number | null;
  /** Đổi mỗi lần chọn để dựng lại luồng kể cả khi chọn lại đúng hội thoại cũ. */
  nonce: number;
  open: (conversationId: number | null) => void;
}

export const useAssistantStore = create<AssistantState>((set) => ({
  conversationId: null,
  nonce: 0,
  open: (conversationId) => set((s) => ({ conversationId, nonce: s.nonce + 1 })),
}));
