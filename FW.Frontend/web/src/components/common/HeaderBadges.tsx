import { useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Popover } from '@astryxdesign/core/Popover';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { Bell, CheckCheck, MessageSquare } from 'lucide-react';
import { accent } from '@/lib/accent';
import { notificationIcons, notificationTarget, timeAgo } from '@/modules/notifications/notificationUi';
import { useChatStore } from '@/stores/chatStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';
import { getErrorMessage } from '@/services/apiClient';
import { RoleGroups } from '@/types/auth';
import type { AppNotification } from '@/types/notification';

/** Huy hiệu số đếm đè lên góc biểu tượng (ẩn khi bằng 0, hiện "99+" khi quá lớn). */
function WithCount({ count, children }: { count: number; children: ReactNode }) {
  return (
    <span className="relative inline-flex">
      {children}
      {count > 0 && (
        <span
          aria-hidden="true"
          className="pointer-events-none absolute -right-0.5 -top-0.5 min-w-4 rounded-full bg-red-600 px-1 text-center text-[10px] font-semibold leading-4 text-white"
        >
          {count > 99 ? '99+' : count}
        </span>
      )}
    </span>
  );
}

/** Biểu tượng chat trên thanh đầu trang: huy hiệu tin chưa đọc, bấm để mở màn hình Chat (chỉ vai trò quản lý). */
export function ChatButton() {
  const navigate = useNavigate();
  const canChat = useAuthStore((s) => s.hasAnyRole(RoleGroups.Managers));
  const unread = useChatStore((s) => s.totalUnread);
  if (!canChat) return null;
  return (
    <WithCount count={unread}>
      <IconButton variant="ghost" label={unread > 0 ? `Tin nhắn, ${unread} chưa đọc` : 'Tin nhắn'} tooltip="Tin nhắn" icon={<Icon icon={MessageSquare} />} onClick={() => navigate('/chat')} />
    </WithCount>
  );
}

/** Chuông thông báo: huy hiệu chưa đọc và khung xem nhanh các thông báo mới nhất. */
export function NotificationBell() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const { unread, recent, markRead, markAllRead } = useNotificationStore();
  const isAdminOrHr = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));

  const openItem = (n: AppNotification) => {
    setOpen(false);
    void markRead(n.id).catch((err) => toast.error(getErrorMessage(err)));
    const target = notificationTarget(n, isAdminOrHr);
    if (target) navigate(target);
  };

  return (
    <WithCount count={unread}>
      <Popover
        isOpen={open}
        onOpenChange={setOpen}
        placement="below"
        alignment="end"
        width={380}
        padding={0}
        label="Thông báo"
        content={
          <VStack>
            <HStack hAlign="between" vAlign="center" padding={3} className="border-b border-border">
              <Heading level={3}>Thông báo</Heading>
              <Button
                label="Đọc tất cả"
                variant="ghost"
                size="sm"
                icon={<Icon icon={CheckCheck} />}
                isDisabled={unread === 0}
                onClick={() => void markAllRead().catch((err) => toast.error(getErrorMessage(err)))}
              />
            </HStack>
            <div className="max-h-96 overflow-y-auto">
              {recent.length === 0 ? (
                <Text type="supporting" className="block p-6 text-center">
                  Chưa có thông báo nào.
                </Text>
              ) : (
                recent.map((n) => <NotificationRow key={n.id} notification={n} onOpen={openItem} />)
              )}
            </div>
            <div className="border-t border-border p-2">
              <Button
                label="Xem tất cả thông báo"
                variant="ghost"
                width="100%"
                onClick={() => {
                  setOpen(false);
                  navigate('/notifications');
                }}
              />
            </div>
          </VStack>
        }
      >
        <IconButton variant="ghost" label={unread > 0 ? `Thông báo, ${unread} chưa đọc` : 'Thông báo'} tooltip="Thông báo" icon={<Icon icon={Bell} />} />
      </Popover>
    </WithCount>
  );
}

/** Một dòng thông báo (dùng cả trong khung chuông và trang Thông báo). */
export function NotificationRow({ notification: n, onOpen }: { notification: AppNotification; onOpen: (n: AppNotification) => void }) {
  return (
    <button
      type="button"
      onClick={() => onOpen(n)}
      className={`flex w-full items-start gap-3 border-b border-border px-4 py-3 text-left last:border-b-0 hover:bg-muted ${n.isRead ? '' : accent.tintSoft}`}
    >
      <span className={`mt-0.5 shrink-0 ${accent.text}`}>
        <Icon icon={notificationIcons[n.type] ?? Bell} />
      </span>
      <VStack gap={0.5} className="min-w-0 flex-1">
        <Text weight={n.isRead ? 'medium' : 'semibold'} maxLines={2}>
          {n.title}
        </Text>
        <Text type="supporting" maxLines={2}>
          {n.content}
        </Text>
        <Text type="supporting">{timeAgo(n.createdAt)}</Text>
      </VStack>
      {!n.isRead && <span aria-label="Chưa đọc" className={`mt-2 size-2 shrink-0 rounded-full ${accent.bg}`} />}
    </button>
  );
}
