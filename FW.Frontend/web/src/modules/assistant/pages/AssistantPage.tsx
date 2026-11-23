import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { MessageSquarePlus } from 'lucide-react';
import { Notice, Spinner } from '@/components/ui/Feedback';
import { PageHeader } from '@/components/ui/Layout';
import { accent } from '@/lib/accent';
import { formatDayMonth } from '@/lib/dates';
import { getErrorMessage } from '@/services/apiClient';
import { assistantApi } from '@/services/assistantApi';
import { useAuthStore } from '@/stores/authStore';
import { RoleGroups } from '@/types/auth';
import type { AssistantConversation } from '@/types/assistant';
import { AssistantThread } from '../components/AssistantThread';

/** Một lần chọn hội thoại = một "phiên" giao diện (nonce), để luồng đang chạy không bị đặt lại khi máy chủ cấp mã cho hội thoại mới. */
interface Session {
  conversationId: number | null;
  nonce: number;
}

/**
 * Trợ lý AI (UC 10.1 – 10.2): hỏi về lịch ca, công, ngày phép, đơn từ bằng lời thường và nhờ soạn đơn nghỉ / đổi ca / OT / giải trình.
 * Trợ lý chỉ tạo BẢN NHÁP; đơn chỉ được gửi khi bạn bấm "Xác nhận" trên thẻ. Quản lý / HR hỏi thêm được về đơn chờ duyệt và nhân sự cần quan tâm.
 */
export function AssistantPage() {
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const [conversations, setConversations] = useState<AssistantConversation[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [session, setSession] = useState<Session>({ conversationId: null, nonce: 0 });
  // Hội thoại đang tô sáng ở cột trái; tách khỏi `session` để hội thoại mới được cấp mã giữa chừng không làm tải lại luồng đang chạy
  const [activeId, setActiveId] = useState<number | null>(null);

  const reload = useCallback(async () => {
    try {
      setConversations((await assistantApi.conversations()).data);
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoaded(true);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  const open = (conversationId: number | null) => {
    setSession((s) => ({ conversationId, nonce: s.nonce + 1 }));
    setActiveId(conversationId);
  };

  // Hội thoại vừa tạo / có tin mới: tải lại danh sách nhưng giữ nguyên phiên đang mở
  const onConversationChanged = useCallback(
    (conversationId: number) => {
      setActiveId(conversationId);
      void reload();
    },
    [reload],
  );

  return (
    <VStack gap={4}>
      <PageHeader title="Trợ lý AI" description="Hỏi bằng lời thường về lịch ca, công, ngày phép, đơn từ; hoặc nhờ soạn đơn rồi tự mình xác nhận gửi." />
      <Card padding={0}>
        <div className="grid h-[calc(100dvh-14rem)] min-h-[480px] grid-cols-1 overflow-hidden rounded-[inherit] md:grid-cols-[300px_1fr]">
          <VStack className="hidden h-full min-h-0 border-r border-border md:flex">
            <HStack hAlign="between" vAlign="center" padding={3}>
              <Heading level={3}>Hội thoại</Heading>
              <Button label="Mới" variant="secondary" size="sm" icon={<Icon icon={MessageSquarePlus} />} onClick={() => open(null)} />
            </HStack>
            <div className="min-h-0 flex-1 overflow-y-auto">
              {!loaded && (
                <div className="flex justify-center py-10">
                  <Spinner />
                </div>
              )}
              <div className="px-3">
                <Notice title={error} />
              </div>
              {loaded && !error && conversations.length === 0 && (
                <Text type="supporting" className="block p-6 text-center">
                  Chưa có hội thoại nào. Hãy đặt câu hỏi đầu tiên!
                </Text>
              )}
              {conversations.map((c) => (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => open(c.id)}
                  aria-current={c.id === activeId}
                  className={`flex w-full flex-col gap-0.5 px-3 py-2.5 text-left hover:bg-muted ${c.id === activeId ? accent.tint : ''}`}
                >
                  <Text weight={c.id === activeId ? 'semibold' : 'medium'} maxLines={2}>
                    {c.title}
                  </Text>
                  <Text type="supporting">{formatDayMonth(c.updatedAt.slice(0, 10))} · {c.updatedAt.slice(11, 16)}</Text>
                </button>
              ))}
            </div>
          </VStack>
          <div className="min-h-0 min-w-0">
            <AssistantThread
              key={session.nonce}
              initialConversationId={session.conversationId}
              isManager={hasAnyRole(RoleGroups.Managers)}
              onConversationChanged={onConversationChanged}
            />
          </div>
        </div>
      </Card>
    </VStack>
  );
}
