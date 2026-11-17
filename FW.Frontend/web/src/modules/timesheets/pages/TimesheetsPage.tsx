import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { Text } from '@astryxdesign/core/Text';
import { Calculator, CheckCheck, Lock } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { EmployeeCell } from '@/components/ui/EmployeeCell';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { Badge, Notice } from '@/components/ui/Feedback';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { ExportButton } from '@/components/ui/ExportButton';
import { BranchSelector, useBranchFilter } from '@/components/ui/BranchFilter';
import { MonthPicker, currentYearMonth, type YearMonth } from '@/components/ui/MonthPicker';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { timesheetApi } from '@/services/timesheetApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';
import { formatCurrency } from '@/lib/format';
import { RoleGroups } from '@/types/auth';
import type { Timesheet, TimesheetBlocker, TimesheetFilter, TimesheetStatus, TimesheetSummary } from '@/types/timesheets';
import { TimesheetDetailDrawer } from '../components/TimesheetDetailDrawer';
import { num, timesheetStatusLabels, timesheetStatusTone } from '../timesheetUi';

type StatusTab = TimesheetStatus | 'all';
type Action = 'calculate' | 'confirm' | 'lock';

const monthLabel = ({ year, month }: YearMonth) => `${String(month).padStart(2, '0')}/${year}`;

/**
 * Bảng công tháng (UC 6.x): tính từ chấm công → Quản lý xác nhận → HR khóa khi tháng đã kết thúc → chốt lương.
 * Quản lý chỉ thao tác chi nhánh mình; Admin / HR chọn chi nhánh hoặc toàn doanh nghiệp.
 */
export function TimesheetsPage() {
  const canLock = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const branchFilter = useBranchFilter();
  const [ym, setYm] = useState<YearMonth>(currentYearMonth());
  const [tab, setTab] = useState<StatusTab>('all');
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [summary, setSummary] = useState<TimesheetSummary | null>(null);
  const [selected, setSelected] = useState<Timesheet | null>(null);
  const [action, setAction] = useState<Action | null>(null);
  const [busy, setBusy] = useState(false);
  const [blocked, setBlocked] = useState<TimesheetBlocker[] | null>(null);
  const { apiBranchId } = branchFilter;

  const { setQuery, page, rows, loading, error, reload } = usePagedList<Timesheet, TimesheetFilter>(timesheetApi.list, {
    pageSize: 20,
    year: ym.year,
    month: ym.month,
  });

  useEffect(
    () => setQuery({ year: ym.year, month: ym.month, branchId: apiBranchId, status: tab === 'all' ? '' : tab, search: debounced }),
    [ym, apiBranchId, tab, debounced, setQuery],
  );
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const loadSummary = useCallback(() => {
    timesheetApi
      .summary(ym.year, ym.month, apiBranchId)
      .then(({ data }) => setSummary(data))
      .catch((err) => toast.error(getErrorMessage(err)));
  }, [ym, apiBranchId]);
  useEffect(loadSummary, [loadSummary]);

  const refresh = () => {
    void reload();
    loadSummary();
  };

  const scope = { year: ym.year, month: ym.month, branchId: apiBranchId === '' ? null : apiBranchId };

  const runAction = async () => {
    if (!action) return;
    setBusy(true);
    try {
      if (action === 'calculate') {
        toast.success((await timesheetApi.calculate(scope)).message);
      } else if (action === 'confirm') {
        const { data, message } = await timesheetApi.confirm(scope);
        if (data.confirmed > 0) toast.success(message);
        if (data.blocked.length > 0) setBlocked(data.blocked);
        else if (data.confirmed === 0) toast.info(message);
      } else {
        toast.success((await timesheetApi.lock(scope)).message);
      }
      setAction(null);
      refresh();
    } catch (err) {
      toast.error(getErrorMessage(err));
      setAction(null);
    } finally {
      setBusy(false);
    }
  };

  const tabLabel = (label: string, count: number | undefined) => (count === undefined ? label : `${label} (${count})`);

  const columns: Column<Timesheet>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      width: 230,
      render: (t) => <EmployeeCell name={t.employee.fullName} code={t.employee.employeeCode} avatarUrl={t.employee.avatarUrl} sub={branchFilter.companyWide ? t.branchName : null} />,
    },
    {
      key: 'shifts',
      header: 'Ca làm / xếp',
      align: 'end',
      render: (t) => (
        <VStack gap={0.5} hAlign="end">
          <Text hasTabularNumbers>
            {t.figures.workedShifts}/{t.figures.scheduledShifts}
          </Text>
          {t.figures.pendingLogs > 0 && <Text type="supporting">{t.figures.pendingLogs} chờ duyệt</Text>}
          {t.figures.missingCheckouts > 0 && <Text type="supporting">{t.figures.missingCheckouts} thiếu check-out</Text>}
        </VStack>
      ),
    },
    {
      key: 'days',
      header: 'Ngày công',
      align: 'end',
      render: (t) => (
        <VStack gap={0.5} hAlign="end">
          <Text weight="semibold" hasTabularNumbers>
            {num(t.figures.actualWorkDays)}
          </Text>
          <Text type="supporting">{num(t.figures.actualWorkHours)} giờ</Text>
        </VStack>
      ),
    },
    {
      key: 'violations',
      header: 'Vi phạm',
      align: 'end',
      render: (t) =>
        t.figures.violationCount === 0 && t.figures.absentShifts === 0 ? (
          <Text type="supporting">—</Text>
        ) : (
          <VStack gap={0.5} hAlign="end">
            <Text hasTabularNumbers>{t.figures.violationCount} lần</Text>
            {t.figures.absentShifts > 0 && <Text type="supporting">{t.figures.absentShifts} ca vắng</Text>}
          </VStack>
        ),
    },
    { key: 'ot', header: 'OT (giờ)', align: 'end', render: (t) => (t.figures.totalOvertimeHours > 0 ? <Text hasTabularNumbers>{num(t.figures.totalOvertimeHours)}</Text> : <Text type="supporting">—</Text>) },
    {
      key: 'leave',
      header: 'Nghỉ phép',
      align: 'end',
      render: (t) =>
        t.figures.paidLeaveDays + t.figures.unpaidLeaveDays === 0 ? (
          <Text type="supporting">—</Text>
        ) : (
          <VStack gap={0.5} hAlign="end">
            <Text hasTabularNumbers>{num(t.figures.paidLeaveDays)} có lương</Text>
            {t.figures.unpaidLeaveDays > 0 && <Text type="supporting">{num(t.figures.unpaidLeaveDays)} không lương</Text>}
          </VStack>
        ),
    },
    {
      key: 'penalty',
      header: 'Tiền phạt',
      align: 'end',
      render: (t) => (t.figures.penaltyAmount > 0 ? <Text hasTabularNumbers>{formatCurrency(t.figures.penaltyAmount)}</Text> : <Text type="supporting">—</Text>),
    },
    {
      key: 'status',
      header: 'Trạng thái',
      width: 140,
      render: (t) => (
        <VStack gap={0.5} hAlign="start">
          <Badge tone={timesheetStatusTone[t.status]}>{timesheetStatusLabels[t.status]}</Badge>
          {t.isPreview && <Text type="supporting">Tạm tính</Text>}
        </VStack>
      ),
    },
  ];

  const unconfirmed = summary?.draft ?? 0;
  const actionText: Record<Action, { title: string; message: string; confirm: string }> = {
    calculate: {
      title: 'Tính lại bảng công',
      message: `Tính lại bảng công nháp tháng ${monthLabel(ym)} từ dữ liệu chấm công hiện tại. Bảng đã xác nhận hoặc đã khóa không bị thay đổi.`,
      confirm: 'Tính bảng công',
    },
    confirm: {
      title: 'Xác nhận bảng công',
      message: `Xác nhận ${unconfirmed} bảng công nháp tháng ${monthLabel(ym)}. Nhân viên còn lượt chấm công chờ duyệt sẽ được liệt kê và chưa xác nhận được.`,
      confirm: 'Xác nhận',
    },
    lock: {
      title: 'Khóa bảng công',
      message: `Khóa các bảng công đã xác nhận của tháng ${monthLabel(ym)}. Sau khi khóa, số liệu cố định và không duyệt thêm chấm công hay đơn từ của tháng này; chỉ HR / Admin mở lại được.`,
      confirm: 'Khóa bảng công',
    },
  };

  return (
    <VStack gap={4}>
      <PageHeader
        title="Bảng công"
        description="Tổng hợp công từ chấm công, ca làm, nghỉ phép và tăng ca. Xác nhận rồi khóa trước khi tính lương."
        actions={
          <>
            <MonthPicker value={ym} onChange={setYm} max={currentYearMonth()} />
            <BranchSelector filter={branchFilter} />
            <ExportButton url={timesheetApi.exportUrl} params={{ year: ym.year, month: ym.month, branchId: apiBranchId }} />
          </>
        }
      />

      <HStack gap={2} wrap="wrap" vAlign="center">
        <Button label="Tính bảng công" variant="secondary" icon={<Icon icon={Calculator} />} onClick={() => setAction('calculate')} />
        <Button label="Xác nhận bảng công" variant="primary" icon={<Icon icon={CheckCheck} />} isDisabled={unconfirmed === 0} onClick={() => setAction('confirm')} />
        {canLock && (
          <Button
            label="Khóa bảng công"
            variant="secondary"
            icon={<Icon icon={Lock} />}
            isDisabled={!summary?.monthEnded || (summary?.confirmed ?? 0) === 0}
            onClick={() => setAction('lock')}
          />
        )}
        {summary && !summary.monthEnded && canLock && <Text type="supporting">Chỉ khóa được sau khi tháng kết thúc.</Text>}
      </HStack>

      <ListCard>
        <Toolbar>
          <TabList value={tab} onChange={(v) => setTab(v as StatusTab)}>
            <Tab value="all" label="Tất cả" />
            <Tab value="draft" label={tabLabel('Nháp', summary?.draft)} />
            <Tab value="confirmed" label={tabLabel('Đã xác nhận', summary?.confirmed)} />
            <Tab value="locked" label={tabLabel('Đã khóa', summary?.locked)} />
            <Tab value="finalized" label={tabLabel('Đã chốt lương', summary?.finalized)} />
          </TabList>
          <SearchInput placeholder="Tìm theo tên hoặc mã nhân viên…" value={search} onChange={setSearch} />
        </Toolbar>
        <DataTable
          columns={columns}
          rows={rows}
          rowKey={(t) => `${t.employee.id}-${t.year}-${t.month}`}
          loading={loading}
          emptyText="Chưa có bảng công trong tháng này"
          onRowClick={setSelected}
        />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <TimesheetDetailDrawer row={selected} onClose={() => setSelected(null)} onChanged={refresh} />

      <ConfirmDialog
        open={action !== null}
        title={action ? actionText[action].title : ''}
        message={action ? actionText[action].message : ''}
        confirmText={action ? actionText[action].confirm : undefined}
        tone="primary"
        loading={busy}
        onConfirm={runAction}
        onClose={() => setAction(null)}
      />

      <Modal
        open={blocked !== null}
        onClose={() => setBlocked(null)}
        title="Một số bảng công chưa xác nhận được"
        description="Xử lý các mục sau (duyệt chấm công, bổ sung check-out…) rồi xác nhận lại."
        footer={<Button label="Đã hiểu" variant="primary" onClick={() => setBlocked(null)} />}
      >
        <VStack gap={2}>
          {blocked?.map((b) => (
            <Notice key={b.userId} status="warning" title={b.fullName} description={b.reason} />
          ))}
        </VStack>
      </Modal>
    </VStack>
  );
}
