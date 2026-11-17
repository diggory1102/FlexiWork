import { useEffect, useState } from 'react';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/Stack';
import { Toolbar } from '@/components/ui/Layout';
import { Badge } from '@/components/ui/Feedback';
import { BranchSelect } from '@/components/ui/BranchSelect';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { EmployeeCell } from '@/components/ui/EmployeeCell';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { RowActions } from '@/components/ui/RowActions';
import { useDebounce } from '@/hooks/useDebounce';
import { usePagedList } from '@/hooks/usePagedList';
import { payrollConfigApi } from '@/services/payrollApi';
import { toast } from '@/stores/toastStore';
import { employmentTypeLabels, formatCurrency } from '@/lib/format';
import type { EmployeePay, PayrollEmployeeFilter } from '@/types/payroll';
import { EmployeePayDrawer } from './EmployeePayDrawer';

const typeOptions = [
  { value: '', label: 'Mọi loại hợp đồng' },
  { value: 'full_time', label: employmentTypeLabels.full_time },
  { value: 'part_time', label: employmentTypeLabels.part_time },
];
const configuredOptions = [
  { value: '', label: 'Mọi trạng thái' },
  { value: 'true', label: 'Đã đủ thông tin lương' },
  { value: 'false', label: 'Còn thiếu thông tin' },
];

/** Lương từng nhân viên: lương cơ bản (toàn thời gian) hoặc đơn giá giờ (bán thời gian) và phụ cấp được hưởng. Thiếu thông tin thì chưa tính lương được. */
export function EmployeePayTab() {
  const { query, setQuery, page, rows, loading, error, reload } = usePagedList<EmployeePay, PayrollEmployeeFilter>(payrollConfigApi.employees, { pageSize: 20 });
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [selected, setSelected] = useState<EmployeePay | null>(null);

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const columns: Column<EmployeePay>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      width: 250,
      render: (e) => <EmployeeCell name={e.fullName} code={e.employeeCode} avatarUrl={e.avatarUrl} sub={e.branchName} />,
    },
    {
      key: 'type',
      header: 'Hợp đồng',
      width: 150,
      render: (e) => (
        <VStack gap={0.5}>
          <Text>{employmentTypeLabels[e.employmentType]}</Text>
          {e.positionTitle && <Text type="supporting">{e.positionTitle}</Text>}
        </VStack>
      ),
    },
    {
      key: 'salary',
      header: 'Lương cơ bản',
      align: 'end',
      render: (e) => (e.baseSalary ? <Text hasTabularNumbers>{formatCurrency(e.baseSalary)}</Text> : <Text type="supporting">—</Text>),
    },
    {
      key: 'rate',
      header: 'Đơn giá giờ',
      align: 'end',
      render: (e) =>
        e.effectiveHourlyRate > 0 ? (
          <VStack gap={0.5} hAlign="end">
            <Text hasTabularNumbers>{formatCurrency(e.effectiveHourlyRate)}</Text>
            <Text type="supporting">{e.hourlyRate ? 'Riêng' : 'Theo chức vụ'}</Text>
          </VStack>
        ) : (
          <Text type="supporting">—</Text>
        ),
    },
    { key: 'allowances', header: 'Phụ cấp', align: 'end', render: (e) => (e.allowanceCount > 0 ? e.allowanceCount : <Text type="supporting">—</Text>) },
    {
      key: 'status',
      header: 'Trạng thái',
      width: 150,
      render: (e) => <Badge tone={e.isConfigured ? 'green' : 'amber'}>{e.isConfigured ? 'Đủ thông tin' : 'Thiếu thông tin'}</Badge>,
    },
    { key: 'actions', header: '', width: 70, align: 'end', render: (e) => <RowActions onEdit={() => setSelected(e)} /> },
  ];

  return (
    <>
      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm theo tên, mã nhân viên…" value={search} onChange={setSearch} />
          <BranchSelect value={query.branchId ?? ''} onChange={(branchId) => setQuery({ branchId })} />
          <Selector
            label="Hợp đồng"
            isLabelHidden
            width={190}
            options={typeOptions}
            value={query.employmentType ?? ''}
            onChange={(v) => setQuery({ employmentType: (v ?? '') as PayrollEmployeeFilter['employmentType'] })}
          />
          <Selector
            label="Trạng thái"
            isLabelHidden
            width={210}
            options={configuredOptions}
            value={query.configured === '' || query.configured === undefined ? '' : String(query.configured)}
            onChange={(v) => setQuery({ configured: v ? v === 'true' : '' })}
          />
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(e) => e.userId} loading={loading} emptyText="Không có nhân viên nào" onRowClick={setSelected} />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <EmployeePayDrawer employee={selected} onClose={() => setSelected(null)} onChanged={() => void reload()} />
    </>
  );
}
