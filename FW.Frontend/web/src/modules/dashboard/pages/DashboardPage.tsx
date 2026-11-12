import { useEffect, useState, type ComponentType, type SVGProps } from 'react';
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
  AlarmClockOff,
  CalendarClock,
  CalendarX2,
  ClipboardList,
  Clock3,
  LogIn,
  LogOut,
  RefreshCw,
  Smartphone,
  Sparkles,
  UserCheck,
  Users,
  Wallet,
} from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { RoleGroups } from '@/types/auth';
import { formatCurrency, todayIso, type ISODateString } from '@/lib/format';

type IconType = ComponentType<SVGProps<SVGSVGElement>>;
type StatColor = 'warning' | 'error' | 'accent' | 'purple';

const stats: { icon: IconType; label: string; color: StatColor }[] = [
  { icon: Clock3, label: 'Đi muộn', color: 'warning' },
  { icon: AlarmClockOff, label: 'Về sớm', color: 'warning' },
  { icon: LogIn, label: 'Quên check-in', color: 'error' },
  { icon: LogOut, label: 'Quên check-out', color: 'error' },
  { icon: CalendarClock, label: 'Nghỉ phép', color: 'accent' },
  { icon: CalendarX2, label: 'Nghỉ không phép', color: 'error' },
  { icon: Sparkles, label: 'Ca phát sinh', color: 'purple' },
  { icon: Smartphone, label: 'Trùng thiết bị', color: 'error' },
];

/**
 * Tổng quan quân số trong ngày. Bố cục tham khảo HRM iPOS; số liệu sẽ được nối API ở Task 5.6
 * (cần dữ liệu chấm công của Cấp 3), hiện hiển thị 0.
 */
export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const { branches, load } = useLookupStore();
  const companyWide = hasAnyRole(RoleGroups.AdminOrHr);
  const [branchId, setBranchId] = useState('');
  const [date, setDate] = useState<ISODateString>(todayIso());

  useEffect(() => {
    if (hasAnyRole(RoleGroups.Managers)) void load();
  }, [hasAnyRole, load]);

  if (!hasAnyRole(RoleGroups.Managers)) return <EmployeeNotice name={user?.fullName ?? ''} />;

  const visibleBranches = companyWide ? branches : branches.filter((b) => b.id === user?.branchId);
  const branchOptions = [
    ...(companyWide ? [{ value: '', label: 'Tất cả chi nhánh' }] : []),
    ...visibleBranches.map((b) => ({ value: String(b.id), label: b.name })),
  ];

  return (
    <VStack gap={4}>
      <PageHeader
        title="Tổng quan"
        description="Tình hình quân số và chấm công trong ngày."
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
            <IconButton label="Về hôm nay" tooltip="Về hôm nay" icon={<Icon icon={RefreshCw} />} onClick={() => setDate(todayIso())} />
          </>
        }
      />

      <Grid columns={{ minWidth: 420, max: 2 }} gap={4} align="start">
        <VStack gap={4}>
          <Card padding={6} variant="blue">
            <HStack hAlign="between" vAlign="start" gap={4}>
              <VStack gap={1}>
                <Text color="secondary">Tổng ca làm việc</Text>
                <Heading level={2} type="display-2">
                  0
                </Heading>
              </VStack>
              <ShiftIllustration />
            </HStack>
            <VStack gap={2} className="mt-4 border-t border-border pt-4">
              <HStack gap={2} vAlign="center">
                <Icon icon={Users} size="sm" color="accent" />
                <Text>
                  <b>0</b> nhân viên được xếp lịch
                </Text>
              </HStack>
              <HStack gap={2} vAlign="center">
                <Icon icon={UserCheck} size="sm" color="success" />
                <Text>
                  <b>0</b> nhân viên đã chấm công
                </Text>
              </HStack>
            </VStack>
          </Card>

          <Grid columns={{ minWidth: 130, max: 4 }} gap={3}>
            {stats.map((s) => (
              <Card key={s.label} padding={4}>
                <HStack hAlign="between" vAlign="center">
                  <Heading level={3}>0</Heading>
                  <Icon icon={s.icon} color={s.color} />
                </HStack>
                <Text type="supporting">{s.label}</Text>
              </Card>
            ))}
          </Grid>

          <Card padding={5}>
            <HStack gap={4} hAlign="between" vAlign="start">
              <VStack gap={2}>
                <Heading level={3}>Công việc theo ca</Heading>
                <Text type="supporting" as="p">
                  Chưa có checklist công việc. Thiết lập đầu việc chuẩn cho từng ca để theo dõi tiến độ tại đây.
                </Text>
                <HStack>
                  <Button label="Thiết lập ngay" variant="secondary" size="sm" href="/tasks" />
                </HStack>
              </VStack>
              <Icon icon={ClipboardList} size="lg" color="accent" />
            </HStack>
          </Card>
        </VStack>

        <VStack gap={4}>
          <Card padding={5}>
            <Grid columns={2} gap={4}>
              <VStack gap={1}>
                <HStack gap={2} vAlign="center">
                  <Icon icon={Wallet} color="accent" />
                  <Heading level={3}>{formatCurrency(0)}</Heading>
                </HStack>
                <Text type="supporting">Lương tạm tính dự kiến trong ngày</Text>
              </VStack>
              <VStack gap={1}>
                <Heading level={3}>{formatCurrency(0)}</Heading>
                <Text type="supporting">Lương tạm tính đến thời điểm hiện tại</Text>
              </VStack>
            </Grid>
          </Card>
          <Card padding={8}>
            <EmptyState
              icon={<EmptyScheduleIllustration />}
              title="Hôm nay chưa xếp lịch làm việc"
              description="Danh sách nhân viên theo ca sẽ hiển thị tại đây sau khi xếp lịch (phân hệ Lịch làm việc)."
              actions={<Button label="Đi tới xếp lịch" variant="primary" href="/schedules" />}
            />
          </Card>
        </VStack>
      </Grid>
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

function EmptyScheduleIllustration() {
  return (
    <svg viewBox="0 0 160 110" className="h-28" aria-hidden="true">
      <ellipse cx="80" cy="98" rx="62" ry="8" fill="var(--color-accent)" fillOpacity=".08" />
      <rect x="32" y="18" width="96" height="74" rx="10" fill="var(--color-background-surface)" stroke="var(--color-border)" strokeWidth="2" />
      <rect x="32" y="18" width="96" height="18" rx="9" fill="var(--color-accent)" fillOpacity=".15" />
      <rect x="50" y="10" width="6" height="16" rx="3" fill="var(--color-accent)" />
      <rect x="104" y="10" width="6" height="16" rx="3" fill="var(--color-accent)" />
      <path d="M62 58l36 20M98 58L62 78" stroke="var(--color-text-secondary)" strokeOpacity=".5" strokeWidth="4" strokeLinecap="round" />
    </svg>
  );
}
