import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { DateInput } from '@astryxdesign/core/DateInput';
import { EmptyState } from '@astryxdesign/core/EmptyState';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import {
  AlarmClockCheck,
  CalendarClock,
  CalendarX2,
  ClipboardList,
  Clock3,
  Hourglass,
  Inbox,
  RefreshCw,
  ShieldQuestion,
  Smartphone,
  UserCheck,
  UserCog,
  Users,
} from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';
import { StatCard } from '@/components/ui/StatCard';
import { Notice } from '@/components/ui/Feedback';
import { Spinner } from '@/components/ui/Feedback';
import { ChartCard, ChartTooltip, axisProps, chartColors, legendProps } from '@/components/charts/ChartCard';
import { reportApi } from '@/services/reportApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { RoleGroups } from '@/types/auth';
import type { Dashboard } from '@/types/reports';
import { formatDayMonth } from '@/lib/dates';
import { todayIso, type ISODateString } from '@/lib/format';

/** Chu kỳ làm mới số liệu khi đang xem hôm nay (gần thời gian thực). */
const REFRESH_MS = 60_000;

/** Tổng quan quân số & chấm công trong ngày, xu hướng 7 ngày và so sánh chi nhánh (số liệu tính theo từng lần tải). */
export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const { branches, load } = useLookupStore();
  const companyWide = hasAnyRole(RoleGroups.AdminOrHr);
  const [branchId, setBranchId] = useState('');
  const [date, setDate] = useState<ISODateString>(todayIso());
  const [data, setData] = useState<Dashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const isManager = hasAnyRole(RoleGroups.Managers);
  const isToday = date === todayIso();

  useEffect(() => {
    if (isManager) void load();
  }, [isManager, load]);

  const fetchData = useCallback(
    async (silent: boolean) => {
      if (!silent) setLoading(true);
      try {
        const { data: result } = await reportApi.dashboard({ date, branchId: companyWide && branchId ? Number(branchId) : '' });
        setData(result);
        setError(null);
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setLoading(false);
      }
    },
    [date, branchId, companyWide],
  );

  useEffect(() => {
    if (!isManager) return;
    void fetchData(false);
    if (!isToday) return;
    const timer = setInterval(() => void fetchData(true), REFRESH_MS);
    return () => clearInterval(timer);
  }, [isManager, fetchData, isToday]);

  const trend = useMemo(
    () =>
      (data?.trend ?? []).map((t) => ({
        day: formatDayMonth(t.date),
        'Có mặt': t.attendedShifts,
        'Đi muộn': t.lateShifts,
        'Vắng mặt': t.absentShifts,
      })),
    [data],
  );
  const branchBars = useMemo(
    () =>
      (data?.branches ?? []).map((b) => ({
        name: b.branchName,
        'Được xếp lịch': b.headcount.scheduledEmployees,
        'Đã chấm công': b.headcount.checkedInEmployees,
      })),
    [data],
  );

  if (!isManager) return <EmployeeNotice name={user?.fullName ?? ''} />;

  const visibleBranches = companyWide ? branches : branches.filter((b) => b.id === user?.branchId);
  const branchOptions = [
    ...(companyWide ? [{ value: '', label: 'Tất cả chi nhánh' }] : []),
    ...visibleBranches.map((b) => ({ value: String(b.id), label: b.name })),
  ];
  const totals = data?.totals;

  return (
    <VStack gap={4}>
      <PageHeader
        title="Tổng quan"
        description={
          data
            ? `Tình hình quân số và chấm công trong ngày. Cập nhật lúc ${data.asOf.slice(11, 16)}${isToday ? ' (tự làm mới mỗi phút)' : ''}.`
            : 'Tình hình quân số và chấm công trong ngày.'
        }
        actions={
          <>
            <Selector
              label="Chi nhánh"
              isLabelHidden
              width={220}
              options={branchOptions}
              value={companyWide ? branchId : String(user?.branchId ?? '')}
              onChange={(v) => setBranchId(v ?? '')}
              isDisabled={!companyWide}
            />
            <DateInput label="Ngày" isLabelHidden width={180} value={date} onChange={(v) => setDate(v ?? todayIso())} format="date" />
            <IconButton
              label="Làm mới"
              tooltip="Làm mới và về hôm nay"
              icon={<Icon icon={RefreshCw} />}
              onClick={() => (isToday ? void fetchData(false) : setDate(todayIso()))}
            />
          </>
        }
      />

      <Notice title={error} />
      {loading && !data && (
        <div className="flex justify-center py-16">
          <Spinner size="lg" />
        </div>
      )}

      {totals && (
        <Grid columns={{ minWidth: 420, max: 2 }} gap={4} align="start">
          <VStack gap={4}>
            <Card padding={6} variant="blue">
              <HStack hAlign="between" vAlign="start" gap={4}>
                <VStack gap={1}>
                  <Text color="secondary">Tổng ca làm việc</Text>
                  <Heading level={2} type="display-2">
                    {totals.scheduledShifts}
                  </Heading>
                </VStack>
                <ShiftIllustration />
              </HStack>
              <VStack gap={2} className="mt-4 border-t border-border pt-4">
                <HStack gap={2} vAlign="center">
                  <Icon icon={Users} size="sm" color="accent" />
                  <Text>
                    <b>{totals.scheduledEmployees}</b> nhân viên được xếp lịch
                  </Text>
                </HStack>
                <HStack gap={2} vAlign="center">
                  <Icon icon={UserCheck} size="sm" color="success" />
                  <Text>
                    <b>{totals.checkedInEmployees}</b> nhân viên đã chấm công ({totals.workingEmployees} đang trong ca)
                  </Text>
                </HStack>
              </VStack>
            </Card>

            <Grid columns={{ minWidth: 150, max: 4 }} gap={3}>
              <StatCard icon={UserCog} label="Đang trong ca" value={totals.workingEmployees} tone="success" />
              <StatCard icon={Hourglass} label="Chờ check-in" value={totals.waitingCheckInShifts} tone="warning" hint={`${totals.upcomingShifts} ca sắp tới`} />
              <StatCard icon={Clock3} label="Đi muộn" value={totals.lateShifts} tone="warning" />
              <StatCard icon={CalendarX2} label="Vắng mặt" value={totals.absentShifts} tone="error" />
              <StatCard icon={CalendarClock} label="Nghỉ phép" value={totals.onLeaveEmployees} tone="accent" />
              <StatCard icon={ShieldQuestion} label="Chấm công chờ duyệt" value={totals.pendingAttendance} tone="purple" />
              <StatCard icon={Inbox} label="Đơn chờ duyệt" value={totals.pendingRequests} tone="purple" />
              <StatCard icon={AlarmClockCheck} label="Tỷ lệ đúng giờ" value={totals.onTimeRate == null ? '—' : `${totals.onTimeRate}%`} tone="success" />
            </Grid>

            <Card padding={5}>
              <HStack gap={4} hAlign="between" vAlign="start">
                <VStack gap={2}>
                  <Heading level={3}>Việc cần xử lý</Heading>
                  <Text type="supporting" as="p">
                    {totals.pendingAttendance + totals.pendingRequests === 0
                      ? 'Không có lượt chấm công hay đơn từ nào đang chờ duyệt.'
                      : `${totals.pendingAttendance} lượt chấm công và ${totals.pendingRequests} đơn từ đang chờ duyệt.`}
                  </Text>
                  <HStack gap={2}>
                    <Button label="Duyệt chấm công" variant="secondary" size="sm" href="/attendance" />
                    <Button label="Duyệt đơn" variant="secondary" size="sm" href="/requests" />
                  </HStack>
                </VStack>
                <Icon icon={ClipboardList} size="lg" color="accent" />
              </HStack>
            </Card>
          </VStack>

          <VStack gap={4}>
            <ChartCard title="Xu hướng 7 ngày" description="Số ca có mặt, đi muộn và vắng mặt mỗi ngày." isEmpty={trend.length === 0}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={trend} margin={{ top: 4, right: 4, left: -20, bottom: 0 }}>
                  <CartesianGrid stroke={chartColors.grid} vertical={false} />
                  <XAxis dataKey="day" {...axisProps} />
                  <YAxis allowDecimals={false} {...axisProps} />
                  <Tooltip content={<ChartTooltip />} cursor={{ fill: chartColors.grid, fillOpacity: 0.3 }} />
                  <Legend {...legendProps} />
                  <Bar dataKey="Có mặt" fill={chartColors.accent} radius={[4, 4, 0, 0]} />
                  <Bar dataKey="Đi muộn" fill={chartColors.warning} radius={[4, 4, 0, 0]} />
                  <Bar dataKey="Vắng mặt" fill={chartColors.error} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartCard>

            {companyWide && branchBars.length > 1 ? (
              <ChartCard title="Theo chi nhánh" description="Số nhân viên được xếp lịch và đã chấm công trong ngày." height={Math.max(180, branchBars.length * 56)}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={branchBars} layout="vertical" margin={{ top: 4, right: 12, left: 8, bottom: 0 }}>
                    <CartesianGrid stroke={chartColors.grid} horizontal={false} />
                    <XAxis type="number" allowDecimals={false} {...axisProps} />
                    <YAxis type="category" dataKey="name" width={120} {...axisProps} />
                    <Tooltip content={<ChartTooltip />} cursor={{ fill: chartColors.grid, fillOpacity: 0.3 }} />
                    <Legend {...legendProps} />
                    <Bar dataKey="Được xếp lịch" fill={chartColors.info} radius={[0, 4, 4, 0]} />
                    <Bar dataKey="Đã chấm công" fill={chartColors.success} radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </ChartCard>
            ) : (
              totals.scheduledShifts === 0 && (
                <Card padding={8}>
                  <EmptyState
                    title={isToday ? 'Hôm nay chưa xếp lịch làm việc' : 'Ngày này chưa có lịch làm việc'}
                    description="Danh sách nhân viên theo ca sẽ hiển thị sau khi xếp lịch (phân hệ Lịch làm việc)."
                    actions={<Button label="Đi tới xếp lịch" variant="primary" href="/schedules" />}
                  />
                </Card>
              )
            )}
          </VStack>
        </Grid>
      )}
    </VStack>
  );
}

function EmployeeNotice({ name }: { name: string }) {
  return (
    <VStack hAlign="center" className="mt-10">
      <Card padding={8} maxWidth={520}>
        <EmptyState
          icon={<Icon icon={Smartphone} size="lg" color="accent" />}
          title={`Xin chào ${name}!`}
          description="Trang quản trị dành cho Quản trị viên, Nhân sự và Quản lý chi nhánh. Vui lòng dùng ứng dụng di động FlexiWork để chấm công, xem lịch ca và gửi đơn từ."
          actions={
            <>
              <Button label="Hồ sơ cá nhân" variant="secondary" href="/profile" />
              <Button label="Đổi mật khẩu" variant="secondary" href="/change-password" />
            </>
          }
          headingLevel={1}
        />
      </Card>
    </VStack>
  );
}

/** Hình minh họa tự vẽ (SVG), màu lấy từ design token nên tự đổi theo theme sáng/tối. */
function ShiftIllustration() {
  return (
    <svg viewBox="0 0 200 120" className="hidden h-28 shrink-0 sm:block" aria-hidden="true">
      <rect x="20" y="30" width="110" height="80" rx="10" fill="var(--color-background-surface)" stroke="var(--color-accent)" strokeOpacity=".35" />
      <rect x="20" y="30" width="110" height="20" rx="10" fill="var(--color-accent)" fillOpacity=".85" />
      {[0, 1, 2, 3].map((r) =>
        [0, 1, 2, 3, 4].map((c) => (
          <rect key={`${r}-${c}`} x={30 + c * 19} y={58 + r * 12} width="13" height="7" rx="2" fill="var(--color-accent)" fillOpacity={(r + c) % 3 === 0 ? 0.6 : 0.15} />
        )),
      )}
      <circle cx="155" cy="62" r="30" fill="var(--color-background-surface)" stroke="var(--color-accent)" strokeWidth="4" />
      <path d="M155 44v18l12 8" stroke="var(--color-accent)" strokeWidth="4" fill="none" strokeLinecap="round" />
      <circle cx="176" cy="90" r="12" fill="#22c55e" />
      <path d="M170 90l4 4 8-8" stroke="white" strokeWidth="3" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
