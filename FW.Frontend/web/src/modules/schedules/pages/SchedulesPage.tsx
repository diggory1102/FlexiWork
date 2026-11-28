import { useCallback, useDeferredValue, useEffect, useMemo, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { ButtonGroup } from '@astryxdesign/core/ButtonGroup';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { CalendarCheck, ChevronLeft, ChevronRight, Copy, Eraser, Users, Zap } from 'lucide-react';
import { PageFrame } from '@/layouts/PageFrame';
import { EmptyState, ErrorList, Spinner, type ErrorDetail } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { SearchInput } from '@/components/ui/ListCard';
import { scheduleApi, shiftApi, staffingApi } from '@/services/schedulingApi';
import { userApi } from '@/services/userApi';
import { getErrorDetail, getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { formatDate, todayIso } from '@/lib/format';
import { addDays, startOfWeek, weekDays } from '@/lib/dates';
import { matchesSearch } from '@/lib/search';
import { RoleGroups } from '@/types/auth';
import type { EffectiveStaffing, Schedule, Shift } from '@/types/scheduling';
import { ScheduleGrid, type GridEmployee } from '../components/ScheduleGrid';
import { AddShiftModal, QuickScheduleModal, ScheduleDetailModal } from '../components/ScheduleDialogs';
import { StaffingDialog } from '../components/StaffingDialog';
import { legend } from '../scheduleUi';

type Confirm = 'publish' | 'copy' | 'clear' | null;

/**
 * Xếp lịch theo tuần cho một chi nhánh (UC 3.2 – 3.3): lịch mới là bản nháp, công bố để nhân viên thấy trên app.
 * Admin/HR chọn chi nhánh; Quản lý luôn làm việc trên chi nhánh của mình.
 */
export function SchedulesPage() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const ownBranchId = useAuthStore((s) => s.user?.branchId ?? null);
  const { branches, load: loadLookups } = useLookupStore();

  const today = todayIso();
  const [weekStart, setWeekStart] = useState(() => startOfWeek(today));
  const [branchId, setBranchId] = useState<number | null>(isCompanyWide ? null : ownBranchId);
  const days = useMemo(() => weekDays(weekStart), [weekStart]);
  const range = useMemo(() => ({ branchId: branchId ?? undefined, fromDate: days[0], toDate: days[6] }), [branchId, days]);

  const [schedules, setSchedules] = useState<Schedule[]>([]);
  const [employees, setEmployees] = useState<GridEmployee[]>([]);
  const [shifts, setShifts] = useState<Shift[]>([]);
  const [staffing, setStaffing] = useState<EffectiveStaffing[]>([]);
  const [staffingTarget, setStaffingTarget] = useState<{ open: boolean; shiftId?: number; date?: string }>({ open: false });
  const [loading, setLoading] = useState(false);
  const [report, setReport] = useState<ErrorDetail | null>(null);

  const [addTarget, setAddTarget] = useState<{ employee: GridEmployee; day: string } | null>(null);
  const [quickOpen, setQuickOpen] = useState(false);
  const [detail, setDetail] = useState<Schedule | null>(null);
  const [confirm, setConfirm] = useState<Confirm>(null);
  const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState('');
  // Lưới có thể rất dài: để ô nhập phản hồi ngay, lọc lại lưới sau
  const query = useDeferredValue(search);
  const visibleEmployees = useMemo(() => employees.filter((e) => matchesSearch(query, e.fullName, e.employeeCode)), [employees, query]);

  useEffect(() => void loadLookups().catch(() => undefined), [loadLookups]);

  // Admin/HR: mặc định chi nhánh đang hoạt động đầu tiên
  useEffect(() => {
    if (isCompanyWide && branchId == null && branches.length > 0) {
      setBranchId((branches.find((b) => b.status === 'active') ?? branches[0]).id);
    }
  }, [isCompanyWide, branchId, branches]);

  // Từ khóa của chi nhánh trước không còn ý nghĩa khi chuyển chi nhánh
  useEffect(() => setSearch(''), [branchId]);

  const load = useCallback(async () => {
    if (branchId == null) return;
    setLoading(true);
    try {
      const [scheduleRes, users, shiftRes, staffingRes] = await Promise.all([
        scheduleApi.branch(range),
        userApi.listAll({ branchId, status: 'active' }),
        shiftApi.available(branchId),
        staffingApi.effective(range),
      ]);
      setSchedules(scheduleRes.data);
      setShifts(shiftRes.data);
      setStaffing(staffingRes.data);

      // Nhân viên đang làm tại chi nhánh + người đã có lịch trong tuần (VD vừa chuyển chi nhánh / nghỉ việc)
      const people = new Map<number, GridEmployee>(users.map((u) => [u.id, u]));
      for (const s of scheduleRes.data) {
        if (!people.has(s.userId)) people.set(s.userId, { id: s.userId, fullName: s.fullName, employeeCode: s.employeeCode });
      }
      setEmployees([...people.values()].sort((a, b) => a.fullName.localeCompare(b.fullName, 'vi')));
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [branchId, range]);

  useEffect(() => {
    setReport(null);
    void load();
  }, [load]);

  const drafts = schedules.filter((s) => !s.isPublished && s.status !== 'cancelled').length;
  const published = schedules.filter((s) => s.isPublished && s.status !== 'cancelled').length;
  const cancelled = schedules.filter((s) => s.status === 'cancelled').length;
  const isPastWeek = days[6] < today;

  const runConfirmed = async () => {
    setBusy(true);
    try {
      if (confirm === 'publish') {
        const { message } = await scheduleApi.publish(range);
        toast.success(message);
      } else if (confirm === 'clear') {
        const { message } = await scheduleApi.deleteDrafts(range);
        toast.success(message);
      } else if (confirm === 'copy') {
        const { data, message } = await scheduleApi.copy({ branchId: branchId ?? undefined, sourceFrom: addDays(weekStart, -7), targetFrom: weekStart, days: 7 });
        toast.success(message);
        setReport(data.skipped > 0 ? { message: `Bỏ qua ${data.skipped} ca khi sao chép:`, errors: data.skippedReasons } : null);
      }
      setConfirm(null);
      await load();
    } catch (err) {
      setConfirm(null);
      setReport(getErrorDetail(err));
    } finally {
      setBusy(false);
    }
  };

  const confirmTexts: Record<Exclude<Confirm, null>, { title: string; message: string; button: string; tone: 'primary' | 'danger' }> = {
    publish: {
      title: 'Công bố lịch tuần',
      message: `Công bố ${drafts} ca nháp của tuần ${formatDate(days[0])} – ${formatDate(days[6])}? Nhân viên sẽ thấy lịch trên ứng dụng và chấm công được theo lịch này.`,
      button: 'Công bố',
      tone: 'primary',
    },
    copy: {
      title: 'Sao chép lịch tuần trước',
      message: `Sao chép các ca của tuần ${formatDate(addDays(weekStart, -7))} – ${formatDate(addDays(weekStart, -1))} sang tuần này (dạng nháp). Ca của ngày đã qua, nhân viên đã chuyển đi hoặc bị trùng giờ sẽ được bỏ qua.`,
      button: 'Sao chép',
      tone: 'primary',
    },
    clear: {
      title: 'Xóa lịch nháp',
      message: `Xóa ${drafts} ca nháp của tuần này? Ca đã công bố không bị ảnh hưởng.`,
      button: 'Xóa nháp',
      tone: 'danger',
    },
  };

  const branchOptions = branches.map((b) => ({ value: String(b.id), label: `${b.branchCode} – ${b.name}` }));

  return (
    <>
      <PageFrame
        title="Xếp lịch làm việc"
        layout="dense"
        actions={
          <>
            {isCompanyWide && (
              <Selector
                label="Chi nhánh"
                isLabelHidden
                width={260}
                options={branchOptions}
                value={branchId ? String(branchId) : ''}
                onChange={(v) => v && setBranchId(Number(v))}
                placeholder="Chọn chi nhánh"
              />
            )}
            <ButtonGroup label="Chọn tuần">
              <IconButton label="Tuần trước" tooltip="Tuần trước" variant="secondary" icon={<Icon icon={ChevronLeft} />} onClick={() => setWeekStart(addDays(weekStart, -7))} />
              <Button label="Tuần này" variant="secondary" onClick={() => setWeekStart(startOfWeek(today))} />
              <IconButton label="Tuần sau" tooltip="Tuần sau" variant="secondary" icon={<Icon icon={ChevronRight} />} onClick={() => setWeekStart(addDays(weekStart, 7))} />
            </ButtonGroup>
            <Text weight="semibold" hasTabularNumbers>
              {formatDate(days[0])} – {formatDate(days[6])}
            </Text>
            <Button label="Xếp nhanh" variant="secondary" icon={<Icon icon={Zap} />} onClick={() => setQuickOpen(true)} isDisabled={!branchId || isPastWeek} />
            <Button
              label={drafts > 0 ? `Công bố ${drafts} ca` : 'Công bố'}
              variant="primary"
              icon={<Icon icon={CalendarCheck} />}
              onClick={() => setConfirm('publish')}
              isDisabled={drafts === 0}
            />
          </>
        }
        toolbar={
          <HStack gap={3} wrap="wrap" vAlign="center" hAlign="between">
            <HStack gap={1} wrap="wrap" vAlign="center">
              <SearchInput value={search} onChange={setSearch} placeholder="Tìm theo tên hoặc mã nhân viên" width={280} />
              <Button label="Sao chép tuần trước" variant="ghost" size="sm" icon={<Icon icon={Copy} />} onClick={() => setConfirm('copy')} isDisabled={!branchId || isPastWeek} />
              <Button label="Xóa nháp" variant="ghost" size="sm" icon={<Icon icon={Eraser} />} onClick={() => setConfirm('clear')} isDisabled={drafts === 0} />
              <Button label="Định biên ca" variant="ghost" size="sm" icon={<Icon icon={Users} />} onClick={() => setStaffingTarget({ open: true })} isDisabled={!branchId} />
              <Text type="supporting">
                {schedules.length === 0 ? 'Tuần này chưa có ca nào.' : `${published} ca đã công bố · ${drafts} ca nháp${cancelled > 0 ? ` · ${cancelled} ca đã hủy` : ''}`}
                {query.trim() ? ` · Hiện ${visibleEmployees.length}/${employees.length} nhân viên` : ''}
              </Text>
            </HStack>
            <HStack gap={1} wrap="wrap">
              {legend.map((l) => (
                <Token key={l.label} size="sm" color={l.color} label={l.label} />
              ))}
            </HStack>
          </HStack>
        }
      >
        <div className="min-h-full bg-surface">
        {report && (
          <HStack padding={3}>
            <ErrorList error={report} status="warning" />
          </HStack>
        )}

        {branchId == null ? (
          <EmptyState title="Chưa chọn chi nhánh" description={isCompanyWide ? 'Chọn chi nhánh để xem và xếp lịch.' : 'Tài khoản Quản lý chưa được gán chi nhánh.'} />
        ) : loading && employees.length === 0 ? (
          <HStack hAlign="center" padding={10}>
            <Spinner />
          </HStack>
        ) : employees.length === 0 ? (
          <EmptyState title="Chi nhánh chưa có nhân viên" description="Thêm nhân viên vào chi nhánh ở trang Nhân viên để xếp lịch." />
        ) : visibleEmployees.length === 0 ? (
          <EmptyState title="Không tìm thấy nhân viên" description={`Không có nhân viên nào khớp "${query.trim()}". Thử tên hoặc mã khác.`} />
        ) : (
          <ScheduleGrid days={days} today={today} employees={visibleEmployees} schedules={schedules} shifts={shifts} staffing={staffing} onStaffing={(shift, day) => setStaffingTarget({ open: true, shiftId: shift.id, date: day })} onAdd={(employee, day) => setAddTarget({ employee, day })} onOpen={setDetail} />
        )}
        </div>
      </PageFrame>

      <AddShiftModal
        target={addTarget}
        branchId={branchId ?? undefined}
        shifts={shifts}
        existing={addTarget ? schedules.filter((s) => s.userId === addTarget.employee.id && s.workDate === addTarget.day) : []}
        onClose={() => setAddTarget(null)}
        onSaved={() => void load()}
      />
      <QuickScheduleModal
        open={quickOpen}
        branchId={branchId ?? undefined}
        days={days}
        today={today}
        employees={employees}
        shifts={shifts}
        onClose={() => setQuickOpen(false)}
        onSaved={() => void load()}
      />
      <StaffingDialog
        open={staffingTarget.open}
        branchId={branchId ?? undefined}
        shifts={shifts}
        initial={staffingTarget.open ? { shiftId: staffingTarget.shiftId, date: staffingTarget.date } : null}
        onClose={() => setStaffingTarget({ open: false })}
        onChanged={() => void load()}
      />
      <ScheduleDetailModal schedule={detail} today={today} onClose={() => setDetail(null)} onChanged={() => void load()} />
      {confirm && (
        <ConfirmDialog
          open
          title={confirmTexts[confirm].title}
          message={confirmTexts[confirm].message}
          confirmText={confirmTexts[confirm].button}
          tone={confirmTexts[confirm].tone}
          loading={busy}
          onConfirm={runConfirmed}
          onClose={() => setConfirm(null)}
        />
      )}
    </>
  );
}
