import { useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
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
import { Button } from '@/components/ui/Button';
import { Input, Select } from '@/components/ui/Form';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { RoleGroups } from '@/types/auth';
import { formatCurrency, todayIso } from '@/lib/format';

/**
 * Tổng quan quân số trong ngày. Bố cục tham khảo HRM iPOS; số liệu sẽ được nối API ở Task 5.4
 * (cần dữ liệu chấm công của Cấp 3), hiện hiển thị 0 kèm nhãn "Sắp có".
 */
export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const { branches, load } = useLookupStore();
  const companyWide = hasAnyRole(RoleGroups.AdminOrHr);
  const [branchId, setBranchId] = useState<string>('');
  const [date, setDate] = useState(todayIso());

  useEffect(() => {
    if (hasAnyRole(RoleGroups.Managers)) void load();
  }, [hasAnyRole, load]);

  if (!hasAnyRole(RoleGroups.Managers)) return <EmployeeNotice name={user?.fullName ?? ''} />;

  const visibleBranches = companyWide ? branches : branches.filter((b) => b.id === user?.branchId);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <Select className="w-56" value={companyWide ? branchId : String(user?.branchId ?? '')} onChange={(e) => setBranchId(e.target.value)} disabled={!companyWide}>
          {companyWide && <option value="">Tất cả chi nhánh</option>}
          {visibleBranches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </Select>
        <Input type="date" className="w-44" value={date} onChange={(e) => setDate(e.target.value)} />
        <Button>Lọc</Button>
        <Button variant="secondary" aria-label="Làm mới" icon={<RefreshCw className="h-4 w-4" />} onClick={() => setDate(todayIso())} />
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div className="space-y-4">
          <div className="card relative overflow-hidden bg-gradient-to-br from-primary/15 via-primary/5 to-surface p-5">
            <ShiftIllustration />
            <p className="text-sm font-medium text-foreground/80">Tổng ca làm việc</p>
            <p className="mt-1 text-4xl font-bold text-foreground">0</p>
            <div className="mt-5 space-y-1.5 border-t border-primary/15 pt-4 text-sm">
              <p className="flex items-center gap-2">
                <Users className="h-4 w-4 text-primary" /> <b>0</b> nhân viên được xếp lịch
              </p>
              <p className="flex items-center gap-2">
                <UserCheck className="h-4 w-4 text-success" /> <b>0</b> nhân viên đã chấm công
              </p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            <Stat icon={<Clock3 />} label="Đi muộn" tone="text-warning" />
            <Stat icon={<AlarmClockOff />} label="Về sớm" tone="text-warning" />
            <Stat icon={<LogIn />} label="Quên check-in" tone="text-danger" />
            <Stat icon={<LogOut />} label="Quên check-out" tone="text-danger" />
            <Stat icon={<CalendarClock />} label="Nghỉ phép" tone="text-primary" />
            <Stat icon={<CalendarX2 />} label="Nghỉ không phép" tone="text-danger" />
            <Stat icon={<Sparkles />} label="Ca phát sinh" tone="text-violet-600" />
            <Stat icon={<Smartphone />} label="Trùng thiết bị" tone="text-danger" />
          </div>

          <div className="card flex items-start justify-between gap-4 p-5">
            <div>
              <p className="font-semibold text-foreground">Công việc theo ca</p>
              <p className="mt-1 text-xs text-muted">Chưa có checklist công việc. Thiết lập đầu việc chuẩn cho từng ca để theo dõi tiến độ tại đây.</p>
              <Link to="/tasks" className="mt-3 inline-block text-sm font-medium text-primary hover:underline">
                Thiết lập ngay
              </Link>
            </div>
            <ClipboardList className="h-10 w-10 shrink-0 text-primary/40" />
          </div>
        </div>

        <div className="space-y-4">
          <div className="card grid grid-cols-2 gap-4 bg-gradient-to-r from-primary to-primary-dark p-5 text-white">
            <div>
              <p className="flex items-center gap-1.5 text-2xl font-bold">
                <Wallet className="h-5 w-5" /> {formatCurrency(0)}
              </p>
              <p className="mt-1 text-xs text-white/80">Lương tạm tính dự kiến trong ngày</p>
            </div>
            <div>
              <p className="text-2xl font-bold">{formatCurrency(0)}</p>
              <p className="mt-1 text-xs text-white/80">Lương tạm tính đến thời điểm hiện tại</p>
            </div>
          </div>
          <div className="card flex flex-col items-center justify-center px-6 py-14 text-center">
            <EmptyScheduleIllustration />
            <p className="mt-4 font-medium text-foreground">Hôm nay chưa xếp lịch làm việc</p>
            <p className="mt-1 max-w-xs text-xs text-muted">Danh sách nhân viên theo ca sẽ hiển thị tại đây sau khi xếp lịch (phân hệ Lịch làm việc).</p>
            <Link to="/schedules" className="mt-4 text-sm font-medium text-primary hover:underline">
              Đi tới xếp lịch
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

function Stat({ icon, label, tone }: { icon: ReactNode; label: string; tone: string }) {
  return (
    <div className="card p-4">
      <div className="flex items-center justify-between">
        <p className="text-2xl font-bold text-foreground">0</p>
        <span className={`[&>svg]:h-5 [&>svg]:w-5 ${tone}`}>{icon}</span>
      </div>
      <p className="mt-1 text-xs text-muted">{label}</p>
    </div>
  );
}

function EmployeeNotice({ name }: { name: string }) {
  return (
    <div className="mx-auto mt-10 max-w-lg card p-8 text-center">
      <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-primary/10 text-primary">
        <Smartphone className="h-7 w-7" />
      </div>
      <h1 className="mt-4 text-lg font-semibold">Xin chào {name}!</h1>
      <p className="mt-2 text-sm text-muted">
        Trang quản trị dành cho Quản trị viên, Nhân sự và Quản lý chi nhánh. Vui lòng dùng <b>ứng dụng di động FlexiWork</b> để chấm công, xem lịch ca và gửi đơn từ.
      </p>
      <div className="mt-5 flex justify-center gap-2">
        <Link to="/profile">
          <Button variant="secondary">Hồ sơ cá nhân</Button>
        </Link>
        <Link to="/change-password">
          <Button variant="secondary">Đổi mật khẩu</Button>
        </Link>
      </div>
    </div>
  );
}

/** Hình minh họa tự vẽ (SVG) — không dùng tài nguyên của bên thứ ba. */
function ShiftIllustration() {
  return (
    <svg viewBox="0 0 200 120" className="pointer-events-none absolute -right-2 bottom-0 hidden h-36 opacity-90 sm:block" aria-hidden="true">
      <rect x="20" y="30" width="110" height="80" rx="10" fill="white" stroke="rgb(var(--color-primary))" strokeOpacity=".35" />
      <rect x="20" y="30" width="110" height="20" rx="10" fill="rgb(var(--color-primary))" fillOpacity=".85" />
      {[0, 1, 2, 3].map((r) =>
        [0, 1, 2, 3, 4].map((c) => (
          <rect key={`${r}-${c}`} x={30 + c * 19} y={58 + r * 12} width="13" height="7" rx="2" fill="rgb(var(--color-primary))" fillOpacity={(r + c) % 3 === 0 ? 0.6 : 0.15} />
        )),
      )}
      <circle cx="155" cy="62" r="30" fill="white" stroke="rgb(var(--color-primary))" strokeWidth="4" />
      <path d="M155 44v18l12 8" stroke="rgb(var(--color-primary))" strokeWidth="4" fill="none" strokeLinecap="round" />
      <circle cx="176" cy="90" r="12" fill="#22c55e" />
      <path d="M170 90l4 4 8-8" stroke="white" strokeWidth="3" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function EmptyScheduleIllustration() {
  return (
    <svg viewBox="0 0 160 110" className="h-32" aria-hidden="true">
      <ellipse cx="80" cy="98" rx="62" ry="8" fill="rgb(var(--color-primary))" fillOpacity=".08" />
      <rect x="32" y="18" width="96" height="74" rx="10" fill="white" stroke="rgb(var(--color-border))" strokeWidth="2" />
      <rect x="32" y="18" width="96" height="18" rx="9" fill="rgb(var(--color-primary))" fillOpacity=".15" />
      <rect x="50" y="10" width="6" height="16" rx="3" fill="rgb(var(--color-primary))" />
      <rect x="104" y="10" width="6" height="16" rx="3" fill="rgb(var(--color-primary))" />
      <path d="M62 58l36 20M98 58L62 78" stroke="rgb(var(--color-muted))" strokeOpacity=".5" strokeWidth="4" strokeLinecap="round" />
    </svg>
  );
}
