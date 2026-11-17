import { useMemo, useState } from 'react';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { SquarePen, Users, UsersRound } from 'lucide-react';
import { Avatar } from '@/components/ui/Layout';
import { SearchInput } from '@/components/ui/ListCard';
import { Spinner } from '@/components/ui/Feedback';
import { accent } from '@/lib/accent';
import type { Conversation } from '@/types/chat';
import { shortTime } from '../chatUi';

/** Ảnh đại diện của phòng: chat 1-1 là ảnh người kia, nhóm là biểu tượng nhóm. */
export function ConversationAvatar({ conversation, size = 40 }: { conversation: Conversation; size?: number }) {
  if (conversation.type === 'group') {
    return (
      <span className={`inline-flex shrink-0 items-center justify-center rounded-full ${accent.tint} ${accent.text}`} style={{ width: size, height: size }}>
        <Icon icon={Users} />
      </span>
    );
  }
  return <Avatar name={conversation.title} url={conversation.avatarUrl} size={size} />;
}

/** Cột trái: ô tìm phòng, nút chat mới và danh sách phòng (mới nhất trước, kèm số tin chưa đọc). */
export function ConversationList({
  conversations,
  loading,
  activeId,
  onSelect,
  onNew,
  onNewGroup,
}: {
  conversations: Conversation[];
  loading: boolean;
  activeId: number | null;
  onSelect: (id: number) => void;
  onNew: () => void;
  onNewGroup: () => void;
}) {
  const [search, setSearch] = useState('');
  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return keyword ? conversations.filter((c) => c.title.toLowerCase().includes(keyword)) : conversations;
  }, [conversations, search]);

  return (
    <VStack className="h-full min-h-0 border-r border-border">
      <HStack hAlign="between" vAlign="center" padding={3}>
        <Heading level={3}>Tin nhắn</Heading>
        <HStack gap={1}>
          <IconButton label="Chat mới" tooltip="Chat mới (1-1)" variant="secondary" icon={<Icon icon={SquarePen} />} onClick={onNew} />
          <IconButton label="Tạo nhóm" tooltip="Tạo nhóm chat" variant="secondary" icon={<Icon icon={UsersRound} />} onClick={onNewGroup} />
        </HStack>
      </HStack>
      <div className="px-3 pb-3">
        <SearchInput placeholder="Tìm cuộc trò chuyện…" value={search} onChange={setSearch} width="100%" />
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {loading && conversations.length === 0 && (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        )}
        {!loading && filtered.length === 0 && (
          <Text type="supporting" className="block p-6 text-center">
            {search ? 'Không tìm thấy cuộc trò chuyện nào.' : 'Chưa có cuộc trò chuyện nào. Bấm “Chat mới” hoặc “Tạo nhóm” để bắt đầu.'}
          </Text>
        )}
        {filtered.map((c) => (
          <button
            key={c.id}
            type="button"
            onClick={() => onSelect(c.id)}
            aria-current={c.id === activeId}
            className={`flex w-full items-center gap-3 px-3 py-2.5 text-left hover:bg-muted ${c.id === activeId ? accent.tint : ''}`}
          >
            <ConversationAvatar conversation={c} />
            <VStack gap={0.5} className="min-w-0 flex-1">
              <HStack hAlign="between" vAlign="center" gap={2}>
                <Text weight={c.unreadCount > 0 ? 'semibold' : 'medium'} maxLines={1}>
                  {c.title}
                </Text>
                {c.lastMessage && (
                  <Text type="supporting" className="shrink-0">
                    {shortTime(c.lastMessage.createdAt)}
                  </Text>
                )}
              </HStack>
              <HStack hAlign="between" vAlign="center" gap={2}>
                <Text type="supporting" maxLines={1} className="min-w-0 flex-1">
                  {c.lastMessage ? `${c.type === 'group' ? `${c.lastMessage.senderName.split(' ').pop()}: ` : ''}${c.lastMessage.preview || '…'}` : 'Chưa có tin nhắn'}
                </Text>
                {c.unreadCount > 0 && (
                  <span className={`min-w-5 shrink-0 rounded-full px-1.5 text-center text-xs font-semibold leading-5 ${accent.bg} ${accent.onBg}`}>
                    {c.unreadCount > 99 ? '99+' : c.unreadCount}
                  </span>
                )}
              </HStack>
            </VStack>
          </button>
        ))}
      </div>
    </VStack>
  );
}
