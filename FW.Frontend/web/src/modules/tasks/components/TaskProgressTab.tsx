import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { DateInput } from '@astryxdesign/core/DateInput';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { ProgressBar } from '@astryxdesign/core/ProgressBar';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { RefreshCw } from 'lucide-react';
import { Toolbar } from '@/components/ui/Layout';
import { EmployeeCell } from '@/components/ui/EmployeeCell';
import { ListCard } from '@/components/ui/ListCard';
import { BranchSelect } from '@/components/ui/BranchSelect';
import { Badge } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { taskApi } from '@/services/taskApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';
import { todayIso } from '@/lib/format';
import { scheduleStatusLabels } from '@/modules/schedules/scheduleUi';
import { RoleGroups } from '@/types/auth';
import type { ScheduleStatus } from '@/types/scheduling';
import type { ScheduleProgress, SharedTask, TaskProgress } from '@/types/tasks';
import { phaseLabels, progressVariant } from '../taskUi';
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

/** Việc làm chung trong ngày: ai đã làm, hoặc chưa ai nhận (để Quản lý nhắc). */
function SharedTasksCard({ tasks, showBranch }: { tasks: SharedTask[]; showBranch: boolean }) {
  const columns: Column<SharedTask>[] = [
    {
      key: 'title',
      header: 'Việc làm chung',
      render: (t) => (
        <VStack gap={0.5}>
          <Text weight="medium">{t.title}</Text>
          <HStack gap={1.5} vAlign="center">
            <Text type="supporting" size="sm">
              {phaseLabels[t.phase]}
            </Text>
            {t.isMandatory && <Badge tone="amber">Bắt buộc</Badge>}
          </HStack>
        </VStack>
      ),
    },
    {
      key: 'shift',
      header: 'Ca',
      width: 200,
      render: (t) => (
        <VStack gap={0.5}>
          <Text>{t.shiftName}</Text>
          <Text type="supporting" size="sm">
            {t.startTime}–{t.endTime}
            {showBranch ? ` · ${t.branchName}` : ''}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'who',
      header: 'Ai làm',
      width: 260,
      render: (t) =>
        t.isDone ? (
          <VStack gap={0.5}>
            <Text weight="medium">{t.completedByName ?? 'Đã có người làm'}</Text>
            <Text type="supporting" size="sm">
              Xong lúc {t.completedAt?.slice(11, 16) ?? '—'}
            </Text>
          </VStack>
        ) : (
          <VStack gap={0.5}>
            <Text type="supporting">Chưa có người nhận</Text>
            <Text type="supporting" size="sm">
              {t.participants} người đang trong ca
            </Text>
          </VStack>
        ),
    },
    {
      key: 'status',
      header: 'Trạng thái',
      width: 150,
      render: (t) =>
        t.isDone ? <Badge tone="green">Đã xong</Badge> : <Badge tone="amber">Chưa ai làm</Badge>,
    },
  ];
  return (
    <ListCard>
      <VStack gap={0.5} padding={4}>
        <Text weight="semibold">Việc làm chung hôm nay</Text>
        <Text type="supporting" size="sm">
          Một người làm là cả ca xong — bảng này cho biết ai đã làm và việc nào chưa ai nhận.
        </Text>
      </VStack>
      <DataTable columns={columns} rows={tasks} rowKey={(t) => `${t.taskId}-${t.branchId}-${t.shiftName}`} />
    </ListCard>
  );
}

/** Tiến độ checklist các ca trong ngày (UC 6.4): tỷ lệ hoàn thành từng nhân viên và toàn chi nhánh. */
export function TaskProgressTab() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const [date, setDate] = useState(todayIso());
  const [branchId, setBranchId] = useState<number | ''>('');
  const [data, setData] = useState<TaskProgress | null>(null);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<ScheduleProgress | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

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
        <EmployeeCell name={r.fullName} code={r.employeeCode} avatarUrl={r.avatarUrl} sub={isCompanyWide ? r.branchName : null} />
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
          r.expected > 0 && r.scheduleStatus === 'scheduled' ? (
            <VStack gap={0.5}>
              <Text>Dự kiến {r.expected} việc</Text>
              <Text type="supporting" size="sm">
                Sẽ nhận khi vào ca
              </Text>
            </VStack>
          ) : (
            <Text type="supporting">{r.expected === 0 ? 'Ca này chưa có việc nào áp dụng' : 'Chưa có việc'}</Text>
          )
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
        if (r.mandatoryTotal === 0) return <Text type="supporting">{r.total === 0 && r.expectedMandatory > 0 ? `${r.expectedMandatory} việc (dự kiến)` : '—'}</Text>;
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
          <Summary label="Ca chưa check-in" value={data ? String(data.notStarted) : '—'} hint="Nhận việc khi vào ca (xem cột Dự kiến)" />
        </Grid>

        {data && data.sharedTasks.length > 0 && <SharedTasksCard tasks={data.sharedTasks} showBranch={isCompanyWide} />}

        <ListCard>
          <Toolbar>
            <DateInput label="Ngày" isLabelHidden width={190} format="date" value={date} onChange={(v) => setDate(v ?? todayIso())} />
            {isCompanyWide && (
              <BranchSelect width={220} value={branchId} onChange={setBranchId} />
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

      <ScheduleTasksDrawer row={selected} date={date} open={drawerOpen} onClose={() => setDrawerOpen(false)} onChanged={() => void load()} />
    </>
  );
}
