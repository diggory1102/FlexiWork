import { useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Modal } from '@/components/ui/Modal';
import { SearchInput } from '@/components/ui/ListCard';
import { EmployeeCell } from '@/components/ui/EmployeeCell';
import { Spinner } from '@/components/ui/Feedback';
import { Pagination } from '@/components/ui/DataTable';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/Stack';
import { useApiData } from '@/hooks/useApiData';
import { useDebounce } from '@/hooks/useDebounce';
import { chatApi } from '@/services/chatApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import type { ChatContact, Conversation } from '@/types/chat';

/** Chọn người để bắt đầu chat 1-1 (mọi nhân sự đang hoạt động trừ chính mình); đã có phòng thì mở lại phòng cũ. */
export function NewChatModal({ open, onClose, onOpened }: { open: boolean; onClose: () => void; onOpened: (conversation: Conversation) => void }) {
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [pageIndex, setPageIndex] = useState(1);
  const [busyId, setBusyId] = useState<number | null>(null);
  const { data, loading } = useApiData(() => chatApi.contacts({ search: debounced, pageIndex, pageSize: 8 }), [debounced, pageIndex], { enabled: open });

  const choose = async (contact: ChatContact) => {
    setBusyId(contact.id);
    try {
      const { data: conversation } = await chatApi.openDirect(contact.id);
      onOpened(conversation);
      onClose();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title="Chat mới" description="Chọn một người để nhắn tin." size="sm" footer={<Button label="Đóng" variant="secondary" onClick={onClose} />}>
      <VStack gap={3}>
        <SearchInput
          placeholder="Tìm theo tên hoặc mã nhân viên…"
          value={search}
          onChange={(v) => {
            setSearch(v);
            setPageIndex(1);
          }}
          width="100%"
        />
        {loading && !data && (
          <div className="flex justify-center py-8">
            <Spinner />
          </div>
        )}
        {data && data.items.length === 0 && <Text type="supporting">Không tìm thấy ai.</Text>}
        <VStack gap={1}>
          {data?.items.map((c) => (
            <button
              key={c.id}
              type="button"
              disabled={busyId !== null}
              onClick={() => void choose(c)}
              className="rounded-lg px-2 py-1.5 text-left hover:bg-muted disabled:opacity-60"
            >
              <EmployeeCell name={c.fullName} code={c.employeeCode} avatarUrl={c.avatarUrl} sub={[c.positionTitle, c.branchName].filter(Boolean).join(' · ') || null} />
            </button>
          ))}
        </VStack>
        {data && <Pagination page={data} onChange={setPageIndex} />}
      </VStack>
    </Modal>
  );
}
