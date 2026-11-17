import { useEffect } from 'react';
import { HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { createHubConnection } from '@/services/signalrClient';
import { emitChat, useChatStore } from '@/stores/chatStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';
import type { ChatMessage, ConversationReadEvent, MessageDeletedEvent } from '@/types/chat';

const quietly = (promise: Promise<unknown>) => void promise.catch(() => undefined);

async function start(connection: HubConnection, isDisposed: () => boolean) {
  try {
    await connection.start();
  } catch {
    // Lần start đầu thất bại thì withAutomaticReconnect không áp dụng → tự thử lại, trừ khi bridge đã bị gỡ
    if (isDisposed()) return;
    setTimeout(() => !isDisposed() && connection.state === HubConnectionState.Disconnected && void start(connection, isDisposed), 10_000);
  }
}

/**
 * Giữ hai kết nối SignalR của người dùng đang đăng nhập: thông báo (/hubs/notifications) và chat (/hubs/chat).
 * Cập nhật huy hiệu chưa đọc, danh sách phòng chat và báo bằng toast; tự nạp lại số liệu khi kết nối lại.
 * Không hiển thị gì — đặt một lần trong khung Admin.
 */
export function RealtimeBridge() {
  const token = useAuthStore((s) => s.accessToken);
  const userId = useAuthStore((s) => s.user?.id);

  useEffect(() => {
    if (!token || userId == null) return;
    const notifications = createHubConnection('notifications');
    const chat = createHubConnection('chat');

    const refreshNotifications = () => quietly(useNotificationStore.getState().refresh());
    const refreshChat = () => quietly(useChatStore.getState().load());

    // Tin báo SignalR không kèm mã thông báo và trạng thái đọc → nạp lại từ máy chủ để có bản ghi đầy đủ
    notifications.on('ReceiveNotification', (n: { title: string }) => {
      toast.info(n.title);
      refreshNotifications();
    });
    notifications.onreconnected(refreshNotifications);

    chat.on('ReceiveMessage', (message: ChatMessage) => {
      const store = useChatStore.getState();
      store.applyMessage(message, userId);
      emitChat({ kind: 'message', message });
      if (message.senderId !== userId && store.activeId !== message.conversationId) {
        toast.info(`${message.senderName}: ${message.imageUrl ? '[Hình ảnh]' : (message.content ?? '')}`);
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
    // Trì hoãn một nhịp: React StrictMode (dev) gỡ rồi gắn lại ngay, tránh hủy kết nối đang thương lượng
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
    };
  }, [token, userId]);

  return null;
}
