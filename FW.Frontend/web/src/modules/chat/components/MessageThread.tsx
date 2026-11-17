import { useCallback, useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Lightbox } from '@astryxdesign/core/Lightbox';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { ImagePlus, Info, SendHorizontal, Undo2 } from 'lucide-react';
import { Avatar } from '@/components/ui/Layout';
import { Notice, Spinner } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { accent } from '@/lib/accent';
import { chatApi } from '@/services/chatApi';
import { getErrorMessage } from '@/services/apiClient';
import { subscribeChat, useChatStore } from '@/stores/chatStore';
import { toast } from '@/stores/toastStore';
import { MAX_IMAGE_BYTES, MAX_MESSAGE_LENGTH, type ChatMessage, type Conversation } from '@/types/chat';
import { dayLabel, groupMessages } from '../chatUi';
import { ConversationAvatar } from './ConversationList';
import { GroupInfoDrawer } from './GroupInfoDrawer';

const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/heic', 'image/heif'];
const NEAR_BOTTOM_PX = 160;

/** Luồng tin nhắn của một phòng (gắn `key` theo mã phòng ở nơi dùng): lịch sử, tin mới tức thì, gửi chữ / ảnh, thu hồi tin của mình. */
export function MessageThread({ conversation, myId }: { conversation: Conversation; myId: number }) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [nextBefore, setNextBefore] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const [recalling, setRecalling] = useState<ChatMessage | null>(null);
  const [photo, setPhoto] = useState<string | null>(null);
  const [showInfo, setShowInfo] = useState(false);
  const scroller = useRef<HTMLDivElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const stickToBottom = useRef(true);
  const pendingScrollRestore = useRef<number | null>(null);
  const conversationId = conversation.id;

  const markRead = useCallback(() => {
    chatApi
      .markRead(conversationId)
      .then(({ data }) => useChatStore.getState().setUnread(conversationId, data.unreadCount))
      .catch(() => undefined);
  }, [conversationId]);

  // Vào phòng: tải trang tin mới nhất rồi đánh dấu đã đọc
  useEffect(() => {
    let current = true;
    useChatStore.getState().setActive(conversationId);
    chatApi
      .messages(conversationId)
      .then(({ data }) => {
        if (!current) return;
        setMessages(data.items);
        setHasMore(data.hasMore);
        setNextBefore(data.nextBeforeId);
        markRead();
      })
      .catch((err) => current && setError(getErrorMessage(err)))
      .finally(() => current && setLoading(false));
    return () => {
      current = false;
      if (useChatStore.getState().activeId === conversationId) useChatStore.getState().setActive(null);
    };
  }, [conversationId, markRead]);

  // Tin mới / tin bị thu hồi tức thì từ SignalR
  useEffect(
    () =>
      subscribeChat((event) => {
        if (event.kind === 'message') {
          const m = event.message;
          if (m.conversationId !== conversationId) return;
          const el = scroller.current;
          stickToBottom.current = !el || el.scrollHeight - el.scrollTop - el.clientHeight < NEAR_BOTTOM_PX || m.senderId === myId;
          setMessages((prev) => (prev.some((x) => x.id === m.id) ? prev : [...prev, m]));
          if (m.senderId !== myId) markRead();
        } else if (event.event.conversationId === conversationId) {
          setMessages((prev) => prev.map((m) => (m.id === event.event.messageId ? { ...m, isDeleted: true, content: null, imageUrl: null } : m)));
        }
      }),
    [conversationId, myId, markRead],
  );

  // Giữ vị trí cuộn: cuối luồng khi có tin mới, hoặc đúng chỗ cũ sau khi tải thêm tin cũ
  useLayoutEffect(() => {
    const el = scroller.current;
    if (!el) return;
    if (pendingScrollRestore.current != null) {
      el.scrollTop = el.scrollHeight - pendingScrollRestore.current;
      pendingScrollRestore.current = null;
    } else if (stickToBottom.current) {
      el.scrollTop = el.scrollHeight;
    }
  }, [messages, loading]);

  const loadOlder = async () => {
    if (!hasMore || nextBefore == null || loadingOlder) return;
    setLoadingOlder(true);
    try {
      const { data } = await chatApi.messages(conversationId, nextBefore);
      const el = scroller.current;
      pendingScrollRestore.current = el ? el.scrollHeight - el.scrollTop : null;
      stickToBottom.current = false;
      setMessages((prev) => [...data.items.filter((m) => !prev.some((x) => x.id === m.id)), ...prev]);
      setHasMore(data.hasMore);
      setNextBefore(data.nextBeforeId);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoadingOlder(false);
    }
  };

  const append = (message: ChatMessage) => {
    stickToBottom.current = true;
    setMessages((prev) => (prev.some((m) => m.id === message.id) ? prev : [...prev, message]));
    useChatStore.getState().applyMessage(message, myId);
  };

  const send = async () => {
    const content = text.trim();
    if (!content || sending) return;
    if (content.length > MAX_MESSAGE_LENGTH) return toast.error(`Tin nhắn tối đa ${MAX_MESSAGE_LENGTH} ký tự.`);
    setSending(true);
    try {
      append((await chatApi.sendText(conversationId, content)).data);
      setText('');
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  };

  const sendImage = async (file: File) => {
    if (!IMAGE_TYPES.includes(file.type)) return toast.error('Ảnh phải là JPG, PNG, WEBP hoặc HEIC.');
    if (file.size > MAX_IMAGE_BYTES) return toast.error('Ảnh tối đa 5 MB.');
    setSending(true);
    try {
      append((await chatApi.sendImage(conversationId, file, text.trim() || undefined)).data);
      setText('');
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  };

  const recall = async () => {
    if (!recalling) return;
    const target = recalling;
    setRecalling(null);
    try {
      await chatApi.deleteMessage(target.id);
      setMessages((prev) => prev.map((m) => (m.id === target.id ? { ...m, isDeleted: true, content: null, imageUrl: null } : m)));
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      void send();
    }
  };

  const groups = groupMessages(messages);
  const isGroupChat = conversation.type === 'group';

  return (
    <VStack className="h-full min-h-0">
      <HStack gap={3} vAlign="center" padding={3} className="border-b border-border">
        <ConversationAvatar conversation={conversation} />
        <VStack gap={0.5} className="min-w-0">
          <Text weight="semibold" maxLines={1}>
            {conversation.title}
          </Text>
          <Text type="supporting">{isGroupChat ? `${conversation.memberCount} thành viên${conversation.isCustomGroup ? '' : ' · nhóm chi nhánh'}` : 'Chat trực tiếp'}</Text>
        </VStack>
        {isGroupChat && (
          <IconButton variant="ghost" label="Thông tin nhóm" tooltip="Thông tin nhóm" icon={<Icon icon={Info} />} className="ml-auto" onClick={() => setShowInfo(true)} />
        )}
      </HStack>

      <div ref={scroller} className="min-h-0 flex-1 overflow-y-auto px-4 py-3" aria-live="polite">
        {error && <Notice title={error} />}
        {loading && (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        )}
        {!loading && hasMore && (
          <div className="mb-3 flex justify-center">
            <Button label="Tải tin cũ hơn" variant="ghost" size="sm" isLoading={loadingOlder} onClick={() => void loadOlder()} />
          </div>
        )}
        {!loading && messages.length === 0 && !error && (
          <Text type="supporting" className="block py-10 text-center">
            Chưa có tin nhắn. Hãy gửi lời chào đầu tiên!
          </Text>
        )}
        {groups.map((group) => (
          <div key={group.day}>
            <div className="my-3 flex justify-center">
              <span className="rounded-full bg-muted px-3 py-0.5 text-xs text-secondary">{dayLabel(`${group.day}T00:00:00`)}</span>
            </div>
            {group.runs.map((run, i) => {
              const mine = run.senderId === myId;
              const first = run.messages[0];
              return (
                <div key={`${first.id}-${i}`} className={`mb-3 flex gap-2 ${mine ? 'flex-row-reverse' : ''}`}>
                  {!mine && isGroupChat ? <Avatar name={first.senderName} url={first.senderAvatarUrl} size={32} /> : !mine ? <span className="w-0" /> : null}
                  <VStack gap={0.5} className={`max-w-[70%] ${mine ? 'items-end' : 'items-start'}`}>
                    {!mine && isGroupChat && (
                      <Text type="supporting" className="px-1">
                        {first.senderName}
                      </Text>
                    )}
                    {run.messages.map((m, idx) => (
                      <Bubble
                        key={m.id}
                        message={m}
                        mine={mine}
                        showTime={idx === run.messages.length - 1}
                        onRecall={() => setRecalling(m)}
                        onOpenPhoto={() => m.imageUrl && setPhoto(m.imageUrl)}
                      />
                    ))}
                  </VStack>
                </div>
              );
            })}
          </div>
        ))}
      </div>

      <HStack gap={2} vAlign="end" padding={3} className="border-t border-border">
        <input
          ref={fileInput}
          type="file"
          accept={IMAGE_TYPES.join(',')}
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            e.target.value = '';
            if (file) void sendImage(file);
          }}
        />
        <IconButton variant="ghost" label="Gửi ảnh" tooltip="Gửi ảnh (≤ 5 MB)" icon={<Icon icon={ImagePlus} />} isDisabled={sending} onClick={() => fileInput.current?.click()} />
        <textarea
          aria-label="Nhập tin nhắn"
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={onKeyDown}
          rows={1}
          maxLength={MAX_MESSAGE_LENGTH}
          placeholder="Nhập tin nhắn… (Enter để gửi, Shift + Enter xuống dòng)"
          className={`max-h-32 min-h-10 flex-1 resize-none rounded-lg border border-border bg-surface px-3 py-2 text-sm outline-none ${accent.focusBorder}`}
        />
        <Button label="Gửi" variant="primary" icon={<Icon icon={SendHorizontal} />} isLoading={sending} isDisabled={!text.trim()} onClick={() => void send()} />
      </HStack>

      <ConfirmDialog
        open={recalling !== null}
        title="Thu hồi tin nhắn"
        message="Tin nhắn sẽ bị ẩn nội dung với mọi người trong cuộc trò chuyện. Không thể hoàn tác."
        confirmText="Thu hồi"
        onConfirm={() => void recall()}
        onClose={() => setRecalling(null)}
      />
      {isGroupChat && <GroupInfoDrawer conversation={conversation} myId={myId} open={showInfo} onClose={() => setShowInfo(false)} />}
      {photo && <Lightbox isOpen onOpenChange={(isOpen) => !isOpen && setPhoto(null)} media={{ src: photo, alt: 'Ảnh trong cuộc trò chuyện' }} hasZoom />}
    </VStack>
  );
}

function Bubble({ message: m, mine, showTime, onRecall, onOpenPhoto }: { message: ChatMessage; mine: boolean; showTime: boolean; onRecall: () => void; onOpenPhoto: () => void }) {
  if (m.isDeleted) {
    return (
      <span className="rounded-2xl border border-dashed border-border px-3 py-1.5 text-sm italic text-secondary">Tin nhắn đã được thu hồi</span>
    );
  }
  return (
    <div className={`group flex items-center gap-1 ${mine ? 'flex-row-reverse' : ''}`}>
      <div className={`overflow-hidden rounded-2xl text-sm ${mine ? `${accent.bg} ${accent.onBg}` : 'bg-muted text-primary'}`}>
        {m.imageUrl && (
          <button type="button" onClick={onOpenPhoto} className="block" aria-label="Xem ảnh">
            <img src={m.imageUrl} alt="Ảnh đã gửi" className="max-h-64 max-w-full object-cover" loading="lazy" />
          </button>
        )}
        {m.content && <p className={`whitespace-pre-wrap break-words px-3 py-1.5 ${mine ? accent.onBg : 'text-primary'}`}>{m.content}</p>}
      </div>
      {mine && (
        <IconButton variant="ghost" size="sm" label="Thu hồi" tooltip="Thu hồi" icon={<Icon icon={Undo2} />} className="opacity-0 transition-opacity group-hover:opacity-100 focus-visible:opacity-100" onClick={onRecall} />
      )}
      {showTime && <span className="shrink-0 text-[11px] text-secondary">{m.createdAt.slice(11, 16)}</span>}
    </div>
  );
}
