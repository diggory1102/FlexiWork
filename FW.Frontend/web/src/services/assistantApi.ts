import { env } from '@/config/env';
import { useAuthStore } from '@/stores/authStore';
import type { ApiResponse } from '@/types/api';
import type {
  AssistantConversation,
  AssistantMessage,
  AssistantStreamEvent,
  ChatReply,
  ConfirmDraftResult,
  DraftView,
} from '@/types/assistant';
import { api, ApiError } from './apiClient';

/** Trợ lý AI: hỏi đáp (trả lời hiện dần qua SSE), lịch sử hội thoại, xác nhận / hủy bản nháp đơn. */
export const assistantApi = {
  conversations: (limit = 50) => api.get<AssistantConversation[]>('/assistant/conversations', { limit }),
  messages: (conversationId: number) => api.get<AssistantMessage[]>(`/assistant/conversations/${conversationId}/messages`),
  draft: (id: string) => api.get<DraftView>(`/assistant/drafts/${id}`),
  /** Xác nhận → gửi đơn thật. Bấm lặp lại trả đơn đã gửi (không tạo trùng). `file` là minh chứng nếu loại đơn yêu cầu. */
  confirmDraft: (id: string, file?: File) => {
    const form = new FormData();
    if (file) form.append('file', file);
    return api.post<ConfirmDraftResult>(`/assistant/drafts/${id}/confirm`, form);
  },
  cancelDraft: (id: string) => api.post<DraftView>(`/assistant/drafts/${id}/cancel`),
};

/** Tách các khối "event: x\ndata: {...}" của một luồng SSE (khối được ngăn bằng dòng trống); dòng chú thích ": ping" bị bỏ qua. */
export function parseSseBlock(block: string): AssistantStreamEvent | null {
  let name = '';
  let data = '';
  for (const line of block.split('\n')) {
    if (line.startsWith('event:')) name = line.slice(6).trim();
    else if (line.startsWith('data:')) data += line.slice(5).trim();
  }
  if (!name) return null; // chỉ có chú thích heartbeat
  const payload = data ? (JSON.parse(data) as Record<string, unknown>) : {};
  switch (name) {
    case 'conversation':
      return { type: 'conversation', conversationId: payload.conversationId as number };
    case 'tool':
      return { type: 'tool', name: payload.name as string };
    case 'delta':
      return { type: 'delta', text: payload.text as string };
    case 'reset':
      return { type: 'reset' };
    case 'done':
      return { type: 'done', reply: payload as unknown as ChatReply };
    case 'error':
      return { type: 'error', message: payload.message as string, status: payload.status as number };
    default:
      return null;
  }
}

/**
 * Gửi câu hỏi và nhận câu trả lời theo luồng (SSE). Lỗi nghiệp vụ trước khi luồng mở (hết hạn mức, không thấy hội thoại…)
 * được ném thành ApiError như các API khác; lỗi giữa chừng đến dưới dạng sự kiện `error`.
 */
export async function streamChat(
  body: { conversationId: number | null; message: string },
  onEvent: (event: AssistantStreamEvent) => void,
  signal?: AbortSignal,
): Promise<void> {
  const token = useAuthStore.getState().accessToken;
  const response = await fetch(`${env.apiBaseUrl}/assistant/chat/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: JSON.stringify(body),
    signal,
  }).catch((err: unknown) => {
    if (err instanceof DOMException && err.name === 'AbortError') throw err;
    throw new ApiError('Không thể kết nối tới máy chủ. Vui lòng kiểm tra backend.', undefined);
  });

  if (!response.ok || !response.body) {
    const json = (await response.json().catch(() => null)) as ApiResponse<unknown> | null;
    if (response.status === 401 && token) useAuthStore.getState().logout(json?.message);
    throw new ApiError(json?.message ?? `Lỗi ${response.status}`, response.status, json?.errors ?? []);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true }).replace(/\r\n/g, '\n');
    let end: number;
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const block = buffer.slice(0, end);
      buffer = buffer.slice(end + 2);
      const event = parseSseBlock(block);
      if (event) onEvent(event);
    }
  }
}
