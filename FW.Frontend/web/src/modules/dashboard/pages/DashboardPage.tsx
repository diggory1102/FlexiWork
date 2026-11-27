import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { DateInput } from '@astryxdesign/core/DateInput';
import { EmptyState } from '@astryxdesign/core/EmptyState';
import { Grid } from '@astryxdesign/core/Grid';
import { Heading } from '@astryxdesign/core/Text';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Clock3, RefreshCw, Smartphone, UserCheck, UserX, Users, type LucideIcon } from 'lucide-react';
import { DataTable } from '@/components/ui/DataTable';
import { PageFrame } from '@/layouts/PageFrame';
import { BranchSelector, useBranchFilter } from '@/components/ui/BranchFilter';
import { Notice } from '@/components/ui/Feedback';
import { AttritionWidget } from '@/modules/assistant/components/AttritionWidget';
import { Spinner } from '@/components/ui/Feedback';
import { TrendChart } from '@/modules/dashboard/components/TrendChart';
import { reportApi } from '@/services/reportApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { RoleGroups } from '@/types/auth';
import type { Dashboard } from '@/types/reports';
import { todayIso, type ISODateString } from '@/lib/format';

/** Chu kỳ làm mới số liệu khi đang xem hôm nay (gần thời gian thực). */
const REFRESH_MS = 60_000;

/** Màu chấm ở thẻ "Trong ngày", theo quy ước màu trạng thái công và kết quả duyệt. */
const statColors = {
  onTime: 'var(--color-data-categorical-teal)',
  leave: 'var(--color-data-categorical-blue)',
  pending: 'var(--color-warning)',
  idle: 'var(--color-data-neutral)',
};

/** Tổng quan quân số & chấm công trong ngày, xu hướng 7 ngày và so sánh chi nhánh (số liệu tính theo từng lần tải). */
export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const branchFilter = useBranchFilter();
  const { apiBranchId } = branchFilter;
  const [date, setDate] = useState<ISODateString>(todayIso());
  const [data, setData] = useState<Dashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const isManager = hasAnyRole(RoleGroups.Managers);
  const isToday = date === todayIso();

  const fetchData = useCallback(
    async (silent: boolean) => {
      if (!silent) setLoading(true);
      try {
        const { data: result } = await reportApi.dashboard({ date, branchId: apiBranchId });
        setData(result);
        setError(null);
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setLoading(false);
      }
    },
    [date, apiBranchId],
  );

  useEffect(() => {
    if (!isManager) return;
    void fetchData(false);
    if (!isToday) return;
    const timer = setInterval(() => void fetchData(true), REFRESH_MS);
    return () => clearInterval(timer);
  }, [isManager, fetchData, isToday]);

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

  const totals = data?.totals;

  return (
    <PageFrame
      title={`Xin chào, ${user?.fullName ?? ''}`}
      meta={
        data && (
          <Text type="supporting">
            Cập nhật lúc {data.asOf.slice(11, 16)}
            {isToday ? ' · tự làm mới mỗi phút' : ''}
          </Text>
        )
      }
      actions={
        <>
          <BranchSelector filter={branchFilter} />
          <DateInput label="Ngày" isLabelHidden width={180} value={date} onChange={(v) => setDate(v ?? todayIso())} format="date" />
          <IconButton
            label="Làm mới"
            tooltip="Làm mới và về hôm nay"
            variant="secondary"
            icon={<Icon icon={RefreshCw} />}
            onClick={() => (isToday ? void fetchData(false) : setDate(todayIso()))}
          />
        </>
      }
    >
      <VStack gap={3}>
        <Notice title={error} />
        {loading && !data && (
          <div className="flex justify-center py-16">
            <Spinner size="lg" />
          </div>
        )}

        {totals && (
          <>
            <Card padding={0}>
              <Grid columns={{ minWidth: 460, max: 2 }} gap={0}>
                <div className="grid grid-cols-2">
                  <Kpi icon={Users} label="Tổng ca làm việc" value={totals.scheduledShifts} hint={`${totals.scheduledEmployees} nhân viên được xếp lịch`} className="border-b border-r" />
                  <Kpi icon={UserCheck} label="Đã chấm công" value={totals.checkedInEmployees} hint="nhân viên" className="border-b" />
                  <Kpi icon={Clock3} label="Đi muộn" value={totals.lateShifts} hint="ca" className="border-r" />
                  <Kpi icon={UserX} label="Vắng mặt" value={totals.absentShifts} hint="ca" />
                </div>
                <VStack gap={3} padding={5}>
                  <HStack hAlign="between" vAlign="center">
                    <Heading level={2}>Xu hướng 7 ngày</Heading>
                  </HStack>
                  <div style={{ height: 300 }}>
                    {(data?.trend.length ?? 0) === 0 ? (
                      <EmptyState title="Chưa có dữ liệu để hiển thị" />
                    ) : (
                      <TrendChart trend={data?.trend ?? []} />
                    )}
                  </div>
                </VStack>
              </Grid>
            </Card>

            <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-3">
              <div className="flex flex-col gap-5 xl:col-span-2">
                {branchFilter.companyWide && branchBars.length > 1 ? (
                  <Card padding={0}>
                    <VStack gap={3} padding={5}>
                      <Heading level={2}>Theo chi nhánh</Heading>
                      <DataTable
                        density="spacious"
                        minWidth={420}
                        rows={branchBars}
                        rowKey={(b) => b.name}
                        columns={[
                          { key: 'name', header: 'Chi nhánh', render: (b) => <Text weight="semibold">{b.name}</Text> },
                          { key: 'scheduled', header: 'Được xếp lịch', align: 'end', render: (b) => <Text hasTabularNumbers>{b['Được xếp lịch']}</Text> },
                          { key: 'checked', header: 'Đã chấm công', align: 'end', render: (b) => <Text hasTabularNumbers>{b['Đã chấm công']}</Text> },
                        ]}
                      />
                    </VStack>
                  </Card>
                ) : (
                  totals.scheduledShifts === 0 && (
                    <Card padding={6}>
                      <EmptyState
                        title={isToday ? 'Hôm nay chưa xếp lịch làm việc' : 'Ngày này chưa có lịch làm việc'}
                        description="Danh sách nhân viên theo ca sẽ hiển thị sau khi xếp lịch (phân hệ Lịch làm việc)."
                        actions={<Button label="Đi tới xếp lịch" variant="primary" href="/schedules" />}
                      />
                    </Card>
                  )
                )}
                <AttritionWidget branchId={apiBranchId} />
              </div>

              <Card padding={5}>
                <VStack gap={4}>
                  <Heading level={2}>Trong ngày</Heading>
                  <Stat color={statColors.onTime} label="Đang trong ca" value={totals.workingEmployees} />
                  <Stat color={statColors.idle} label={`Chờ check-in (${totals.upcomingShifts} ca sắp tới)`} value={totals.waitingCheckInShifts} />
                  <Stat color={statColors.leave} label="Nghỉ phép" value={totals.onLeaveEmployees} />
                  <Stat color={statColors.onTime} label="Tỷ lệ đúng giờ" value={totals.onTimeRate == null ? '—' : `${totals.onTimeRate}%`} />
                  <Stat color={statColors.pending} label="Chấm công chờ duyệt" value={totals.pendingAttendance} />
                  <Stat color={statColors.pending} label="Đơn chờ duyệt" value={totals.pendingRequests} />
                  <Button label="Duyệt chấm công" variant="secondary" href="/attendance" />
                  <Button label="Duyệt đơn" variant="primary" href="/requests" />
                </VStack>
              </Card>
            </div>
          </>
        )}
      </VStack>
    </PageFrame>
  );
}

/** Ô chỉ số lớn: biểu tượng tròn, số, nhãn (theo Home V1 của mẫu). */
function Kpi({ icon, label, value, hint, className = '' }: { icon: LucideIcon; label: string; value: ReactNode; hint: string; className?: string }) {
  return (
    <VStack gap={2} padding={5} className={`border-border ${className}`}>
      <span className="flex h-12 w-12 items-center justify-center rounded-full bg-muted">
        <Icon icon={icon} />
      </span>
      <Text type="display-2" weight="bold" hasTabularNumbers>
        {value}
      </Text>
      <Text weight="semibold">{label}</Text>
      <Text type="supporting">{hint}</Text>
    </VStack>
  );
}

/** Dòng chỉ số dạng chú giải: chấm màu, nhãn, số. */
function Stat({ color, label, value }: { color: string; label: string; value: ReactNode }) {
  return (
    <HStack gap={3} vAlign="center" hAlign="between">
      <HStack gap={2} vAlign="center">
        <span className="size-2.5 shrink-0 rounded-full" style={{ background: color }} />
        <Text color="secondary">{label}</Text>
      </HStack>
      <Text weight="bold" hasTabularNumbers>
        {value}
      </Text>
    </HStack>
  );
}

function EmployeeNotice({ name }: { name: string }) {
  return (
    <VStack hAlign="center" padding={4} className="mt-10">
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
