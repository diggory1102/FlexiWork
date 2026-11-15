import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Switch } from '@astryxdesign/core/Switch';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { TimeInput, type ISOTimeString } from '@astryxdesign/core/TimeInput';
import { Clock } from 'lucide-react';
import { Modal } from '@/components/ui/Modal';
import { ErrorList, Notice, type ErrorDetail } from '@/components/ui/Feedback';
import { shiftApi } from '@/services/schedulingApi';
import { getErrorDetail } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { formatMinutes, timeToMinutes } from '@/lib/dates';
import { RoleGroups } from '@/types/auth';
import type { Shift, ShiftRequest } from '@/types/scheduling';

const SHARED = 'shared';
const MAX_DURATION = 16 * 60;

const emptyForm = (branchId: number | null): ShiftRequest => ({
  shiftCode: '',
  name: '',
  branchId,
  startTime: '08:00',
  endTime: '17:00',
  breakMinutes: 60,
  workUnits: 1,
  isNightShift: false,
  lateGraceMinutes: 5,
  earlyLeaveGraceMinutes: 5,
});

interface Props {
  open: boolean;
  shift: Shift | null; // null = tạo mới
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Thêm / sửa khung ca. Admin/HR chọn ca dùng chung hoặc ca riêng của một chi nhánh; Quản lý chỉ tạo ca cho chi nhánh mình.
 * Ca đã được xếp lịch: khóa giờ giấc, giờ nghỉ, hệ số công, ca đêm, chi nhánh (backend cũng chặn).
 */
export function ShiftFormModal({ open, shift, onClose, onSaved }: Props) {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const ownBranchId = useAuthStore((s) => s.user?.branchId ?? null);
  const branches = useLookupStore((s) => s.branches);

  const [form, setForm] = useState<ShiftRequest>(emptyForm(null));
  const [isInUse, setIsInUse] = useState(false);
  const [error, setError] = useState<ErrorDetail | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setIsInUse(false);
    if (!shift) {
      setForm(emptyForm(isCompanyWide ? null : ownBranchId));
      return;
    }
    const { shiftCode, name, branchId, startTime, endTime, breakMinutes, workUnits, isNightShift, lateGraceMinutes, earlyLeaveGraceMinutes } = shift;
    setForm({ shiftCode, name, branchId, startTime, endTime, breakMinutes, workUnits, isNightShift, lateGraceMinutes, earlyLeaveGraceMinutes });
    shiftApi
      .get(shift.id)
      .then(({ data }) => setIsInUse(data.isInUse))
      .catch(() => setIsInUse(false));
  }, [open, shift, isCompanyWide, ownBranchId]);

  const set = <K extends keyof ShiftRequest>(key: K, value: ShiftRequest[K]) => setForm((f) => ({ ...f, [key]: value }));

  // Xem trước: ca qua đêm, độ dài, số phút làm thực tế
  const preview = useMemo(() => {
    const start = timeToMinutes(form.startTime);
    const end = timeToMinutes(form.endTime);
    if (start == null || end == null) return null;
    if (start === end) return { error: 'Giờ kết thúc phải khác giờ bắt đầu.' };
    const crossDay = end < start;
    const duration = crossDay ? end + 1440 - start : end - start;
    const working = duration - form.breakMinutes;
    if (duration > MAX_DURATION) return { error: 'Ca dài tối đa 16 giờ (kể cả giờ nghỉ).' };
    if (working < 30) return { error: 'Giờ nghỉ quá dài: thời gian làm thực tế phải từ 30 phút trở lên.' };
    return { crossDay, duration, working };
  }, [form.startTime, form.endTime, form.breakMinutes]);

  const branchOptions = [
    { value: SHARED, label: 'Dùng chung mọi chi nhánh' },
    ...branches.map((b) => ({ value: String(b.id), label: `${b.branchCode} – ${b.name}` })),
  ];
  const ownBranchName = branches.find((b) => b.id === form.branchId)?.name;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!form.shiftCode.trim() || !form.name.trim()) return setError({ message: 'Vui lòng nhập mã ca và tên ca.', errors: [] });
    if (!preview) return setError({ message: 'Vui lòng nhập giờ bắt đầu và giờ kết thúc (HH:mm).', errors: [] });
    if ('error' in preview) return setError({ message: preview.error!, errors: [] });

    setSaving(true);
    setError(null);
    try {
      const { message } = shift ? await shiftApi.update(shift.id, form) : await shiftApi.create(form);
      toast.success(message);
      onSaved();
      onClose();
    } catch (err) {
      setError(getErrorDetail(err));
    } finally {
      setSaving(false);
    }
  };

  const lockedMessage = 'Ca đã được xếp lịch — tạo ca mới nếu cần khung giờ khác.';

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      purpose="form"
      title={shift ? `Sửa ca ${shift.shiftCode}` : 'Thêm ca làm việc'}
      description="Giờ kết thúc nhỏ hơn giờ bắt đầu nghĩa là ca kết thúc vào sáng hôm sau (ca đêm). Ca gãy: tạo hai ca nối tiếp nhau."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" type="submit" form="shift-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="shift-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <ErrorList error={error} />
          {isInUse && (
            <Notice
              status="warning"
              title="Ca này đã được xếp lịch"
              description="Chỉ sửa được mã, tên và số phút cho phép đi muộn / về sớm. Giờ làm, giờ nghỉ, hệ số công, ca đêm và phạm vi được giữ nguyên để không làm sai lịch sử chấm công."
            />
          )}

          <Grid columns={{ minWidth: 240, max: 2 }} gap={4}>
            <TextInput
              label="Mã ca"
              isRequired
              labelTooltip="2–20 ký tự chữ, số, '-' hoặc '_'; tự chuyển thành chữ hoa."
              placeholder="VD: CA-SANG"
              value={form.shiftCode}
              onChange={(v) => set('shiftCode', v)}
              hasAutoFocus
            />
            <TextInput label="Tên ca" isRequired placeholder="VD: Ca sáng" value={form.name} onChange={(v) => set('name', v)} />
            <GridSpan columns="full">
              {isCompanyWide ? (
                <Selector
                  label="Phạm vi áp dụng"
                  options={branchOptions}
                  value={form.branchId == null ? SHARED : String(form.branchId)}
                  onChange={(v) => set('branchId', v === SHARED || !v ? null : Number(v))}
                  isDisabled={isInUse}
                  disabledMessage={lockedMessage}
                  labelTooltip="Ca dùng chung: mọi chi nhánh đều xếp được. Ca riêng: chỉ chi nhánh đó xếp được và Quản lý chi nhánh tự sửa được."
                />
              ) : (
                <TextInput label="Phạm vi áp dụng" value={`Ca riêng của chi nhánh ${ownBranchName ?? 'của bạn'}`} onChange={() => {}} isDisabled />
              )}
            </GridSpan>
          </Grid>

          <Card variant="muted" padding={4}>
            <VStack gap={3}>
              <Grid columns={{ minWidth: 160, max: 3 }} gap={4}>
                <TimeInput
                  label="Giờ bắt đầu"
                  isRequired
                  hourFormat="24h"
                  value={form.startTime as ISOTimeString}
                  onChange={(v) => set('startTime', v ?? '')}
                  isDisabled={isInUse}
                  disabledMessage={lockedMessage}
                />
                <TimeInput
                  label="Giờ kết thúc"
                  isRequired
                  hourFormat="24h"
                  value={form.endTime as ISOTimeString}
                  onChange={(v) => set('endTime', v ?? '')}
                  isDisabled={isInUse}
                  disabledMessage={lockedMessage}
                />
                <NumberInput
                  label="Nghỉ giữa ca"
                  units="phút"
                  min={0}
                  max={480}
                  step={15}
                  isIntegerOnly
                  value={form.breakMinutes}
                  onChange={(v) => set('breakMinutes', v ?? 0)}
                  isDisabled={isInUse}
                  disabledMessage={lockedMessage}
                />
              </Grid>
              {preview && (
                <HStack gap={2} vAlign="center">
                  <Icon icon={Clock} size="sm" color={'error' in preview ? 'error' : 'secondary'} />
                  <Text type="supporting" color={'error' in preview ? undefined : 'secondary'}>
                    {'error' in preview
                      ? preview.error
                      : `${preview.crossDay ? `Ca qua đêm: ${form.startTime} hôm nay → ${form.endTime} sáng hôm sau. ` : ''}Làm việc thực tế ${formatMinutes(preview.working!)} (ca dài ${formatMinutes(preview.duration!)}${form.breakMinutes > 0 ? `, nghỉ ${form.breakMinutes} phút` : ''}).`}
                  </Text>
                </HStack>
              )}
            </VStack>
          </Card>

          <Grid columns={{ minWidth: 160, max: 3 }} gap={4}>
            <NumberInput
              label="Hệ số công"
              labelTooltip="Số công quy đổi của ca: 1 = một công, 0,5 = nửa công."
              min={0.1}
              max={3}
              step={0.25}
              value={form.workUnits}
              onChange={(v) => set('workUnits', v ?? 1)}
              isDisabled={isInUse}
              disabledMessage={lockedMessage}
            />
            <NumberInput
              label="Cho phép đi muộn"
              units="phút"
              labelTooltip="Check-in trễ trong số phút này chưa tính là đi muộn. Vượt quá thì tính toàn bộ số phút muộn theo quy tắc chấm công."
              min={0}
              max={60}
              isIntegerOnly
              value={form.lateGraceMinutes}
              onChange={(v) => set('lateGraceMinutes', v ?? 0)}
            />
            <NumberInput
              label="Cho phép về sớm"
              units="phút"
              labelTooltip="Check-out sớm trong số phút này chưa tính là về sớm."
              min={0}
              max={60}
              isIntegerOnly
              value={form.earlyLeaveGraceMinutes}
              onChange={(v) => set('earlyLeaveGraceMinutes', v ?? 0)}
            />
          </Grid>

          <Switch
            label="Ca đêm"
            description="Được tính phụ cấp ca đêm khi tính lương."
            value={form.isNightShift}
            onChange={(v) => set('isNightShift', v)}
            isDisabled={isInUse}
            disabledMessage={lockedMessage}
          />
        </VStack>
      </form>
    </Modal>
  );
}
