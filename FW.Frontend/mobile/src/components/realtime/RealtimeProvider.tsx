import { useEffect } from 'react';
import { HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { createHubConnection } from '@/services/signalrClient';
import { emitChat, useChatStore } from '@/stores/chatStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { useBannerStore } from '@/stores/bannerStore';
import { useAuthStore } from '@/stores/authStore';
import type { ChatMessage, ConversationReadEvent, MessageDeletedEvent } from '@/types/chat';

const quietly = (promise: Promise<unknown>) => void promise.catch(() => undefined);

async function start(connection: HubConnection, isDisposed: () => boolean) {
  try {
    await connection.start();
  } catch {
    // Lần start đầu thất bại thì withAutomaticReconnect không áp dụng → tự thử lại, trừ khi provider đã bị gỡ
    if (isDisposed()) return;
    setTimeout(() => !isDisposed() && connection.state === HubConnectionState.Disconnected && void start(connection, isDisposed), 10_000);
  }
}

/**
 * Giữ hai kết nối SignalR của người dùng đang đăng nhập: thông báo (/hubs/notifications) và chat (/hubs/chat).
 * Cập nhật huy hiệu chưa đọc, danh sách phòng chat và hiện banner khi app đang mở; tự nạp lại số liệu khi kết nối lại.
 * Không vẽ gì — đặt một lần trong layout gốc.
 */
export function RealtimeProvider() {
  const token = useAuthStore((s) => s.accessToken);
  const userId = useAuthStore((s) => s.user?.id);
  const ready = useAuthStore((s) => !!s.user && !s.user.mustChangePassword);

  useEffect(() => {
    if (!token || userId == null || !ready) return;
    const notifications = createHubConnection('notifications');
    const chat = createHubConnection('chat');

    const refreshNotifications = () => quietly(useNotificationStore.getState().refresh());
    const refreshChat = () => quietly(useChatStore.getState().load());

    // Tin báo SignalR không kèm mã thông báo và trạng thái đọc → nạp lại số chưa đọc từ máy chủ
    notifications.on('ReceiveNotification', (n: { title: string; content?: string }) => {
      useBannerStore.getState().show({ title: n.title, body: n.content, href: '/notifications' });
      refreshNotifications();
    });
    notifications.onreconnected(refreshNotifications);

    chat.on('ReceiveMessage', (message: ChatMessage) => {
      const store = useChatStore.getState();
      store.applyMessage(message, userId);
      emitChat({ kind: 'message', message });
      if (message.senderId !== userId && store.activeId !== message.conversationId) {
        const conversation = store.conversations.find((c) => c.id === message.conversationId);
        useBannerStore.getState().show({
          title: conversation?.type === 'group' ? `${conversation.title} · ${message.senderName}` : message.senderName,
          body: message.imageUrl ? '[Hình ảnh]' : (message.content ?? ''),
          href: { pathname: '/chat/[id]', params: { id: String(message.conversationId) } },
        });
      }
    });
    chat.on('MessageDeleted', (event: MessageDeletedEvent) => {
      emitChat({ kind: 'deleted', event });
      refreshChat(); // xem trước tin cuối có thể đã đổi
    });
    // Nhóm thay đổi (tạo mới, đổi tên, thêm / xóa thành viên, có người rời): nạp lại danh sách phòng
    chat.on('ConversationUpdated', refreshChat);
    chat.on('ConversationRead', (event: ConversationReadEvent) => useChatStore.getState().setUnread(event.conversationId, event.unreadCount));
    chat.onreconnected(refreshChat);

    let disposed = false;
    const isDisposed = () => disposed;
    refreshNotifications();
    refreshChat();
    const startTimer = setTimeout(() => {
      void start(notifications, isDisposed);
      void start(chat, isDisposed);
    }, 50);

    return () => {
      disposed = true;
      clearTimeout(startTimer);
      void notifications.stop();
      void chat.stop();
      useNotificationStore.getState().reset();
      useChatStore.getState().reset();
      useBannerStore.getState().hide();
    };
  }, [token, userId, ready]);

  return null;
}
