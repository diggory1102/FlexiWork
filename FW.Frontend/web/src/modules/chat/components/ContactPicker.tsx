import { useState } from 'react';
import { Icon } from '@astryxdesign/core/Icon';
import { VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Check, X } from 'lucide-react';
import { EmployeeCell } from '@/components/ui/EmployeeCell';
import { Spinner } from '@/components/ui/Feedback';
import { Pagination } from '@/components/ui/DataTable';
import { SearchInput } from '@/components/ui/ListCard';
import { useApiData } from '@/hooks/useApiData';
import { useDebounce } from '@/hooks/useDebounce';
import { accent } from '@/lib/accent';
import { chatApi } from '@/services/chatApi';
import type { ChatContact } from '@/types/chat';

/**
 * Chọn nhiều người từ danh bạ (tìm kiếm, phân trang): danh sách đã chọn hiện thành thẻ có nút bỏ chọn và được giữ nguyên khi chuyển trang.
 * `excludeIds` là những người đã ở trong nhóm — hiện mờ và không chọn được.
 */
export function ContactPicker({
  selected,
  onChange,
  excludeIds = [],
}: {
  selected: ChatContact[];
  onChange: (next: ChatContact[]) => void;
  excludeIds?: number[];
}) {
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [pageIndex, setPageIndex] = useState(1);
  const { data, loading } = useApiData(() => chatApi.contacts({ search: debounced, pageIndex, pageSize: 6 }), [debounced, pageIndex]);

  const toggle = (contact: ChatContact) =>
    onChange(selected.some((c) => c.id === contact.id) ? selected.filter((c) => c.id !== contact.id) : [...selected, contact]);

  return (
    <VStack gap={3}>
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5" aria-label="Người đã chọn">
          {selected.map((c) => (
            <span key={c.id} className="inline-flex items-center gap-1 rounded-full bg-muted py-0.5 pl-2.5 pr-1 text-sm">
              {c.fullName}
              <button type="button" aria-label={`Bỏ chọn ${c.fullName}`} onClick={() => toggle(c)} className="rounded-full p-0.5 hover:bg-border">
                <X className="size-3.5" />
              </button>
            </span>
          ))}
        </div>
      )}
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
        <div className="flex justify-center py-6">
          <Spinner />
        </div>
      )}
      {data && data.items.length === 0 && <Text type="supporting">Không tìm thấy ai.</Text>}
      <VStack gap={0.5}>
        {data?.items.map((c) => {
          const isSelected = selected.some((x) => x.id === c.id);
          const already = excludeIds.includes(c.id);
          return (
            <button
              key={c.id}
              type="button"
              disabled={already}
              aria-pressed={isSelected}
              onClick={() => toggle(c)}
              className={`flex items-center justify-between gap-3 rounded-lg px-2 py-1.5 text-left hover:bg-muted disabled:opacity-50 ${isSelected ? accent.tint : ''}`}
            >
              <EmployeeCell name={c.fullName} code={c.employeeCode} avatarUrl={c.avatarUrl} sub={already ? 'Đã trong nhóm' : [c.positionTitle, c.branchName].filter(Boolean).join(' · ') || null} />
              {isSelected && <Icon icon={Check} color="accent" />}
            </button>
          );
        })}
      </VStack>
      {data && <Pagination page={data} onChange={setPageIndex} />}
    </VStack>
  );
}
