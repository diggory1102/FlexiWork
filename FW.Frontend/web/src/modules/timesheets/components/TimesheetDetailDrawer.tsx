import { useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Grid } from '@astryxdesign/core/Grid';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { CalendarCheck, Clock3, Moon, TriangleAlert } from 'lucide-react';
import { Avatar } from '@/components/ui/Layout';
import { Badge, FormError, Notice, Spinner } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { Modal } from '@/components/ui/Modal';
import { StatCard } from '@/components/ui/StatCard';
import { useApiData } from '@/hooks/useApiData';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { timesheetApi } from '@/services/timesheetApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatCurrency, formatDate, formatDateTime, formatTime } from '@/lib/format';
import { formatMinutes } from '@/lib/dates';
import type { OvertimeLine, Timesheet, TimesheetLine } from '@/types/timesheets';
import { num, outcomeLabels, outcomeTone, timesheetStatusLabels, timesheetStatusTone } from '../timesheetUi';

/** Ngăn chi tiết bảng công của một nhân viên: số liệu, từng ca trong tháng, giờ tăng ca; xác nhận / khóa / mở lại. */
export function TimesheetDetailDrawer({
  row,
  onClose,
  onChanged,
}: {
  row: Timesheet | null;
  onClose: () => void;
  /** Gọi sau khi xác nhận / khóa / mở lại để làm mới danh sách. */
  onChanged: () => void;
}) {
  const id = row?.id ?? null;
  const { data: detail, loading, error, reload } = useApiData(() => timesheetApi.detail(id!), [id], { enabled: id != null, toastError: false });
  const [busy, setBusy] = useState<'confirm' | 'lock' | null>(null);
  const [reopening, setReopening] = useState(false);
  const [note, setNote] = useState('');
  const reopenForm = useFormSubmit();
  const employeeId = row?.employee.id;

  const run = async (kind: 'confirm' | 'lock') => {
    if (!row || employeeId == null) return;
    const scope = { year: row.year, month: row.month, userIds: [employeeId] };
    setBusy(kind);
    try {
      const { data, message } = kind === 'confirm' ? await timesheetApi.confirm(scope) : await timesheetApi.lock(scope);
      if (kind === 'confirm' && 'blocked' in data && data.blocked.length > 0) {
        toast.error(data.blocked.map((b) => `${b.fullName}: ${b.reason}`).join(' '));
      } else {
        toast.success(message);
      }
      onChanged();
      await reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(null);
    }
  };

  const reopen = () => {
    if (id == null) return;
    if (!note.trim()) return reopenForm.setError('Vui lòng ghi lý do mở lại bảng công.');
    void reopenForm.run(
      async () => {
        await timesheetApi.reopen(id, note.trim());
        return 'Đã mở lại bảng công về nháp.';
      },
      () => {
        setReopening(false);
        setNote('');
        onChanged();
        void reload();
      },
    );
  };

  const ts = detail?.timesheet ?? row;
  const f = ts?.figures;

  const lineColumns: Column<TimesheetLine>[] = [
    { key: 'date', header: 'Ngày', width: 90, render: (l) => formatDate(l.workDate).slice(0, 5) },
    {
      key: 'shift',
      header: 'Ca',
      render: (l) => (
        <VStack gap={0.5}>
          <Text weight="medium">{l.shiftName}</Text>
          <Text type="supporting">
            {l.startTime}–{l.endTime}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'outcome',
      header: 'Kết quả',
      render: (l) => (
        <VStack gap={0.5} hAlign="start">
          <Badge tone={outcomeTone[l.outcome]}>{outcomeLabels[l.outcome]}</Badge>
          {l.leaveKind && <Text type="supporting">{l.leaveKind === 'paid' ? 'Có lương' : 'Không lương'}</Text>}
          {l.exempted && <Text type="supporting">Đã giải trình</Text>}
        </VStack>
      ),
    },
    { key: 'time', header: 'Vào – ra', className: 'whitespace-nowrap', render: (l) => (l.checkIn ? `${formatTime(l.checkIn)} – ${formatTime(l.checkOut)}` : '—') },
    {
      key: 'violation',
      header: 'Muộn / sớm',
      align: 'end',
      render: (l) => {
        const parts = [l.lateMinutes > 0 && `muộn ${l.lateMinutes}'`, l.earlyMinutes > 0 && `sớm ${l.earlyMinutes}'`].filter(Boolean);
        return parts.length ? parts.join(' · ') : '—';
      },
    },
    { key: 'units', header: 'Công', align: 'end', render: (l) => (l.outcome === 'worked' || l.outcome === 'leave' ? num(l.workUnits - l.deductedUnits) : '—') },
    { key: 'penalty', header: 'Phạt', align: 'end', render: (l) => (l.penalty > 0 ? formatCurrency(l.penalty) : '—') },
  ];

  const overtimeColumns: Column<OvertimeLine>[] = [
    { key: 'date', header: 'Ngày', render: (o) => formatDate(o.startAt) },
    { key: 'time', header: 'Giờ', render: (o) => `${formatTime(o.startAt)} – ${formatTime(o.endAt)}` },
    { key: 'hours', header: 'Số giờ', align: 'end', render: (o) => num(o.hours) },
  ];

  const hasActions = !!detail && (detail.canConfirm || detail.canLock || detail.canReopen);

  return (
    <>
      <DetailDrawer
        open={!!row}
        onClose={onClose}
        label="Chi tiết bảng công"
        title={`Bảng công tháng ${row ? `${String(row.month).padStart(2, '0')}/${row.year}` : ''}`}
        subtitle={row ? `${row.employee.fullName} · ${row.employee.employeeCode}` : undefined}
        width={820}
        footer={
          hasActions ? (
            <HStack gap={2} hAlign="end">
              {detail.canReopen && <Button label="Mở lại về nháp" variant="secondary" isDisabled={busy !== null} onClick={() => {
                  reopenForm.setError(null);
                  setReopening(true);
                }} />}
              {detail.canLock && <Button label="Khóa bảng công" variant="secondary" isLoading={busy === 'lock'} isDisabled={busy !== null} onClick={() => run('lock')} />}
              {detail.canConfirm && <Button label="Xác nhận bảng công" variant="primary" isLoading={busy === 'confirm'} isDisabled={busy !== null} onClick={() => run('confirm')} />}
            </HStack>
          ) : undefined
        }
      >
        <VStack gap={4}>
          <Notice title={error} />
          {row?.id == null && row && (
            <Notice status="info" title="Chưa có bảng công đã lưu" description="Bấm “Tính bảng công” ở trang danh sách để lưu số liệu tháng này." />
          )}
          {loading && !detail && (
            <div className="flex justify-center py-10">
              <Spinner />
            </div>
          )}
          {ts && f && (
            <>
              <HStack gap={3} vAlign="center" hAlign="between" wrap="wrap">
                <HStack gap={3} vAlign="center">
                  <Avatar name={ts.employee.fullName} url={ts.employee.avatarUrl} size={48} />
                  <VStack gap={0.5}>
                    <Heading level={3}>{ts.employee.fullName}</Heading>
                    <Text type="supporting">{[ts.employee.positionTitle, ts.branchName].filter(Boolean).join(' · ') || '—'}</Text>
                  </VStack>
                </HStack>
                <Badge tone={timesheetStatusTone[ts.status]}>{timesheetStatusLabels[ts.status]}</Badge>
              </HStack>

              {detail?.drifted && (
                <Notice
                  status="warning"
                  title="Dữ liệu nguồn đã thay đổi sau khi xác nhận"
                  description="Số liệu đã lưu khác số liệu tính trực tiếp từ chấm công hiện tại. Mở lại bảng công để tính lại nếu cần."
                />
              )}
              {(f.pendingLogs > 0 || f.missingCheckouts > 0) && (
                <Notice
                  status="warning"
                  title="Còn mục cần xử lý trước khi xác nhận"
                  description={[f.pendingLogs > 0 && `${f.pendingLogs} lượt chấm công chờ duyệt`, f.missingCheckouts > 0 && `${f.missingCheckouts} ca thiếu check-out`]
                    .filter(Boolean)
                    .join(' · ')}
                />
              )}

              <Grid columns={{ minWidth: 150, max: 4 }} gap={3}>
                <StatCard icon={CalendarCheck} label={`Ngày công / ${num(ts.standardWorkDays)}`} value={num(f.actualWorkDays)} tone="success" hint={`${num(f.actualWorkHours)} giờ làm`} />
                <StatCard icon={Clock3} label="Ca đã làm / đã xếp" value={`${f.workedShifts}/${f.scheduledShifts}`} tone="accent" hint={`${f.absentShifts} ca vắng`} />
                <StatCard
                  icon={TriangleAlert}
                  label="Vi phạm"
                  value={f.violationCount}
                  tone={f.violationCount > 0 ? 'warning' : 'secondary'}
                  hint={`Muộn ${formatMinutes(f.totalLateMinutes)} · sớm ${formatMinutes(f.totalEarlyMinutes)}`}
                />
                <StatCard icon={Moon} label="Ca đêm / ngày ca gãy" value={`${f.nightShifts} / ${f.splitShiftDays}`} tone="purple" hint={`OT ${num(f.totalOvertimeHours)} giờ`} />
              </Grid>
              <Grid columns={{ minWidth: 150, max: 4 }} gap={3}>
                <StatCard icon={CalendarCheck} label="Nghỉ có lương" value={`${num(f.paidLeaveDays)} ngày`} tone="accent" />
                <StatCard icon={CalendarCheck} label="Nghỉ không lương" value={`${num(f.unpaidLeaveDays)} ngày`} tone="secondary" />
                <StatCard icon={TriangleAlert} label="Công bị trừ" value={num(f.deductedWorkDays)} tone="warning" />
                <StatCard icon={TriangleAlert} label="Tiền phạt" value={formatCurrency(f.penaltyAmount)} tone="error" />
              </Grid>

              {detail && (
                <>
                  <VStack gap={2}>
                    <Heading level={3}>Từng ca trong tháng</Heading>
                    <DataTable columns={lineColumns} rows={detail.lines} rowKey={(l) => l.scheduleId} minWidth={0} emptyText="Chưa có ca nào được xếp trong tháng" />
                  </VStack>
                  {detail.overtime.length > 0 && (
                    <VStack gap={2}>
                      <Heading level={3}>Tăng ca đã duyệt</Heading>
                      <DataTable columns={overtimeColumns} rows={detail.overtime} rowKey={(o) => o.startAt} minWidth={0} />
                    </VStack>
                  )}
                </>
              )}

              <Text type="supporting">
                {ts.calculatedAt && <>Tính lúc {formatDateTime(ts.calculatedAt)}. </>}
                {ts.confirmedByName && <>Xác nhận bởi {ts.confirmedByName} ({formatDateTime(ts.confirmedAt)}). </>}
                {ts.lockedByName && <>Khóa bởi {ts.lockedByName} ({formatDateTime(ts.lockedAt)}). </>}
                {ts.note && <>Ghi chú: {ts.note}</>}
              </Text>
            </>
          )}
        </VStack>
      </DetailDrawer>

      <Modal
        open={reopening}
        onClose={() => setReopening(false)}
        size="sm"
        purpose="form"
        title="Mở lại bảng công"
        description="Bảng công trở về trạng thái nháp để tính lại. Lý do được lưu lại để đối chiếu."
        footer={
          <>
            <Button label="Hủy" variant="secondary" onClick={() => setReopening(false)} />
            <Button label="Mở lại" variant="primary" isLoading={reopenForm.saving} onClick={reopen} />
          </>
        }
      >
        <VStack gap={3}>
          <FormError message={reopenForm.error} />
          <TextArea label="Lý do mở lại" isRequired value={note} onChange={setNote} maxLength={255} />
        </VStack>
      </Modal>
    </>
  );
}
