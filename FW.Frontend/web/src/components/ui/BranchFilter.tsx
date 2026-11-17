import { useState } from 'react';
import { useAuthStore } from '@/stores/authStore';
import { RoleGroups } from '@/types/auth';
import { BranchSelect } from './BranchSelect';

/**
 * Trạng thái bộ lọc chi nhánh cho các màn hình kiểu báo cáo / bảng công: Admin / HR chọn tất cả hoặc một chi nhánh,
 * Quản lý luôn bị khóa ở chi nhánh của mình (máy chủ cũng kiểm tra lại phạm vi).
 */
export function useBranchFilter() {
  const user = useAuthStore((s) => s.user);
  const companyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const [value, setValue] = useState<number | ''>('');

  return {
    companyWide,
    /** Chi nhánh Quản lý bị khóa vào (null với Admin / HR). */
    lockedBranchId: companyWide ? null : (user?.branchId ?? null),
    value,
    setValue,
    /** Giá trị gửi lên API: Quản lý không cần gửi (máy chủ tự lấy chi nhánh của họ). */
    apiBranchId: (companyWide ? value : '') as number | '',
  };
}

export type BranchFilterState = ReturnType<typeof useBranchFilter>;

export function BranchSelector({ filter, width }: { filter: BranchFilterState; width?: number }) {
  return <BranchSelect value={filter.value} onChange={filter.setValue} lockedTo={filter.lockedBranchId} width={width} />;
}
