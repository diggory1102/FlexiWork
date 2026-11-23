import { useCallback, useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { SendHorizontal, Sparkles } from 'lucide-react';
import { Notice, Spinner } from '@/components/ui/Feedback';
import { accent } from '@/lib/accent';
import { getErrorMessage } from '@/services/apiClient';
import { assistantApi, streamChat } from '@/services/assistantApi';
import type { DraftView } from '@/types/assistant';
import { suggestionsFor, toolLabel } from '../assistantUi';
import { DraftCard } from './DraftCard';

const MAX_LENGTH = 1000;
/** Sau chừng này mà chưa có chữ nào → báo mô hình đang khởi động (nạp lạnh có thể mất 1–2 phút). */
const SLOW_AFTER_MS = 8_000;
const NEAR_BOTTOM_PX = 160;

interface Item {
  key: string;
  role: 'user' | 'assistant';
  content: string;
  drafts: DraftView[];
  /** Trợ lý đang trả lời: hiện chữ dần + dòng trạng thái. */
  streaming?: boolean;
  status?: string;
  failed?: boolean;
}

let keySeed = 0;
const nextKey = () => `m${++keySeed}`;

/**
 * Luồng hội thoại với trợ lý AI. Câu trả lời hiện dần (SSE); khi xong, nội dung chính thức + thẻ bản nháp thay cho bản xem trước.
 * Gắn `key` theo lần chọn hội thoại ở nơi dùng — hội thoại mới được máy chủ cấp mã giữa chừng không làm mất luồng đang chạy.
 */
export function AssistantThread({
  initialConversationId,
  isManager,
  onConversationChanged,
}: {
  initialConversationId: number | null;
  isManager: boolean;
  /** Gọi khi hội thoại được tạo / có tin mới, để cột lịch sử tải lại. */
  onConversationChanged: (conversationId: number) => void;
}) {
  const [items, setItems] = useState<Item[]>([]);
  const [loading, setLoading] = useState(initialConversationId != null);
  const [error, setError] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const [slow, setSlow] = useState(false);
  const conversationId = useRef<number | null>(initialConversationId);
  const scroller = useRef<HTMLDivElement>(null);
  const stickToBottom = useRef(true);
  const abort = useRef<AbortController | null>(null);
  const slowTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Mở hội thoại cũ: tải toàn bộ tin
  useEffect(() => {
    if (initialConversationId == null) return;
    let current = true;
    assistantApi
      .messages(initialConversationId)
      .then(({ data }) => current && setItems(data.map((m) => ({ key: `s${m.id}`, role: m.role, content: m.content, drafts: m.drafts }))))
      .catch((err) => current && setError(getErrorMessage(err)))
      .finally(() => current && setLoading(false));
    return () => {
      current = false;
    };
  }, [initialConversationId]);

  useEffect(
    () => () => {
      abort.current?.abort();
      if (slowTimer.current) clearTimeout(slowTimer.current);
    },
    [],
  );

  useLayoutEffect(() => {
    const el = scroller.current;
    if (el && stickToBottom.current) el.scrollTop = el.scrollHeight;
  }, [items, loading]);

  const onScroll = () => {
    const el = scroller.current;
    if (el) stickToBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < NEAR_BOTTOM_PX;
  };

  const patchAssistant = useCallback((key: string, patch: Partial<Item> | ((item: Item) => Partial<Item>)) => {
    setItems((prev) => prev.map((m) => (m.key === key ? { ...m, ...(typeof patch === 'function' ? patch(m) : patch) } : m)));
  }, []);

  const clearSlow = () => {
    if (slowTimer.current) clearTimeout(slowTimer.current);
    slowTimer.current = null;
    setSlow(false);
  };

  const send = async (raw: string) => {
    const message = raw.trim();
    if (!message || sending) return;
    const assistantKey = nextKey();
    stickToBottom.current = true;
    setError(null);
    setText('');
    setSending(true);
    setItems((prev) => [
      ...prev,
      { key: nextKey(), role: 'user', content: message, drafts: [] },
      { key: assistantKey, role: 'assistant', content: '', drafts: [], streaming: true, status: 'Đang suy nghĩ…' },
    ]);
    slowTimer.current = setTimeout(() => setSlow(true), SLOW_AFTER_MS);

    const controller = new AbortController();
    abort.current = controller;
    let finished = false;
    try {
      await streamChat(
        { conversationId: conversationId.current, message },
        (event) => {
          switch (event.type) {
            case 'conversation':
              conversationId.current = event.conversationId;
              break;
            case 'tool':
              clearSlow();
              patchAssistant(assistantKey, { status: toolLabel(event.name) });
              break;
            case 'delta':
              clearSlow();
              patchAssistant(assistantKey, (m) => ({ content: m.content + event.text, status: undefined }));
              break;
            case 'reset':
              patchAssistant(assistantKey, { content: '', status: 'Đang xử lý…' });
              break;
            case 'done':
              finished = true;
              clearSlow();
              patchAssistant(assistantKey, { content: event.reply.reply, drafts: event.reply.drafts, streaming: false, status: undefined });
              onConversationChanged(event.reply.conversationId);
              break;
            case 'error':
              finished = true;
              clearSlow();
              patchAssistant(assistantKey, { content: event.message, streaming: false, failed: true, status: undefined });
              break;
          }
        },
        controller.signal,
      );
      if (!finished) patchAssistant(assistantKey, (m) => ({ streaming: false, failed: !m.content, content: m.content || 'Kết nối bị gián đoạn, vui lòng thử lại.', status: undefined }));
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') return;
      clearSlow();
      // Lỗi trước khi luồng mở (hết hạn mức, hội thoại không tồn tại…): bỏ bong bóng trống, báo ở khung nhập
      setItems((prev) => prev.filter((m) => m.key !== assistantKey));
      setError(getErrorMessage(err));
      setText(message);
    } finally {
      setSending(false);
    }
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      void send(text);
    }
  };

  const updateDraft = (messageKey: string, draft: DraftView) =>
    patchAssistant(messageKey, (m) => ({ drafts: m.drafts.map((d) => (d.id === draft.id ? draft : d)) }));

  const empty = !loading && items.length === 0;

  return (
    <VStack className="h-full min-h-0">
      <div ref={scroller} onScroll={onScroll} className="min-h-0 flex-1 overflow-y-auto px-4 py-4" aria-live="polite">
        {loading && (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        )}
        {empty && (
          <VStack hAlign="center" vAlign="center" gap={3} className="h-full py-8 text-center">
            <span className={`inline-flex size-12 items-center justify-center rounded-full ${accent.tint} ${accent.text}`}>
              <Icon icon={Sparkles} size="lg" />
            </span>
            <VStack gap={1}>
              <Text weight="semibold" size="lg">
                Tôi có thể giúp gì cho bạn?
              </Text>
              <Text type="supporting">Hỏi về lịch ca, công, ngày phép, đơn từ — hoặc nhờ soạn đơn nghỉ, đổi ca, làm thêm giờ, giải trình.</Text>
            </VStack>
            <HStack gap={2} className="max-w-xl flex-wrap justify-center">
              {suggestionsFor(isManager).map((s) => (
                <button key={s} type="button" onClick={() => void send(s)} className="rounded-full border border-border bg-surface px-3 py-1.5 text-sm hover:bg-muted">
                  {s}
                </button>
              ))}
            </HStack>
          </VStack>
        )}

        {items.map((m) => (
          <div key={m.key} className={`mb-4 flex ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}>
            <VStack gap={2} className={`max-w-[85%] ${m.role === 'user' ? 'items-end' : 'items-start'}`}>
              {m.role === 'user' ? (
                <p className={`m-0 whitespace-pre-wrap break-words rounded-2xl px-3 py-2 text-sm ${accent.bg} ${accent.onBg}`}>{m.content}</p>
              ) : (
                <>
                  {(m.content || m.streaming) && (
                    <div className={`rounded-2xl px-3 py-2 text-sm ${m.failed ? 'border border-[var(--color-error,#dc2626)] text-primary' : 'bg-muted text-primary'}`}>
                      {m.content && <p className="m-0 whitespace-pre-wrap break-words">{m.content}</p>}
                      {m.streaming && (
                        <HStack gap={2} vAlign="center" className={m.content ? 'mt-1' : ''}>
                          <Spinner size="sm" />
                          <Text type="supporting">{m.status ?? 'Đang trả lời…'}</Text>
                        </HStack>
                      )}
                      {m.streaming && slow && !m.content && <Text type="supporting" className="mt-1 block">Mô hình đang khởi động, lần đầu có thể mất 1–2 phút.</Text>}
                    </div>
                  )}
                  {m.drafts.map((d) => (
                    <DraftCard key={d.id} draft={d} onChange={(draft) => updateDraft(m.key, draft)} />
                  ))}
                </>
              )}
            </VStack>
          </div>
        ))}
      </div>

      <VStack gap={2} padding={3} className="border-t border-border">
        <Notice title={error} />
        <HStack gap={2} vAlign="end">
          <textarea
            aria-label="Nhập câu hỏi cho trợ lý"
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={onKeyDown}
            rows={1}
            maxLength={MAX_LENGTH}
            placeholder="Hỏi trợ lý hoặc nhờ soạn đơn… (Enter để gửi, Shift + Enter xuống dòng)"
            className={`max-h-32 min-h-10 flex-1 resize-none rounded-lg border border-border bg-surface px-3 py-2 text-sm outline-none ${accent.focusBorder}`}
          />
          <Button label="Gửi" variant="primary" icon={<Icon icon={SendHorizontal} />} isLoading={sending} isDisabled={!text.trim()} onClick={() => void send(text)} />
        </HStack>
        <Text type="supporting">Trợ lý có thể nhầm — hãy kiểm tra kỹ bản nháp trước khi xác nhận gửi đơn.</Text>
      </VStack>
    </VStack>
  );
}
