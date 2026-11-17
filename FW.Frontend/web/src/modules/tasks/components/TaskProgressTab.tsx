import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { DateInput } from '@astryxdesign/core/DateInput';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { ProgressBar } from '@astryxdesign/core/ProgressBar';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { RefreshCw } from 'lucide-react';
import { Avatar, Toolbar } from '@/components/ui/Layout';
import { ListCard } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { taskApi } from '@/services/taskApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { todayIso } from '@/lib/format';
import { scheduleStatusLabels } from '@/modules/schedules/scheduleUi';
import { RoleGroups } from '@/types/auth';
import type { ScheduleStatus } from '@/types/scheduling';
import type { ScheduleProgress, TaskProgress } from '@/types/tasks';
import { progressVariant } from '../taskUi';
import { ScheduleTasksDrawer } from './ScheduleTasksDrawer';

function Summary({ label, value, hint, tone }: { label: string; value: string; hint?: string; tone?: 'amber' }) {
  return (
    <Card variant="muted" padding={4}>
      <VStack gap={0.5}>
        <Text type="supporting" size="sm">
          {label}
        </Text>
        <HStack gap={2} vAlign="center">
          <Text size="xl" weight="semibold" hasTabularNumbers>
            {value}
          </Text>
          {tone && <Badge tone={tone}>Cần theo dõi</Badge>}
        </HStack>
        {hint && (
          <Text type="supporting" size="sm">
            {hint}
          </Text>
        )}
      </VStack>
    </Card>
  );
}

/** Tiến độ checklist các ca trong ngày (UC 6.4): tỷ lệ hoàn thành từng nhân viên và toàn chi nhánh. */
export function TaskProgressTab() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const { branches, load: loadLookups } = useLookupStore();
  const [date, setDate] = useState(todayIso());
  const [branchId, setBranchId] = useState<number | ''>('');
  const [data, setData] = useState<TaskProgress | null>(null);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<ScheduleProgress | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  useEffect(() => {
    if (isCompanyWide) void loadLookups().catch(() => undefined);
  }, [isCompanyWide, loadLookups]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData((await taskApi.progress(date, branchId)).data);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [date, branchId]);
  useEffect(() => void load(), [load]);

  const openRow = (row: ScheduleProgress) => {
    setSelected(row);
    setDrawerOpen(true);
  };

  const columns: Column<ScheduleProgress>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      width: 250,
      render: (r) => (
        <HStack gap={3} vAlign="center">
          <Avatar name={r.fullName} url={r.avatarUrl} size={36} />
          <VStack gap={0.5} className="min-w-0">
            <Text weight="medium" maxLines={1}>
              {r.fullName}
            </Text>
            <Text type="supporting" maxLines={1}>
              <span className="font-mono">{r.employeeCode}</span>
              {isCompanyWide && <> · {r.branchName}</>}
            </Text>
          </VStack>
        </HStack>
      ),
    },
    {
      key: 'shift',
      header: 'Ca',
      width: 190,
      render: (r) => (
        <VStack gap={0.5}>
          <Text>{r.shiftName}</Text>
          <Text type="supporting" size="sm">
            {r.startTime}–{r.endTime}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'status',
      header: 'Trạng thái ca',
      width: 150,
      render: (r) => (
        <Badge tone={r.scheduleStatus === 'completed' ? 'green' : r.scheduleStatus === 'in_progress' ? 'blue' : 'gray'}>
          {scheduleStatusLabels[r.scheduleStatus as ScheduleStatus] ?? r.scheduleStatus}
        </Badge>
      ),
    },
    {
      key: 'progress',
      header: 'Tiến độ checklist',
      width: 260,
      render: (r) =>
        r.total === 0 ? (
          <Text type="supporting">{r.scheduleStatus === 'scheduled' ? 'Chưa vào ca' : 'Chưa có việc'}</Text>
        ) : (
          <ProgressBar
            label={`Tiến độ của ${r.fullName}`}
            isLabelHidden
            value={r.percent ?? 0}
            variant={progressVariant(r.percent, r.mandatoryTotal - r.mandatoryDone)}
            hasValueLabel
            formatValueLabel={() => `${r.done}/${r.total} việc`}
          />
        ),
    },
    {
      key: 'mandatory',
      header: 'Việc bắt buộc',
      width: 150,
      render: (r) => {
        const pending = r.mandatoryTotal - r.mandatoryDone;
        if (r.mandatoryTotal === 0) return <Text type="supporting">—</Text>;
        return pending > 0 ? <Badge tone="amber">Còn {pending} việc</Badge> : <Badge tone="green">Đã xong</Badge>;
      },
    },
    {
      key: 'actions',
      header: '',
      width: 90,
      align: 'end',
      render: (r) => <Button label="Xem" size="sm" variant="ghost" onClick={() => openRow(r)} />,
    },
  ];

  return (
    <>
      <VStack gap={3}>
        <Grid columns={{ minWidth: 200, max: 4 }} gap={3}>
          <Summary
            label="Tiến độ chung"
            value={data?.percent == null ? '—' : `${data.percent}%`}
            hint={data ? `${data.doneTasks}/${data.totalTasks} việc đã giao` : undefined}
          />
          <Summary label="Số ca trong ngày" value={data ? String(data.schedules.length) : '—'} />
          <Summary
            label="Việc bắt buộc còn dở"
            value={data ? String(data.mandatoryPending) : '—'}
            tone={data && data.mandatoryPending > 0 ? 'amber' : undefined}
          />
          <Summary label="Ca chưa check-in" value={data ? String(data.notStarted) : '—'} hint="Chưa được giao việc" />
        </Grid>

        <ListCard>
          <Toolbar>
            <DateInput label="Ngày" isLabelHidden width={190} format="date" value={date} onChange={(v) => setDate(v ?? todayIso())} />
            {isCompanyWide && (
              <Selector
                label="Chi nhánh"
                isLabelHidden
                width={220}
                options={[{ value: '', label: 'Tất cả chi nhánh' }, ...branches.map((b) => ({ value: String(b.id), label: b.name }))]}
                value={String(branchId)}
                onChange={(v) => setBranchId(v ? Number(v) : '')}
              />
            )}
            <Button label="Làm mới" variant="secondary" icon={<Icon icon={RefreshCw} />} onClick={() => void load()} />
          </Toolbar>
          <DataTable
            columns={columns}
            rows={data?.schedules ?? []}
            rowKey={(r) => r.scheduleId}
            loading={loading}
            emptyText="Không có ca nào được công bố trong ngày này"
            onRowClick={openRow}
          />
        </ListCard>
      </VStack>

      <ScheduleTasksDrawer row={selected} date={date} open={drawerOpen} onClose={() => setDrawerOpen(false)} />
    </>
  );
}
