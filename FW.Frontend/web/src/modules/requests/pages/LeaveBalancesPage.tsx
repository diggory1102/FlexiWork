import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Avatar, PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { BranchSelect } from '@/components/ui/BranchSelect';
import { Badge } from '@/components/ui/Feedback';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { leaveBalanceApi } from '@/services/requestApi';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';
import { RoleGroups } from '@/types/auth';
import type { LeaveBalanceFilter, LeaveBalanceRow } from '@/types/requests';
import { LeaveBalanceModal } from '../components/LeaveBalanceModal';
import { days } from '../requestUi';

/**
 * Quỹ phép năm (UC 5.5): số ngày phép của từng nhân viên theo năm. Admin/HR xem mọi chi nhánh và cộng / trừ kèm lý do;
 * Quản lý chỉ xem nhân viên chi nhánh mình. Nhân viên chưa có quỹ riêng hiện số ngày mặc định.
 */
export function LeaveBalancesPage() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const currentYear = new Date().getFullYear();
  const { query, setQuery, page, rows, loading, error, reload } = usePagedList<LeaveBalanceRow, LeaveBalanceFilter>(leaveBalanceApi.list, {
    pageSize: 20,
    year: currentYear,
  });

  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [selected, setSelected] = useState<LeaveBalanceRow | null>(null);
  const [open, setOpen] = useState(false);

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const openRow = (row: LeaveBalanceRow) => {
    setSelected(row);
    setOpen(true);
  };

  const yearOptions = [currentYear - 1, currentYear, currentYear + 1].map((y) => ({ value: String(y), label: `Năm ${y}` }));

  const columns: Column<LeaveBalanceRow>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      width: 260,
      render: (r) => (
        <HStack gap={3} vAlign="center">
          <Avatar name={r.fullName} url={r.avatarUrl} size={36} />
          <VStack gap={0.5} className="min-w-0">
            <Text weight="medium" maxLines={1}>
              {r.fullName}
            </Text>
            <Text type="supporting" maxLines={1}>
              <span className="font-mono">{r.employeeCode}</span>
              {isCompanyWide && r.branchName && <> · {r.branchName}</>}
            </Text>
          </VStack>
        </HStack>
      ),
    },
    { key: 'total', header: 'Tổng quỹ', align: 'end', render: (r) => <Text hasTabularNumbers>{days(r.totalDays)}</Text> },
    { key: 'used', header: 'Đã dùng', align: 'end', render: (r) => <Text hasTabularNumbers>{days(r.usedDays)}</Text> },
    {
      key: 'pending',
      header: 'Chờ duyệt',
      align: 'end',
      render: (r) => (r.pendingDays > 0 ? <Badge tone="amber">{days(r.pendingDays)}</Badge> : <Text type="supporting">—</Text>),
    },
    {
      key: 'available',
      header: 'Còn xin được',
      align: 'end',
      render: (r) => (
        <Text weight="semibold" hasTabularNumbers>
          {days(r.availableDays)}
        </Text>
      ),
    },
    {
      key: 'default',
      header: '',
      width: 190,
      render: (r) => r.isDefault && <Badge tone="gray">Số ngày mặc định</Badge>,
    },
    {
      key: 'actions',
      header: '',
      width: 130,
      align: 'end',
      render: (r) => (
        <Button label={isCompanyWide ? 'Điều chỉnh' : 'Xem'} size="sm" variant={isCompanyWide ? 'secondary' : 'ghost'} onClick={() => openRow(r)} />
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Quỹ phép năm"
        description={
          isCompanyWide
            ? 'Số ngày phép của từng nhân viên. Cộng / trừ thủ công kèm lý do — mọi lần điều chỉnh đều được lưu lịch sử. Đơn nghỉ trừ phép được duyệt sẽ tự trừ vào đây.'
            : 'Số ngày phép của nhân viên chi nhánh bạn quản lý. Chỉ Quản trị viên / HR được điều chỉnh quỹ phép.'
        }
      />
      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm tên hoặc mã nhân viên…" value={search} onChange={setSearch} />
          <Selector
            label="Năm"
            isLabelHidden
            width={150}
            options={yearOptions}
            value={String(query.year ?? currentYear)}
            onChange={(v) => v && setQuery({ year: Number(v) })}
          />
          {isCompanyWide && (
            <BranchSelect width={220} value={query.branchId ?? ''} onChange={(branchId) => setQuery({ branchId })} />
          )}
          <Text type="supporting" size="sm">
            Đơn vị: ngày
          </Text>
        </Toolbar>
        <DataTable
          columns={columns}
          rows={rows}
          rowKey={(r) => r.userId}
          loading={loading}
          emptyText="Không tìm thấy nhân viên"
          onRowClick={openRow}
        />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <LeaveBalanceModal row={selected} open={open} canAdjust={isCompanyWide} onClose={() => setOpen(false)} onChanged={() => void reload()} />
    </>
  );
}
