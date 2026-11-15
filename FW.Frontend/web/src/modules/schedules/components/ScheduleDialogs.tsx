import { useEffect, useMemo, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { CheckboxList, CheckboxListItem } from '@astryxdesign/core/CheckboxList';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { MultiSelector } from '@astryxdesign/core/MultiSelector';
import { ScrollableArea } from '@astryxdesign/core/ScrollableArea';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { Modal } from '@/components/ui/Modal';
import { ErrorList, Notice, type ErrorDetail } from '@/components/ui/Feedback';
import { scheduleApi } from '@/services/schedulingApi';
import { getErrorDetail } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatDate } from '@/lib/format';
import { formatDayMonth, formatMinutes, weekdayLabels } from '@/lib/dates';
import type { Schedule, ScheduleItem, Shift } from '@/types/scheduling';
import { scheduleColor, scheduleStateLabel, shiftLabel } from '../scheduleUi';
import type { GridEmployee } from './ScheduleGrid';

/** Gửi danh sách ca (bản nháp); lỗi xung đột hiển thị từng dòng trong hộp thoại. */
function useCreateSchedules(branchId: number | undefined, onSaved: () => void, onClose: () => void) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ErrorDetail | null>(null);

  const submit = async (items: ScheduleItem[]) => {
    if (items.length === 0) return setError({ message: 'Chưa chọn ca nào để xếp.', errors: [] });
    setSaving(true);
    setError(null);
    try {
      const { message } = await scheduleApi.create(branchId, items);
      toast.success(message);
      onSaved();
      onClose();
    } catch (err) {
      setError(getErrorDetail(err));
    } finally {
      setSaving(false);
    }
  };

  return { saving, error, setError, submit };
}

// =============================================================================
// Xếp ca cho một nhân viên trong một ngày (bấm "+" trên ô lưới)
// =============================================================================

interface AddShiftProps {
  target: { employee: GridEmployee; day: string } | null;
  branchId: number | undefined;
  shifts: Shift[];
  existing: Schedule[];
  onClose: () => void;
  onSaved: () => void;
}

export function AddShiftModal({ target, branchId, shifts, existing, onClose, onSaved }: AddShiftProps) {
  const [selected, setSelected] = useState<string[]>([]);
  const { saving, error, setError, submit } = useCreateSchedules(branchId, onSaved, onClose);

  useEffect(() => {
    setSelected([]);
    setError(null);
  }, [target, setError]);

  if (!target) return null;
  const { employee, day } = target;

  return (
    <Modal
      open
      onClose={onClose}
      title={`Xếp ca cho ${employee.fullName}`}
      description={`${weekdayLabels[(new Date(`${day}T00:00:00Z`).getUTCDay() + 6) % 7]}, ${formatDate(day)} — chọn một hoặc nhiều ca (ca gãy: chọn hai ca nối tiếp).`}
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button
            label={selected.length > 1 ? `Xếp ${selected.length} ca` : 'Xếp ca'}
            variant="primary"
            isLoading={saving}
            onClick={() => submit(selected.map((id) => ({ userId: employee.id, shiftId: Number(id), workDate: day })))}
          />
        </>
      }
    >
      <VStack gap={4}>
        <ErrorList error={error} />
        {existing.length > 0 && (
          <VStack gap={1}>
            <Text type="label">Đã có trong ngày</Text>
            <HStack gap={1} wrap="wrap">
              {existing.map((s) => (
                <Token key={s.id} size="sm" color={scheduleColor(s)} label={`${s.shiftCode} · ${s.startTime}–${s.endTime}`} />
              ))}
            </HStack>
          </VStack>
        )}
        {shifts.length === 0 ? (
          <Notice status="warning" title="Chi nhánh chưa có ca nào để xếp" description="Vào trang Khung ca làm việc để tạo ca." />
        ) : (
          <ScrollableArea label="Danh sách ca" height={shifts.length > 5 ? 340 : undefined}>
            <CheckboxList label="Ca làm việc" value={selected} onChange={setSelected} hasDividers>
              {shifts.map((s) => (
                <CheckboxListItem
                  key={s.id}
                  value={String(s.id)}
                  label={`${s.name} (${s.shiftCode})`}
                  description={`${s.startTime} – ${s.endTime}${s.isCrossDay ? ' hôm sau' : ''} · làm ${formatMinutes(s.workingMinutes)}`}
                />
              ))}
            </CheckboxList>
          </ScrollableArea>
        )}
      </VStack>
    </Modal>
  );
}

// =============================================================================
// Xếp nhanh: nhiều nhân viên × một ca × nhiều ngày trong tuần
// =============================================================================

interface QuickProps {
  open: boolean;
  branchId: number | undefined;
  days: string[];
  today: string;
  employees: GridEmployee[];
  shifts: Shift[];
  onClose: () => void;
  onSaved: () => void;
}

export function QuickScheduleModal({ open, branchId, days, today, employees, shifts, onClose, onSaved }: QuickProps) {
  const [userIds, setUserIds] = useState<string[]>([]);
  const [shiftId, setShiftId] = useState<string>('');
  const [selectedDays, setSelectedDays] = useState<string[]>([]);
  const { saving, error, setError, submit } = useCreateSchedules(branchId, onSaved, onClose);

  const futureDays = useMemo(() => days.filter((d) => d >= today), [days, today]);

  useEffect(() => {
    if (!open) return;
    setUserIds([]);
    setShiftId(shifts[0] ? String(shifts[0].id) : '');
    // Mặc định: các ngày làm việc T2–T6 còn lại của tuần; cuối tuần thì chọn các ngày còn lại
    const weekdays = days.filter((d, i) => i < 5 && d >= today);
    setSelectedDays(weekdays.length > 0 ? weekdays : futureDays);
    setError(null);
  }, [open, shifts, days, today, futureDays, setError]);

  const count = userIds.length * selectedDays.length;

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="md"
      purpose="form"
      title="Xếp nhanh trong tuần"
      description="Xếp một ca cho nhiều nhân viên vào nhiều ngày cùng lúc. Lịch tạo ra là bản nháp."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button
            label={count > 0 ? `Xếp ${count} ca` : 'Xếp ca'}
            variant="primary"
            isLoading={saving}
            isDisabled={!shiftId}
            onClick={() =>
              submit(userIds.flatMap((u) => selectedDays.map((d) => ({ userId: Number(u), shiftId: Number(shiftId), workDate: d }))))
            }
          />
        </>
      }
    >
      <VStack gap={4}>
        <ErrorList error={error} />
        <MultiSelector
          label="Nhân viên"
          placeholder="Chọn nhân viên"
          options={employees.map((e) => ({ value: String(e.id), label: `${e.fullName} (${e.employeeCode})` }))}
          value={userIds}
          onChange={setUserIds}
          triggerDisplay="count"
        />
        <Selector
          label="Ca làm việc"
          options={shifts.map((s) => ({ value: String(s.id), label: `${s.name} — ${shiftLabel(s)}` }))}
          value={shiftId}
          onChange={(v) => setShiftId(v ?? '')}
          placeholder="Chọn ca"
        />
        <CheckboxList label="Các ngày" value={selectedDays} onChange={setSelectedDays} density="compact">
          {days.map((d, i) => (
            <CheckboxListItem
              key={d}
              value={d}
              label={`${weekdayLabels[i]}, ${formatDayMonth(d)}`}
              isDisabled={d < today}
              description={d < today ? 'Ngày đã qua' : undefined}
            />
          ))}
        </CheckboxList>
      </VStack>
    </Modal>
  );
}

// =============================================================================
// Chi tiết một ca đã xếp: xóa nháp / hủy ca đã công bố
// =============================================================================

interface DetailProps {
  schedule: Schedule | null;
  today: string;
  onClose: () => void;
  onChanged: () => void;
}

export function ScheduleDetailModal({ schedule, today, onClose, onChanged }: DetailProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ErrorDetail | null>(null);

  useEffect(() => setError(null), [schedule]);
  if (!schedule) return null;

  const canDelete = !schedule.isPublished;
  const canCancel = schedule.isPublished && schedule.status === 'scheduled' && schedule.workDate >= today;

  const remove = async () => {
    setBusy(true);
    setError(null);
    try {
      const { message } = await scheduleApi.remove(schedule.id);
      toast.success(message);
      onChanged();
      onClose();
    } catch (err) {
      setError(getErrorDetail(err));
    } finally {
      setBusy(false);
    }
  };

  const rows: [string, string][] = [
    ['Nhân viên', `${schedule.fullName} (${schedule.employeeCode})`],
    ['Ngày làm', formatDate(schedule.workDate)],
    ['Ca', `${schedule.shiftName} (${schedule.shiftCode})`],
    ['Giờ', `${schedule.startTime} – ${schedule.endTime}${schedule.isCrossDay ? ' sáng hôm sau' : ''}`],
    ['Trạng thái', scheduleStateLabel(schedule)],
  ];

  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Chi tiết ca làm việc"
      footer={
        <>
          <Button label="Đóng" variant="secondary" onClick={onClose} />
          {canDelete && <Button label="Xóa ca nháp" variant="destructive" isLoading={busy} onClick={remove} />}
          {canCancel && <Button label="Hủy ca" variant="destructive" isLoading={busy} onClick={remove} />}
        </>
      }
    >
      <VStack gap={3}>
        <ErrorList error={error} />
        <MetadataList label={{ width: 96 }}>
          {rows.map(([label, value]) => (
            <MetadataListItem key={label} label={label}>
              {value}
            </MetadataListItem>
          ))}
        </MetadataList>
        {canCancel && (
          <Text type="supporting" as="p">
            Ca đã công bố: hủy ca sẽ giữ lại bản ghi (hiện "đã hủy") để nhân viên biết và để đối soát công.
          </Text>
        )}
      </VStack>
    </Modal>
  );
}
