import { useEffect, useMemo, useState } from 'react';
import { Grid } from '@astryxdesign/core/Grid';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { Card } from '@astryxdesign/core/Card';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { CalendarCheck, CalendarClock, CalendarMinus, Hourglass, Percent, UserRoundX } from 'lucide-react';
import { StatCard } from '@/components/ui/StatCard';
import { Badge, Spinner } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { BranchSelector, useBranchFilter } from '@/components/ui/BranchFilter';
import { ChartCard, ChartTooltip, axisProps, chartColors } from '@/components/charts/ChartCard';
import { reportApi } from '@/services/reportApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import type { BranchLeaveUsage, LeaveTypeUsage, LeaveUsageEmployee, LeaveUsageReport } from '@/types/reports';
import { percent } from './ViolationsTab';

const thisYear = new Date().getFullYear();
const yearOptions = Array.from({ length: 5 }, (_, i) => String(thisYear - i)).map((y) => ({ value: y, label: `Năm ${y}` }));
const days = (value: number) => `${value} ngày`;

/** Báo cáo quỹ phép năm: tổng quỹ / đã dùng / còn lại, theo chi nhánh, theo loại đơn, theo tháng, người dùng nhiều và người sắp hết phép. */
export function LeaveUsageTab() {
  const branchFilter = useBranchFilter();
  const [year, setYear] = useState(String(thisYear));
  const [report, setReport] = useState<LeaveUsageReport | null>(null);
  const [loading, setLoading] = useState(true);
  const { apiBranchId } = branchFilter;

  useEffect(() => {
    let current = true;
    setLoading(true);
    reportApi
      .leaveUsage({ year: Number(year), branchId: apiBranchId })
      .then(({ data }) => current && setReport(data))
      .catch((err) => current && toast.error(getErrorMessage(err)))
      .finally(() => current && setLoading(false));
    return () => {
      current = false;
    };
  }, [year, apiBranchId]);

  const months = useMemo(
    () => (report?.byMonth ?? []).map((m) => ({ month: `T${m.month}`, 'Ngày phép đã dùng': m.days })),
    [report],
  );

  const employeeColumns = (highlight: 'used' | 'remaining'): Column<LeaveUsageEmployee>[] => [
    {
      key: 'employee',
      header: 'Nhân viên',
      render: (e) => (
        <VStack gap={0.5}>
          <Text weight="medium">{e.fullName}</Text>
          <Text type="supporting">
            <span className="font-mono">{e.employeeCode}</span>
            {e.branchName && <> · {e.branchName}</>}
          </Text>
        </VStack>
      ),
    },
    { key: 'total', header: 'Quỹ', align: 'end', render: (e) => (e.isDefault ? <Text type="supporting">{e.totalDays} (mặc định)</Text> : e.totalDays) },
    { key: 'used', header: 'Đã dùng', align: 'end', render: (e) => <Text weight={highlight === 'used' ? 'semibold' : undefined}>{e.usedDays}</Text> },
    { key: 'remaining', header: 'Còn lại', align: 'end', render: (e) => <Text weight={highlight === 'remaining' ? 'semibold' : undefined}>{e.remainingDays}</Text> },
  ];

  const branchColumns: Column<BranchLeaveUsage>[] = [
    { key: 'name', header: 'Chi nhánh', render: (b) => <Text weight="medium">{b.branchName}</Text> },
    { key: 'emp', header: 'Nhân viên', align: 'end', render: (b) => b.employees },
    { key: 'total', header: 'Tổng quỹ', align: 'end', render: (b) => b.totalDays },
    { key: 'used', header: 'Đã dùng', align: 'end', render: (b) => b.usedDays },
    { key: 'left', header: 'Còn lại', align: 'end', render: (b) => b.remainingDays },
    { key: 'rate', header: 'Tỷ lệ dùng', align: 'end', render: (b) => percent(b.usageRate) },
  ];

  const typeColumns: Column<LeaveTypeUsage>[] = [
    {
      key: 'name',
      header: 'Loại đơn',
      render: (t) => (
        <VStack gap={1} hAlign="start">
          <Text weight="medium">{t.name}</Text>
          <HStack gap={1} wrap="wrap">
            <Badge tone={t.isPaid ? 'green' : 'gray'}>{t.isPaid ? 'Có lương' : 'Không lương'}</Badge>
            {t.deductsBalance && <Badge tone="blue">Trừ quỹ phép</Badge>}
          </HStack>
        </VStack>
      ),
    },
    { key: 'count', header: 'Đã duyệt', align: 'end', render: (t) => t.approvedRequests },
    { key: 'days', header: 'Số ngày', align: 'end', render: (t) => t.approvedDays },
    { key: 'pending', header: 'Chờ duyệt', align: 'end', render: (t) => (t.pendingDays > 0 ? t.pendingDays : '—') },
  ];

  const s = report?.summary;

  return (
    <VStack gap={4}>
      <HStack gap={2} wrap="wrap" vAlign="end">
        <BranchSelector filter={branchFilter} />
        <Selector label="Năm" isLabelHidden width={140} options={yearOptions} value={year} onChange={(v) => setYear(v ?? String(thisYear))} />
        {report && (
          <Text type="supporting">
            Quỹ mặc định {report.defaultDays} ngày/năm · cảnh báo khi còn từ 0 đến {report.lowBalanceDays} ngày.
          </Text>
        )}
      </HStack>

      {loading && !report && (
        <div className="flex justify-center py-16">
          <Spinner size="lg" />
        </div>
      )}

      {s && report && (
        <>
          <Grid columns={{ minWidth: 170, max: 6 }} gap={3}>
            <StatCard icon={CalendarCheck} label="Tổng quỹ phép" value={days(s.totalDays)} hint={`${s.employees} nhân viên`} />
            <StatCard icon={CalendarMinus} label="Đã dùng" value={days(s.usedDays)} tone="warning" />
            <StatCard icon={CalendarClock} label="Còn lại" value={days(s.remainingDays)} tone="success" />
            <StatCard icon={Percent} label="Tỷ lệ đã dùng" value={percent(s.usageRate)} tone="accent" />
            <StatCard icon={Hourglass} label="Đang chờ duyệt" value={days(s.pendingDays)} tone="purple" hint="Sẽ trừ vào quỹ khi duyệt" />
            <StatCard
              icon={UserRoundX}
              label="Sắp hết phép"
              value={s.lowBalanceEmployees}
              tone={s.lowBalanceEmployees > 0 ? 'error' : 'secondary'}
              hint={s.withoutBalance > 0 ? `${s.withoutBalance} người dùng quỹ mặc định` : undefined}
            />
          </Grid>

          <ChartCard title="Ngày phép đã dùng theo tháng" description={`Năm ${report.year}`} isEmpty={months.every((m) => m['Ngày phép đã dùng'] === 0)} emptyText="Chưa có ngày phép nào được duyệt">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={months} margin={{ top: 4, right: 4, left: -20, bottom: 0 }}>
                <CartesianGrid stroke={chartColors.grid} vertical={false} />
                <XAxis dataKey="month" {...axisProps} />
                <YAxis allowDecimals={false} {...axisProps} />
                <Tooltip content={<ChartTooltip />} cursor={{ fill: chartColors.grid, fillOpacity: 0.3 }} />
                <Bar dataKey="Ngày phép đã dùng" fill={chartColors.accent} radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>

          <Grid columns={{ minWidth: 420, max: 2 }} gap={4} align="start">
            {branchFilter.companyWide && (
              <Section title="Theo chi nhánh">
                <DataTable minWidth={0} columns={branchColumns} rows={report.branches} rowKey={(b) => b.branchId ?? 0} emptyText="Chưa có dữ liệu" />
              </Section>
            )}
            <Section title="Theo loại đơn">
              <DataTable minWidth={0} columns={typeColumns} rows={report.byType} rowKey={(t) => t.leaveTypeId} emptyText="Chưa có đơn nghỉ nào" />
            </Section>
            <Section title="Dùng nhiều phép nhất">
              <DataTable minWidth={0} columns={employeeColumns('used')} rows={report.topUsed} rowKey={(e) => e.userId} emptyText="Chưa có dữ liệu" />
            </Section>
            <Section title="Sắp hết phép">
              <DataTable minWidth={0} columns={employeeColumns('remaining')} rows={report.lowRemaining} rowKey={(e) => e.userId} emptyText="Không có ai sắp hết phép" />
            </Section>
          </Grid>
        </>
      )}
    </VStack>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card padding={0}>
      <div className="overflow-hidden rounded-[inherit]">
        <div className="border-b border-border p-4">
          <Heading level={3}>{title}</Heading>
        </div>
        {children}
      </div>
    </Card>
  );
}
