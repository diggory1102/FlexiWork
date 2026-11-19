import { create } from 'zustand';
import { chatApi } from '@/services/chatApi';
import type { ChatMessage, Conversation, MessageDeletedEvent } from '@/types/chat';

/** Sự kiện thời gian thực mà màn hình đang mở có thể lắng nghe (thêm tin vào luồng, ẩn tin bị thu hồi). */
export type ChatEvent = { kind: 'message'; message: ChatMessage } | { kind: 'deleted'; event: MessageDeletedEvent };

const listeners = new Set<(e: ChatEvent) => void>();

/** Đăng ký nhận sự kiện chat; trả về hàm hủy đăng ký. */
export function subscribeChat(listener: (e: ChatEvent) => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function emitChat(event: ChatEvent) {
  listeners.forEach((l) => l(event));
}

interface ChatState {
  conversations: Conversation[];
  /** Tổng số tin chưa đọc (huy hiệu trên Trang chủ). */
  totalUnread: number;
  /** Phòng đang mở (tin tới phòng này được coi là đã đọc). */
  activeId: number | null;
  loaded: boolean;
  setActive: (id: number | null) => void;
  load: () => Promise<void>;
  applyMessage: (message: ChatMessage, myId: number) => void;
  setUnread: (conversationId: number, unreadCount: number) => void;
  upsert: (conversation: Conversation) => void;
  reset: () => void;
}

const sortByActivity = (items: Conversation[]) => [...items].sort((a, b) => b.lastActivityAt.localeCompare(a.lastActivityAt));
const sumUnread = (items: Conversation[]) => items.reduce((total, c) => total + c.unreadCount, 0);

export const useChatStore = create<ChatState>((set, get) => ({
  conversations: [],
  totalUnread: 0,
  activeId: null,
  loaded: false,
  setActive: (id) => set({ activeId: id }),
  load: async () => {
    const { data } = await chatApi.conversations();
    const conversations = sortByActivity(data);
    set({ conversations, totalUnread: sumUnread(conversations), loaded: true });
  },
  applyMessage: (message, myId) => {
    const { conversations, activeId } = get();
    const existing = conversations.find((c) => c.id === message.conversationId);
    if (!existing) {
      void get().load().catch(() => undefined); // phòng mới (có người vừa nhắn lần đầu / vừa thêm tôi vào nhóm)
      return;
    }
    const read = message.senderId === myId || activeId === message.conversationId;
    const updated: Conversation = {
      ...existing,
      lastMessage: {
        id: message.id,
        senderId: message.senderId,
        senderName: message.senderName,
        preview: message.imageUrl ? (message.content ? `[Hình ảnh] ${message.content}` : '[Hình ảnh]') : (message.content ?? ''),
        createdAt: message.createdAt,
      },
      lastActivityAt: message.createdAt,
      unreadCount: read ? existing.unreadCount : existing.unreadCount + 1,
    };
    const next = sortByActivity([updated, ...conversations.filter((c) => c.id !== updated.id)]);
    set({ conversations: next, totalUnread: sumUnread(next) });
  },
  setUnread: (conversationId, unreadCount) => {
    const next = get().conversations.map((c) => (c.id === conversationId ? { ...c, unreadCount } : c));
    set({ conversations: next, totalUnread: sumUnread(next) });
  },
  upsert: (conversation) => {
    const next = sortByActivity([conversation, ...get().conversations.filter((c) => c.id !== conversation.id)]);
    set({ conversations: next, totalUnread: sumUnread(next) });
  },
  reset: () => set({ conversations: [], totalUnread: 0, activeId: null, loaded: false }),
}));
