import { useEffect, useState } from 'react';
import { Card } from '@astryxdesign/core/Card';
import { Icon } from '@astryxdesign/core/Icon';
import { VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { MessagesSquare } from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';
import { useAuthStore } from '@/stores/authStore';
import { useChatStore } from '@/stores/chatStore';
import type { Conversation } from '@/types/chat';
import { ConversationList } from '../components/ConversationList';
import { MessageThread } from '../components/MessageThread';
import { NewChatModal } from '../components/NewChatModal';
import { NewGroupModal } from '../components/GroupModals';

/** Truyền thông nội bộ (UC 7.1 – 7.3): chat 1-1, nhóm chi nhánh và nhóm tùy chỉnh, tin mới tới tức thì. Danh sách phòng nằm trong chatStore (cập nhật bởi SignalR). */
export function ChatPage() {
  const myId = useAuthStore((s) => s.user?.id) ?? 0;
  const { conversations, loaded, load, upsert } = useChatStore();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [creating, setCreating] = useState(false);
  const [creatingGroup, setCreatingGroup] = useState(false);

  useEffect(() => {
    if (!loaded) void load().catch(() => undefined);
  }, [loaded, load]);

  // Mặc định mở phòng có hoạt động gần nhất; phòng đang mở biến mất (bị xóa khỏi nhóm, rời nhóm) thì chuyển sang phòng khác
  useEffect(() => {
    if (!loaded) return;
    if (selectedId != null && !conversations.some((c) => c.id === selectedId)) setSelectedId(null);
    else if (selectedId == null && conversations.length > 0) setSelectedId(conversations[0].id);
  }, [loaded, selectedId, conversations]);

  const selected = conversations.find((c) => c.id === selectedId) ?? null;

  const openConversation = (conversation: Conversation) => {
    upsert(conversation);
    setSelectedId(conversation.id);
  };

  return (
    <VStack gap={4}>
      <PageHeader title="Truyền thông nội bộ" description="Nhắn tin trực tiếp, nhóm chi nhánh và nhóm do bạn tạo. Tin nhắn mới hiện ngay, không cần tải lại trang." />
      <Card padding={0}>
        <div className="grid h-[calc(100dvh-14rem)] min-h-[480px] grid-cols-1 overflow-hidden rounded-[inherit] md:grid-cols-[320px_1fr]">
          <ConversationList conversations={conversations} loading={!loaded} activeId={selectedId} onSelect={setSelectedId} onNew={() => setCreating(true)} onNewGroup={() => setCreatingGroup(true)} />
          <div className="min-h-0 min-w-0">
            {selected ? (
              <MessageThread key={selected.id} conversation={selected} myId={myId} />
            ) : (
              <VStack hAlign="center" vAlign="center" gap={2} className="h-full text-secondary">
                <Icon icon={MessagesSquare} size="lg" color="secondary" />
                <Text type="supporting">Chọn một cuộc trò chuyện, bấm “Chat mới” hoặc “Tạo nhóm”.</Text>
              </VStack>
            )}
          </div>
        </div>
      </Card>
      <NewChatModal open={creating} onClose={() => setCreating(false)} onOpened={openConversation} />
      <NewGroupModal open={creatingGroup} onClose={() => setCreatingGroup(false)} onCreated={openConversation} />
    </VStack>
  );
}
