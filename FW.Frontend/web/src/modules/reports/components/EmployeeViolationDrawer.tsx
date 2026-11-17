import { VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Badge, type BadgeTone, Notice, Spinner } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { useApiData } from '@/hooks/useApiData';
import { reportApi } from '@/services/reportApi';
import { formatCurrency, formatDate } from '@/lib/format';
import { formatMinutes } from '@/lib/dates';
import type { EmployeeViolation, ViolationItem, ViolationType } from '@/types/reports';

const typeLabels: Record<ViolationType, string> = { late: 'Đi muộn', early_leave: 'Về sớm', absent: 'Vắng mặt' };
const typeTones: Record<ViolationType, BadgeTone> = { late: 'amber', early_leave: 'blue', absent: 'red' };

/** Ngăn chi tiết: từng ca vi phạm của một nhân viên trong khoảng ngày đang xem (ngày, ca, loại, số phút, tiền phạt, công bị trừ). */
export function EmployeeViolationDrawer({
  employee,
  fromDate,
  toDate,
  onClose,
}: {
  employee: EmployeeViolation | null;
  fromDate: string;
  toDate: string;
  onClose: () => void;
}) {
  const userId = employee?.userId;
  const { data: detail, loading, error } = useApiData(() => reportApi.employeeViolations(userId!, { fromDate, toDate }), [userId, fromDate, toDate], {
    enabled: userId != null,
    toastError: false,
  });

  const columns: Column<ViolationItem>[] = [
    { key: 'date', header: 'Ngày', width: 110, render: (i) => formatDate(i.workDate) },
    {
      key: 'shift',
      header: 'Ca',
      render: (i) => (
        <VStack gap={0.5}>
          <Text weight="medium">{i.shiftName}</Text>
          <Text type="supporting">
            {i.startTime}–{i.endTime}
          </Text>
        </VStack>
      ),
    },
    { key: 'type', header: 'Vi phạm', render: (i) => <Badge tone={typeTones[i.type]}>{typeLabels[i.type]}</Badge> },
    { key: 'minutes', header: 'Số phút', align: 'end', render: (i) => (i.type === 'absent' ? '—' : formatMinutes(i.minutes)) },
    { key: 'deduct', header: 'Công trừ', align: 'end', render: (i) => (i.deductWorkUnits > 0 ? i.deductWorkUnits : '—') },
    { key: 'penalty', header: 'Phạt', align: 'end', render: (i) => (i.penaltyAmount > 0 ? formatCurrency(i.penaltyAmount) : '—') },
  ];

  return (
    <DetailDrawer
      open={!!employee}
      onClose={onClose}
      label="Chi tiết vi phạm"
      title={employee?.fullName ?? 'Chi tiết vi phạm'}
      subtitle={employee ? `${employee.employeeCode} · ${formatDate(fromDate)} – ${formatDate(toDate)}` : undefined}
      width={680}
    >
      <VStack gap={3}>
        <Notice title={error} />
        {loading && (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        )}
        {detail && <DataTable columns={columns} rows={detail.items} rowKey={(i) => `${i.workDate}-${i.startTime}-${i.type}`} minWidth={0} emptyText="Không có vi phạm nào" />}
      </VStack>
    </DetailDrawer>
  );
}
