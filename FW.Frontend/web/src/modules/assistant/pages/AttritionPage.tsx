import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Eye, RefreshCw, ShieldAlert, Users } from 'lucide-react';
import { BranchSelector, useBranchFilter } from '@/components/ui/BranchFilter';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Notice } from '@/components/ui/Feedback';
import { Avatar, PageHeader, Toolbar } from '@/components/ui/Layout';
import { StatCard } from '@/components/ui/StatCard';
import { formatDayMonth } from '@/lib/dates';
import { getErrorMessage } from '@/services/apiClient';
import { attritionApi } from '@/services/attritionApi';
import { toast } from '@/stores/toastStore';
import { useAuthStore } from '@/stores/authStore';
import { RoleGroups } from '@/types/auth';
import type { AttritionAlert, AttritionSummary } from '@/types/assistant';
import { ATTRITION_DISCLAIMER, AttritionLevelBadge, ScoreDelta } from '../components/AttritionBits';
import { AttritionDrawer } from '../components/AttritionDrawer';

type MinLevel = 'medium' | 'high';

/** Nhân sự cần quan tâm (UC 10.3 – 10.4): nhân viên có hành vi đổi khác so với chính họ trong 2 tuần qua, kèm lý do và xu hướng theo tuần. */
export function AttritionPage() {
  const branchFilter = useBranchFilter();
  const { apiBranchId } = branchFilter;
  const canRun = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const [minLevel, setMinLevel] = useState<MinLevel>('medium');
  const [alerts, setAlerts] = useState<AttritionAlert[]>([]);
  const [summary, setSummary] = useState<AttritionSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<AttritionAlert | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const branchId = apiBranchId === '' ? undefined : apiBranchId;
      const [list, sum] = await Promise.all([attritionApi.alerts({ branchId, minLevel, limit: 100 }), attritionApi.summary({ branchId })]);
      setAlerts(list.data);
      setSummary(sum.data);
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [apiBranchId, minLevel]);

  useEffect(() => {
    void load();
  }, [load]);

  const rerun = async () => {
    setRunning(true);
    try {
      const { data } = await attritionApi.run();
      toast.success(`Đã chấm lại tuần ${formatDayMonth(data.weekStart)}: ${data.scored} nhân viên đủ dữ liệu, ${data.high} mức cao, ${data.medium} mức trung bình.`);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setRunning(false);
    }
  };

  const columns: Column<AttritionAlert>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      render: (a) => (
        <HStack gap={3} vAlign="center">
          <Avatar name={a.employeeName} size={36} />
          <Text weight="medium" maxLines={1}>
            {a.employeeName}
          </Text>
        </HStack>
      ),
    },
    { key: 'level', header: 'Mức', width: 110, render: (a) => <AttritionLevelBadge level={a.level} /> },
    { key: 'score', header: 'Điểm', width: 80, align: 'end', render: (a) => <span className="font-semibold tabular-nums">{a.score}</span> },
    { key: 'delta', header: 'So với tuần trước', width: 150, render: (a) => <ScoreDelta delta={a.delta} /> },
    {
      key: 'reasons',
      header: 'Lý do chính',
      render: (a) => (
        <Text type="supporting" maxLines={2}>
          {a.reasons[0] ?? '—'}
        </Text>
      ),
    },
    {
      key: 'actions',
      header: '',
      width: 60,
      align: 'end',
      render: (a) => (
        <Button label="Xem" variant="ghost" size="sm" icon={<Icon icon={Eye} />} onClick={() => setSelected(a)} />
      ),
    },
  ];

  return (
    <VStack gap={4}>
      <PageHeader
        title="Nhân sự cần quan tâm"
        description={
          summary?.weekStart
            ? `Lần chấm điểm gần nhất: tuần bắt đầu ${formatDayMonth(summary.weekStart)} (tự chạy mỗi sáng thứ Hai).`
            : 'Chấm điểm tự chạy mỗi sáng thứ Hai, dựa trên thay đổi hành vi so với chính nhân viên đó.'
        }
        actions={
          <>
            <BranchSelector filter={branchFilter} />
            {canRun && <Button label="Chấm lại ngay" variant="secondary" icon={<Icon icon={RefreshCw} />} isLoading={running} onClick={() => void rerun()} />}
          </>
        }
      />

      <Notice status="info" title={ATTRITION_DISCLAIMER} />
      <Notice title={error} />

      <Grid columns={{ minWidth: 180, max: 3 }} gap={3}>
        <StatCard icon={Users} label="Nhân viên đã chấm điểm" value={summary?.scored ?? '—'} hint="Chỉ tính người đủ dữ liệu (đủ số ca của 8 tuần nền và 2 tuần gần nhất)" tone="accent" />
        <StatCard icon={ShieldAlert} label="Mức cao" value={summary?.high ?? '—'} tone="error" />
        <StatCard icon={ShieldAlert} label="Mức trung bình" value={summary?.medium ?? '—'} tone="warning" />
      </Grid>

      <Card padding={0}>
        <Toolbar>
          <HStack gap={2} vAlign="center">
            <Button label="Trung bình trở lên" variant={minLevel === 'medium' ? 'primary' : 'secondary'} size="sm" onClick={() => setMinLevel('medium')} />
            <Button label="Chỉ mức cao" variant={minLevel === 'high' ? 'primary' : 'secondary'} size="sm" onClick={() => setMinLevel('high')} />
          </HStack>
        </Toolbar>
        <div className="p-3">
          <DataTable
            columns={columns}
            rows={alerts}
            rowKey={(a) => a.userId}
            loading={loading}
            onRowClick={setSelected}
            emptyText="Không có nhân viên nào ở mức cần quan tâm."
            minWidth={680}
          />
        </div>
      </Card>

      <AttritionDrawer alert={selected} onClose={() => setSelected(null)} />
    </VStack>
  );
}
