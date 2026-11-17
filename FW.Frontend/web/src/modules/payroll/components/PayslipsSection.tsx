import { useEffect, useState } from 'react';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/Stack';
import { Toolbar } from '@/components/ui/Layout';
import { Badge } from '@/components/ui/Feedback';
import { BranchSelect } from '@/components/ui/BranchSelect';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { EmployeeCell } from '@/components/ui/EmployeeCell';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { useDebounce } from '@/hooks/useDebounce';
import { usePagedList } from '@/hooks/usePagedList';
import { payrollApi } from '@/services/payrollApi';
import { toast } from '@/stores/toastStore';
import { formatCurrency, formatNumber } from '@/lib/format';
import type { Payslip, PayslipFilter, PayslipStatus } from '@/types/payroll';
import { payslipStatusLabels, payslipStatusTone } from '../payrollUi';
import { PayslipDetailDrawer } from './PayslipDetailDrawer';

type Query = PayslipFilter & { periodId: number };
type StatusTab = PayslipStatus | 'all';

/** Danh sách phiếu lương của một kỳ (gắn với kỳ qua `key` ở nơi dùng): lọc chi nhánh / trạng thái gửi / từ khóa, bấm dòng xem chi tiết. */
export function PayslipsSection({ periodId, finalized, onChanged }: { periodId: number; finalized: boolean; onChanged: () => void }) {
  const { setQuery, page, rows, loading, error, reload } = usePagedList<Payslip, Query>((q) => payrollApi.payslips(q.periodId, q), { pageSize: 20, periodId });
  const [tab, setTab] = useState<StatusTab>('all');
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [branchId, setBranchId] = useState<number | ''>('');
  const [selected, setSelected] = useState<Payslip | null>(null);

  useEffect(() => setQuery({ search: debounced, branchId, status: tab === 'all' ? '' : tab }), [debounced, branchId, tab, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const columns: Column<Payslip>[] = [
    { key: 'employee', header: 'Nhân viên', width: 250, render: (p) => <EmployeeCell name={p.employee.fullName} code={p.employee.employeeCode} avatarUrl={p.employee.avatarUrl} sub={p.branchName} /> },
    {
      key: 'work',
      header: 'Công / giờ',
      align: 'end',
      render: (p) => (
        <VStack gap={0.5} hAlign="end">
          <Text hasTabularNumbers>{formatNumber(p.workDays)} công</Text>
          <Text type="supporting">
            {formatNumber(p.workHours)} giờ{p.overtimeHours > 0 ? ` · OT ${formatNumber(p.overtimeHours)}` : ''}
          </Text>
        </VStack>
      ),
    },
    { key: 'basic', header: 'Lương', align: 'end', render: (p) => <Text hasTabularNumbers>{formatCurrency(p.basicPay)}</Text> },
    { key: 'allowance', header: 'Phụ cấp', align: 'end', render: (p) => (p.allowanceTotal ? <Text hasTabularNumbers>{formatCurrency(p.allowanceTotal)}</Text> : <Text type="supporting">—</Text>) },
    { key: 'ot', header: 'OT', align: 'end', render: (p) => (p.overtimePay ? <Text hasTabularNumbers>{formatCurrency(p.overtimePay)}</Text> : <Text type="supporting">—</Text>) },
    { key: 'penalty', header: 'Phạt', align: 'end', render: (p) => (p.penaltyTotal ? <Text hasTabularNumbers>{formatCurrency(-Math.abs(p.penaltyTotal))}</Text> : <Text type="supporting">—</Text>) },
    {
      key: 'net',
      header: 'Thực nhận',
      align: 'end',
      render: (p) => (
        <Text weight="semibold" hasTabularNumbers>
          {formatCurrency(p.netPay)}
        </Text>
      ),
    },
    {
      key: 'status',
      header: 'Email',
      width: 120,
      render: (p) => <Badge tone={payslipStatusTone[p.status]}>{payslipStatusLabels[p.status]}</Badge>,
    },
  ];

  return (
    <>
      <ListCard>
        <Toolbar>
          {finalized && (
            <TabList value={tab} onChange={(v) => setTab(v as StatusTab)}>
              <Tab value="all" label="Tất cả" />
              <Tab value="draft" label="Chưa gửi" />
              <Tab value="sent" label="Đã gửi" />
            </TabList>
          )}
          <SearchInput placeholder="Tìm theo tên hoặc mã nhân viên…" value={search} onChange={setSearch} />
          <BranchSelect value={branchId} onChange={setBranchId} />
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(p) => p.id} loading={loading} emptyText="Chưa có phiếu lương nào" onRowClick={setSelected} />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <PayslipDetailDrawer
        payslip={selected}
        onClose={() => setSelected(null)}
        onSent={() => {
          void reload();
          onChanged();
        }}
      />
    </>
  );
}
