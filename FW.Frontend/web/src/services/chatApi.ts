import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type { ChatContact, ChatContactFilter, ChatMessage, Conversation, ConversationDetail, MessagePage } from '@/types/chat';

/** Chat nội bộ: chat 1-1 và nhóm chi nhánh. Tin mới tới tức thì qua SignalR (xem RealtimeBridge); gửi tin đi qua REST. */
export const chatApi = {
  conversations: () => api.get<Conversation[]>('/chat/conversations'),
  conversation: (id: number) => api.get<ConversationDetail>(`/chat/conversations/${id}`),
  openDirect: (userId: number) => api.post<Conversation>('/chat/conversations/direct', { userId }),
  /** Nhóm tùy chỉnh (UC 7.2): người tạo là quản trị nhóm. */
  createGroup: (title: string, memberIds: number[]) => api.post<Conversation>('/chat/conversations/group', { title, memberIds }),
  renameGroup: (id: number, title: string) => api.put<Conversation>(`/chat/conversations/${id}`, { title }),
  addMembers: (id: number, userIds: number[]) => api.post<ConversationDetail>(`/chat/conversations/${id}/members`, { userIds }),
  /** Quản trị nhóm xóa người khác, hoặc tự rời nhóm khi userId là chính mình. */
  removeMember: (id: number, userId: number) => api.delete<void>(`/chat/conversations/${id}/members/${userId}`),
  messages: (id: number, beforeId?: number | null, pageSize = 30) => api.get<MessagePage>(`/chat/conversations/${id}/messages`, { beforeId, pageSize }),
  sendText: (id: number, content: string) => api.post<ChatMessage>(`/chat/conversations/${id}/messages`, { content }),
  sendImage: (id: number, file: File, caption?: string) => {
    const form = new FormData();
    form.append('file', file);
    if (caption) form.append('caption', caption);
    return api.post<ChatMessage>(`/chat/conversations/${id}/messages/image`, form);
  },
  markRead: (id: number, upToMessageId?: number) => api.post<{ conversationId: number; unreadCount: number }>(`/chat/conversations/${id}/read`, { upToMessageId }),
  deleteMessage: (id: number) => api.delete<void>(`/chat/messages/${id}`),
  unreadCount: () => api.get<{ count: number }>('/chat/unread-count'),
  contacts: (filter: ChatContactFilter) => api.get<PaginatedResult<ChatContact>>('/chat/contacts', filter),
};
