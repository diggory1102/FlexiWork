import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { DateInput } from '@astryxdesign/core/DateInput';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Trash2 } from 'lucide-react';
import { Modal } from '@/components/ui/Modal';
import { ErrorList, Notice, type ErrorDetail } from '@/components/ui/Feedback';
import { staffingApi } from '@/services/schedulingApi';
import { getErrorDetail } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatDate, todayIso, type ISODateString } from '@/lib/format';
import { weekdayLabels } from '@/lib/dates';
import type { Shift, StaffingRule, StaffingScope } from '@/types/scheduling';
import { staffingLabel } from '../scheduleUi';

interface Props {
  open: boolean;
  branchId: number | undefined;
  /** Các ca của chi nhánh. */
  shifts: Shift[];
  /** Điền sẵn khi mở từ một ô của lưới (ca + ngày). */
  initial: { shiftId?: number; date?: string } | null;
  onClose: () => void;
  /** Gọi sau khi đặt / bỏ ngoại lệ để lưới nạp lại định biên. */
  onChanged: () => void;
}

const scopeOptions = [
  { value: 'date', label: 'Một ngày cụ thể' },
  { value: 'weekday', label: 'Mọi tuần, theo thứ' },
];
const weekdayOptions = weekdayLabels.map((label, i) => ({ value: String(i + 1), label }));

const ruleLabel = (r: StaffingRule) => {
  const when = r.scope === 'date' && r.date ? formatDate(r.date) : `Mọi ${weekdayLabels[(r.dayOfWeek ?? 1) - 1]} hằng tuần`;
  const limits = staffingLabel({ minStaff: r.minStaff, maxStaff: r.maxStaff });
  return `${r.shiftCode} · ${when} · ${limits}`;
};

/**
 * Ngoại lệ định biên (Task 6.6, bước 2): đổi số người tối thiểu / tối đa của một ca cho đúng một ngày (VD ngày có tiệc đặt bàn)
 * hoặc cho mọi thứ trong tuần. Không đụng giờ giấc của ca và không xóa người đã xếp; bỏ ngoại lệ thì ô quay về mặc định của ca.
 */
export function StaffingDialog({ open, branchId, shifts, initial, onClose, onChanged }: Props) {
  const [rules, setRules] = useState<StaffingRule[]>([]);
  const [shiftId, setShiftId] = useState<string>('');
  const [scope, setScope] = useState<StaffingScope>('date');
  const [date, setDate] = useState<ISODateString>(todayIso() as ISODateString);
  const [dayOfWeek, setDayOfWeek] = useState<string>('6');
  const [minStaff, setMinStaff] = useState<number | null>(null);
  const [maxStaff, setMaxStaff] = useState<number | null>(null);
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ErrorDetail | null>(null);

  const shift = shifts.find((s) => String(s.id) === shiftId);

  const loadRules = useCallback(async () => {
    if (!branchId) return;
    try {
      setRules((await staffingApi.rules(branchId)).data);
    } catch {
      setRules([]);
    }
  }, [branchId]);

  // Mở hộp thoại: điền sẵn ca / ngày từ ô đã bấm, nạp danh sách ngoại lệ hiện có
  useEffect(() => {
    if (!open) return;
    setError(null);
    setScope('date');
    setDate((initial?.date ?? todayIso()) as ISODateString);
    setShiftId(initial?.shiftId ? String(initial.shiftId) : shifts[0] ? String(shifts[0].id) : '');
    void loadRules();
  }, [open, initial, shifts, loadRules]);

  const existing = useMemo(
    () =>
      rules.find(
        (r) =>
          String(r.shiftId) === shiftId &&
          r.scope === scope &&
          (scope === 'date' ? r.date === date : String(r.dayOfWeek) === dayOfWeek),
      ),
    [rules, shiftId, scope, date, dayOfWeek],
  );

  // Đổi ca / phạm vi / ngày: nếu ô đó đã có ngoại lệ thì hiện giá trị đang đặt, chưa có thì lấy mặc định của ca để chỉ cần sửa số
  useEffect(() => {
    if (existing) {
      setMinStaff(existing.minStaff);
      setMaxStaff(existing.maxStaff);
      setNote(existing.note ?? '');
    } else {
      setMinStaff(shift?.minStaff ?? null);
      setMaxStaff(shift?.maxStaff ?? null);
      setNote('');
    }
    setError(null);
  }, [existing, shift]);

  const save = async () => {
    if (!shiftId) return setError({ message: 'Vui lòng chọn ca.', errors: [] });
    if (minStaff != null && maxStaff != null && minStaff > maxStaff) {
      return setError({ message: 'Số người tối thiểu không được lớn hơn số người tối đa.', errors: [] });
    }
    setSaving(true);
    setError(null);
    try {
      const { message } = await staffingApi.save({
        branchId,
        shiftId: Number(shiftId),
        scope,
        date: scope === 'date' ? date : undefined,
        dayOfWeek: scope === 'weekday' ? Number(dayOfWeek) : undefined,
        minStaff,
        maxStaff,
        note: note.trim() || null,
      });
      toast.success(message);
      onChanged();
      await loadRules();
    } catch (err) {
      setError(getErrorDetail(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (rule: StaffingRule) => {
    try {
      const { message } = await staffingApi.remove(rule.id);
      toast.success(message);
      onChanged();
      await loadRules();
    } catch (err) {
      setError(getErrorDetail(err));
    }
  };

  const noLimit = minStaff == null && maxStaff == null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      purpose="form"
      title="Định biên ca theo ngày"
      description="Đổi số người tối thiểu / tối đa của một ca cho đúng một ngày (VD ngày có tiệc đặt bàn) hoặc cho mọi thứ trong tuần. Người đã được xếp giữ nguyên; các ngày khác theo mặc định của ca."
      footer={
        <>
          <Button label="Đóng" variant="secondary" onClick={onClose} />
          <Button label={existing ? 'Cập nhật ngoại lệ' : 'Lưu ngoại lệ'} variant="primary" isLoading={saving} onClick={() => void save()} />
        </>
      }
    >
      {open && (
        <VStack gap={4}>
          <ErrorList error={error} />
          {shifts.length === 0 ? (
            <Notice status="warning" title="Chi nhánh chưa có ca nào" description="Vào trang Khung ca làm việc để tạo ca trước." />
          ) : (
            <>
              <Grid columns={{ minWidth: 220, max: 2 }} gap={4}>
                <Selector
                  label="Ca"
                  options={shifts.map((s) => ({ value: String(s.id), label: `${s.name} (${s.shiftCode}) · ${s.startTime}–${s.endTime}` }))}
                  value={shiftId}
                  onChange={(v) => v && setShiftId(v)}
                />
                <Selector label="Áp dụng cho" options={scopeOptions} value={scope} onChange={(v) => v && setScope(v as StaffingScope)} />
                {scope === 'date' ? (
                  <DateInput label="Ngày" isRequired value={date} onChange={(v) => v && setDate(v)} format="date" />
                ) : (
                  <Selector label="Thứ trong tuần" options={weekdayOptions} value={dayOfWeek} onChange={(v) => v && setDayOfWeek(v)} />
                )}
                <TextInput
                  label="Ghi chú"
                  placeholder="VD: Tiệc đặt bàn công ty ABC"
                  value={note}
                  onChange={(v) => setNote(v.slice(0, 255))}
                />
              </Grid>

              <Grid columns={{ minWidth: 160, max: 2 }} gap={4}>
                <NumberInput
                  label="Số người tối thiểu"
                  units="người"
                  labelTooltip="Chưa đủ số này thì lưới xếp lịch chỉ cảnh báo. Bỏ trống = không yêu cầu."
                  min={1}
                  max={500}
                  isIntegerOnly
                  hasClear
                  placeholder="Không yêu cầu"
                  value={minStaff}
                  onChange={(v) => setMinStaff(v ?? null)}
                />
                <NumberInput
                  label="Số người tối đa"
                  units="người"
                  labelTooltip="Đủ số này thì không xếp thêm được cho ngày đó. Bỏ trống = không giới hạn."
                  min={1}
                  max={500}
                  isIntegerOnly
                  hasClear
                  placeholder="Không giới hạn"
                  value={maxStaff}
                  onChange={(v) => setMaxStaff(v ?? null)}
                />
              </Grid>
              <Text type="supporting">
                {shift ? `Mặc định của ca ${shift.shiftCode}: ${staffingLabel(shift)} mỗi ngày. ` : ''}
                {noLimit ? 'Để trống cả hai ô nghĩa là ngày này không giới hạn số người.' : ''}
              </Text>
            </>
          )}

          <VStack gap={2}>
            <Text type="label">Ngoại lệ đang đặt ({rules.length})</Text>
            {rules.length === 0 ? (
              <Text type="supporting">Chưa có ngoại lệ nào — mọi ca đang theo định biên mặc định.</Text>
            ) : (
              rules.map((r) => (
                <HStack key={r.id} gap={2} vAlign="center" hAlign="between">
                  <VStack gap={0}>
                    <Text>{ruleLabel(r)}</Text>
                    {r.note && <Text type="supporting">{r.note}</Text>}
                  </VStack>
                  <IconButton
                    label={`Bỏ ngoại lệ ${ruleLabel(r)}`}
                    tooltip="Bỏ ngoại lệ"
                    variant="ghost"
                    icon={<Icon icon={Trash2} size="sm" />}
                    onClick={() => void remove(r)}
                  />
                </HStack>
              ))
            )}
          </VStack>
        </VStack>
      )}
    </Modal>
  );
}
