import { useEffect, useState } from 'react';
import { Selector } from '@astryxdesign/core/Selector';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { RoleGroups } from '@/types/auth';

/**
 * Trạng thái bộ lọc chi nhánh dùng chung cho các màn hình quản lý: Admin / HR chọn tất cả hoặc một chi nhánh,
 * Quản lý luôn bị khóa ở chi nhánh của mình (máy chủ cũng kiểm tra lại phạm vi).
 */
export function useBranchFilter() {
  const user = useAuthStore((s) => s.user);
  const companyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const { branches, load } = useLookupStore();
  const [value, setValue] = useState('');

  useEffect(() => {
    void load().catch(() => undefined);
  }, [load]);

  const visible = companyWide ? branches : branches.filter((b) => b.id === user?.branchId);
  const options = [
    ...(companyWide ? [{ value: '', label: 'Tất cả chi nhánh' }] : []),
    ...visible.map((b) => ({ value: String(b.id), label: b.name })),
  ];

  return {
    companyWide,
    branches,
    options,
    value: companyWide ? value : String(user?.branchId ?? ''),
    setValue,
    /** Giá trị gửi lên API: Quản lý không cần gửi (máy chủ tự lấy chi nhánh của họ). */
    apiBranchId: (companyWide && value ? Number(value) : '') as number | '',
  };
}

export type BranchFilterState = ReturnType<typeof useBranchFilter>;

export function BranchSelector({ filter, width = 220 }: { filter: BranchFilterState; width?: number }) {
  return (
    <Selector
      label="Chi nhánh"
      isLabelHidden
      width={width}
      options={filter.options}
      value={filter.value}
      onChange={(v) => filter.setValue(v ?? '')}
      isDisabled={!filter.companyWide}
    />
  );
}
