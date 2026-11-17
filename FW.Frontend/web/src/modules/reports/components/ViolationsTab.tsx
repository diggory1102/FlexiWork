import { useEffect, useMemo, useState } from 'react';
import { DateRangeInput, type DateRange } from '@astryxdesign/core/DateRangeInput';
import { Grid } from '@astryxdesign/core/Grid';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { AlarmClockOff, Clock3, CalendarX2, Percent, TriangleAlert, WalletCards } from 'lucide-react';
import { Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { StatCard } from '@/components/ui/StatCard';
import { BranchSelector, useBranchFilter } from '@/components/ui/BranchFilter';
import { ChartCard, ChartTooltip, axisProps, chartColors, legendProps } from '@/components/charts/ChartCard';
import { useApiData } from '@/hooks/useApiData';
import { useDebounce } from '@/hooks/useDebounce';
import { reportApi } from '@/services/reportApi';
import { todayIso, formatCurrency } from '@/lib/format';
import { formatMinutes } from '@/lib/dates';
import { iso, monthStart, rangePresets } from '@/lib/dateRanges';
import type { EmployeeViolation } from '@/types/reports';
import { EmployeeViolationDrawer } from './EmployeeViolationDrawer';

const PAGE_SIZE = 15;

/** Tỷ lệ % hiển thị gọn: null → "—". */
export const percent = (value: number | null | undefined) => (value == null ? '—' : `${value}%`);

/** Báo cáo vi phạm chấm công theo khoảng ngày: tổng hợp, so sánh chi nhánh, danh sách nhân viên (bấm để xem từng ca vi phạm). */
export function ViolationsTab() {
  const branchFilter = useBranchFilter();
  const today = todayIso();
  const [range, setRange] = useState<DateRange>({ start: monthStart(today), end: today });
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [pageIndex, setPageIndex] = useState(1);
  const [selected, setSelected] = useState<EmployeeViolation | null>(null);

  const { apiBranchId } = branchFilter;
  const fromDate = range.start;
  const toDate = range.end;

  useEffect(() => setPageIndex(1), [apiBranchId, fromDate, toDate, debounced]);

  const { data: report, loading } = useApiData(
    () => reportApi.violations({ branchId: apiBranchId, fromDate, toDate, search: debounced, pageIndex, pageSize: PAGE_SIZE }),
    [apiBranchId, fromDate, toDate, debounced, pageIndex],
  );

  const summary = report?.summary;
  const branchBars = useMemo(
    () =>
      (report?.branches ?? []).map((b) => ({
        name: b.branchName,
        'Đi muộn': b.summary.lateCount,
        'Về sớm': b.summary.earlyCount,
        'Vắng mặt': b.summary.absentCount,
      })),
    [report],
  );

  const columns: Column<EmployeeViolation>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      render: (e) => (
        <VStack gap={0.5}>
          <Text weight="medium">{e.fullName}</Text>
          <Text type="supporting">
            <span className="font-mono">{e.employeeCode}</span>
            {branchFilter.companyWide && e.branchName && <> · {e.branchName}</>}
          </Text>
        </VStack>
      ),
    },
    { key: 'late', header: 'Đi muộn', align: 'end', render: (e) => <Count count={e.summary.lateCount} detail={formatMinutes(e.summary.lateMinutes)} /> },
    { key: 'early', header: 'Về sớm', align: 'end', render: (e) => <Count count={e.summary.earlyCount} detail={formatMinutes(e.summary.earlyMinutes)} /> },
    { key: 'absent', header: 'Vắng mặt', align: 'end', render: (e) => <Count count={e.summary.absentCount} /> },
    { key: 'rate', header: 'Tỷ lệ vi phạm', align: 'end', render: (e) => <Text hasTabularNumbers>{percent(e.summary.violationRate)}</Text> },
    { key: 'deduct', header: 'Công bị trừ', align: 'end', render: (e) => <Text hasTabularNumbers>{e.summary.deductedWorkDays}</Text> },
    {
      key: 'penalty',
      header: 'Tiền phạt',
      align: 'end',
      render: (e) => (
        <Text weight="medium" hasTabularNumbers>
          {formatCurrency(e.summary.penaltyAmount)}
        </Text>
      ),
    },
  ];

  return (
    <VStack gap={4}>
      <HStack gap={2} wrap="wrap" vAlign="end">
        <BranchSelector filter={branchFilter} />
        <DateRangeInput
          label="Khoảng ngày"
          isLabelHidden
          width={300}
          value={range}
          onChange={(v) => setRange(v ?? { start: monthStart(today), end: today })}
          presets={rangePresets(today)}
          max={iso(today)}
        />
        <Text type="supporting">Tối đa 93 ngày mỗi lần xem.</Text>
      </HStack>

      {summary && (
        <Grid columns={{ minWidth: 170, max: 6 }} gap={3}>
          <StatCard
            icon={Percent}
            label="Tỷ lệ ca vi phạm"
            value={percent(summary.violationRate)}
            tone={summary.violationRate != null && summary.violationRate >= 20 ? 'error' : 'accent'}
            hint={`${summary.violatingShifts}/${summary.evaluatedShifts} ca đã có kết quả`}
          />
          <StatCard icon={Clock3} label="Đi muộn" value={summary.lateCount} tone="warning" hint={formatMinutes(summary.lateMinutes)} />
          <StatCard icon={AlarmClockOff} label="Về sớm" value={summary.earlyCount} tone="warning" hint={formatMinutes(summary.earlyMinutes)} />
          <StatCard icon={CalendarX2} label="Vắng mặt" value={summary.absentCount} tone="error" />
          <StatCard icon={TriangleAlert} label="Công bị trừ" value={summary.deductedWorkDays} tone="purple" />
          <StatCard icon={WalletCards} label="Tiền phạt" value={formatCurrency(summary.penaltyAmount)} tone="error" />
        </Grid>
      )}

      {branchFilter.companyWide && branchBars.length > 1 && (
        <ChartCard title="Vi phạm theo chi nhánh" description="Số lần đi muộn, về sớm và vắng mặt trong khoảng ngày đã chọn." height={Math.max(200, branchBars.length * 60)}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={branchBars} layout="vertical" margin={{ top: 4, right: 12, left: 8, bottom: 0 }}>
              <CartesianGrid stroke={chartColors.grid} horizontal={false} />
              <XAxis type="number" allowDecimals={false} {...axisProps} />
              <YAxis type="category" dataKey="name" width={130} {...axisProps} />
              <Tooltip content={<ChartTooltip />} cursor={{ fill: chartColors.grid, fillOpacity: 0.3 }} />
              <Legend {...legendProps} />
              <Bar dataKey="Đi muộn" stackId="v" fill={chartColors.warning} />
              <Bar dataKey="Về sớm" stackId="v" fill={chartColors.info} />
              <Bar dataKey="Vắng mặt" stackId="v" fill={chartColors.error} radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm theo tên hoặc mã nhân viên…" value={search} onChange={setSearch} />
        </Toolbar>
        <DataTable
          columns={columns}
          rows={report?.employees.items ?? []}
          rowKey={(e) => e.userId}
          loading={loading}
          emptyText="Không có nhân viên nào vi phạm trong khoảng ngày này"
          onRowClick={setSelected}
        />
        <Pagination page={report?.employees ?? null} onChange={setPageIndex} />
      </ListCard>

      <EmployeeViolationDrawer employee={selected} fromDate={fromDate} toDate={toDate} onClose={() => setSelected(null)} />
    </VStack>
  );
}

function Count({ count, detail }: { count: number; detail?: string }) {
  if (count === 0) return <Text type="supporting">—</Text>;
  return (
    <VStack gap={0.5} hAlign="end">
      <Text weight="medium" hasTabularNumbers>
        {count}
      </Text>
      {detail && <Text type="supporting">{detail}</Text>}
    </VStack>
  );
}
