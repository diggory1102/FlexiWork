import { useEffect, useState } from 'react';
import { Drawer, DrawerHeader } from '@astryxdesign/core/Drawer';
import { Layout, LayoutContent } from '@astryxdesign/core/Layout';
import { VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Badge, type BadgeTone, Notice, Spinner } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { reportApi } from '@/services/reportApi';
import { getErrorMessage } from '@/services/apiClient';
import { formatCurrency, formatDate } from '@/lib/format';
import { formatMinutes } from '@/lib/dates';
import type { EmployeeViolation, EmployeeViolationDetail, ViolationItem, ViolationType } from '@/types/reports';

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
  const [detail, setDetail] = useState<EmployeeViolationDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const userId = employee?.userId;

  useEffect(() => {
    if (userId == null) return;
    let current = true;
    setDetail(null);
    setError(null);
    setLoading(true);
    reportApi
      .employeeViolations(userId, { fromDate, toDate })
      .then(({ data }) => current && setDetail(data))
      .catch((err) => current && setError(getErrorMessage(err)))
      .finally(() => current && setLoading(false));
    return () => {
      current = false;
    };
  }, [userId, fromDate, toDate]);

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
    <Drawer isOpen={!!employee} onOpenChange={(isOpen) => !isOpen && onClose()} label="Chi tiết vi phạm" width={680} purpose="form">
      <Layout
        header={
          <DrawerHeader
            title={employee?.fullName ?? 'Chi tiết vi phạm'}
            subtitle={employee ? `${employee.employeeCode} · ${formatDate(fromDate)} – ${formatDate(toDate)}` : undefined}
            onOpenChange={(isOpen) => !isOpen && onClose()}
          />
        }
        content={
          <LayoutContent>
            <VStack gap={3}>
              <Notice title={error} />
              {loading && (
                <div className="flex justify-center py-10">
                  <Spinner />
                </div>
              )}
              {detail && <DataTable columns={columns} rows={detail.items} rowKey={(i) => `${i.workDate}-${i.startTime}-${i.type}`} minWidth={0} emptyText="Không có vi phạm nào" />}
            </VStack>
          </LayoutContent>
        }
      />
    </Drawer>
  );
}
