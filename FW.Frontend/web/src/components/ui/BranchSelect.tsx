import { useEffect } from 'react';
import { Selector } from '@astryxdesign/core/Selector';
import { getErrorMessage } from '@/services/apiClient';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';

/**
 * Ô chọn chi nhánh dùng chung: "Tất cả chi nhánh" + danh sách từ lookupStore (tự nạp nếu chưa có).
 * `lockedTo` đặt chi nhánh cố định (Quản lý): chỉ hiện chi nhánh đó và khóa ô chọn.
 */
export function BranchSelect({
  value,
  onChange,
  width = 220,
  lockedTo,
  allLabel = 'Tất cả chi nhánh',
}: {
  value: number | '';
  onChange: (value: number | '') => void;
  width?: number;
  lockedTo?: number | null;
  allLabel?: string;
}) {
  const { branches, load } = useLookupStore();

  useEffect(() => {
    void load().catch((err) => toast.error(getErrorMessage(err)));
  }, [load]);

  const locked = lockedTo != null;
  const options = locked
    ? branches.filter((b) => b.id === lockedTo).map((b) => ({ value: String(b.id), label: b.name }))
    : [{ value: '', label: allLabel }, ...branches.map((b) => ({ value: String(b.id), label: b.name }))];

  return (
    <Selector
      label="Chi nhánh"
      isLabelHidden
      width={width}
      options={options}
      value={locked ? String(lockedTo) : String(value)}
      onChange={(v) => onChange(v ? Number(v) : '')}
      isDisabled={locked}
    />
  );
}
