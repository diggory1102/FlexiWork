import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { DateInput } from '@astryxdesign/core/DateInput';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { TextArea } from '@astryxdesign/core/TextArea';
import { TimeInput } from '@astryxdesign/core/TimeInput';
import { FormError } from '@/components/ui/Feedback';
import { Modal } from '@/components/ui/Modal';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { attendanceApi } from '@/services/attendanceApi';
import { formatDate, type ISODateString } from '@/lib/format';
import type { AttendanceLogDetail } from '@/types/attendance';

type TimeValue = React.ComponentProps<typeof TimeInput>['value'];

/**
 * Quản lý / HR nhập giờ ra thay nhân viên quên check-out. Hệ thống tính lại giờ làm và phút về sớm như check-out thường,
 * lưu người điều chỉnh + lý do và báo cho nhân viên. Giờ ra phải sau giờ vào, không ở tương lai, không muộn quá 12 giờ sau khi hết ca.
 */
export function AdjustCheckoutModal({
  open,
  logId,
  employeeName,
  workDate,
  shiftLabel,
  checkInTime,
  shiftEnd,
  onClose,
  onAdjusted,
}: {
  open: boolean;
  logId: number;
  employeeName: string;
  /** Ngày công, dạng ISO (yyyy-MM-dd…). */
  workDate: string;
  /** VD: "Ca sáng 08:00–12:00". */
  shiftLabel: string;
  /** Giờ vào, dạng ISO (yyyy-MM-ddTHH:mm:ss). */
  checkInTime: string;
  /** Giờ kết thúc ca "HH:mm" — làm giá trị gợi ý ban đầu. */
  shiftEnd: string | null;
  onClose: () => void;
  onAdjusted: (detail: AttendanceLogDetail) => void;
}) {
  const [date, setDate] = useState<ISODateString>(workDate.slice(0, 10) as ISODateString);
  const [time, setTime] = useState<string>(shiftEnd ?? '17:00');
  const [note, setNote] = useState('');
  const { saving, error, setError, run } = useFormSubmit();

  useEffect(() => {
    if (!open) return;
    setDate(workDate.slice(0, 10) as ISODateString);
    setTime(shiftEnd ?? '17:00');
    setNote('');
    setError(null);
  }, [open, workDate, shiftEnd, setError]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!time) return setError('Vui lòng nhập giờ ra.');
    if (!note.trim()) return setError('Vui lòng ghi lý do điều chỉnh.');
    let result: AttendanceLogDetail | null = null;
    void run(
      async () => {
        const { data, message } = await attendanceApi.adjustCheckout(logId, `${date}T${time.slice(0, 5)}:00`, note.trim());
        result = data;
        return message;
      },
      () => {
        if (result) onAdjusted(result);
        onClose();
      },
    );
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="sm"
      purpose="form"
      title="Điều chỉnh giờ ra"
      description={`${employeeName} · ${shiftLabel} · ngày ${formatDate(workDate)} · vào lúc ${checkInTime.slice(11, 16)}`}
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Ghi giờ ra" type="submit" form="adjust-checkout-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      {open && (
        <form id="adjust-checkout-form" onSubmit={submit} noValidate>
          <VStack gap={4}>
            <FormError message={error} />
            <HStack gap={3} vAlign="end" wrap="wrap">
              <DateInput label="Ngày ra" isRequired value={date} onChange={(v) => v && setDate(v)} format="date" />
              <TimeInput label="Giờ ra" isRequired value={time as TimeValue} onChange={(v) => setTime(v ?? '')} />
            </HStack>
            <TextArea
              label="Lý do điều chỉnh"
              isRequired
              rows={3}
              maxLength={255}
              placeholder="VD: Nhân viên quên check-out, ca trưởng xác nhận ra lúc 16:30."
              value={note}
              onChange={(v) => {
                setNote(v);
                setError(null);
              }}
            />
          </VStack>
        </form>
      )}
    </Modal>
  );
}
