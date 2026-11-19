import { api } from './apiClient';
import { imageFormData, multipartConfig, type PickedImage } from './multipart';
import type { PaginatedResult } from '@/types/api';
import type { ChatContact, ChatMessage, Conversation, ConversationDetail, MessagePage } from '@/types/chat';

/** Chat nội bộ: 1-1, nhóm chi nhánh và nhóm tự tạo. Tin mới tới tức thì qua SignalR; gửi tin đi qua REST. */
export const chatApi = {
  conversations: () => api.get<Conversation[]>('/chat/conversations'),
  conversation: (id: number) => api.get<ConversationDetail>(`/chat/conversations/${id}`),
  openDirect: (userId: number) => api.post<Conversation>('/chat/conversations/direct', { userId }),
  createGroup: (title: string, memberIds: number[]) => api.post<Conversation>('/chat/conversations/group', { title, memberIds }),
  renameGroup: (id: number, title: string) => api.put<Conversation>(`/chat/conversations/${id}`, { title }),
  addMembers: (id: number, userIds: number[]) => api.post<ConversationDetail>(`/chat/conversations/${id}/members`, { userIds }),
  /** Quản trị nhóm xóa người khác, hoặc tự rời nhóm khi userId là chính mình. */
  removeMember: (id: number, userId: number) => api.delete<void>(`/chat/conversations/${id}/members/${userId}`),
  messages: (id: number, beforeId?: number | null, pageSize = 30) => api.get<MessagePage>(`/chat/conversations/${id}/messages`, { beforeId: beforeId ?? undefined, pageSize }),
  sendText: (id: number, content: string) => api.post<ChatMessage>(`/chat/conversations/${id}/messages`, { content }),
  sendImage: async (id: number, image: PickedImage, caption?: string) =>
    api.post<ChatMessage>(`/chat/conversations/${id}/messages/image`, await imageFormData(image, { caption: caption || undefined }), multipartConfig),
  markRead: (id: number) => api.post<{ conversationId: number; unreadCount: number }>(`/chat/conversations/${id}/read`, {}),
  deleteMessage: (id: number) => api.delete<void>(`/chat/messages/${id}`),
  contacts: (params: { search?: string; pageIndex?: number; pageSize?: number }) => api.get<PaginatedResult<ChatContact>>('/chat/contacts', params),
};
