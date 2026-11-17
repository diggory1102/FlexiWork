import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Divider } from '@astryxdesign/core/Divider';
import { Grid } from '@astryxdesign/core/Grid';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Modal } from '@/components/ui/Modal';
import { Avatar } from '@/components/ui/Layout';
import { Badge, FormError, Spinner } from '@/components/ui/Feedback';
import { leaveBalanceApi } from '@/services/requestApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatDateTime } from '@/lib/format';
import type { LeaveBalanceAdjustment, LeaveBalanceRow } from '@/types/requests';
import { days } from '../requestUi';

function Figure({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <VStack gap={0.5}>
      <Text type="supporting" size="sm">
        {label}
      </Text>
      <Text size="lg" weight="semibold" hasTabularNumbers>
        {value}
      </Text>
      {hint && (
        <Text type="supporting" size="sm">
          {hint}
        </Text>
      )}
    </VStack>
  );
}

interface Props {
  /** Quỹ phép đang xem (giữ lại khi đóng để hộp thoại không trống lúc đóng). */
  row: LeaveBalanceRow | null;
  open: boolean;
  /** Admin/HR được cộng / trừ; Quản lý chỉ xem. */
  canAdjust: boolean;
  onClose: () => void;
  /** Gọi sau khi điều chỉnh để danh sách tải lại. */
  onChanged: () => void;
}

/**
 * Quỹ phép năm của một nhân viên (UC 5.5): số ngày tổng / đã dùng / chờ duyệt / còn xin được, lịch sử điều chỉnh,
 * và (Admin/HR) cộng / trừ kèm lý do — bước 0,5 ngày, tối đa ±30 ngày mỗi lần, tổng mới không nhỏ hơn số ngày đã dùng.
 */
export function LeaveBalanceModal({ row, open, canAdjust, onClose, onChanged }: Props) {
  const [current, setCurrent] = useState<LeaveBalanceRow | null>(row);
  const [history, setHistory] = useState<LeaveBalanceAdjustment[] | null>(null);
  const [delta, setDelta] = useState<number | undefined>(undefined);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const userId = row?.userId;
  const year = row?.year;
  const loadHistory = useCallback(async () => {
    if (userId == null || year == null) return;
    try {
      setHistory((await leaveBalanceApi.history(userId, year)).data);
    } catch (err) {
      setHistory([]);
      setError(getErrorMessage(err));
    }
  }, [userId, year]);

  useEffect(() => {
    if (!open) return;
    setCurrent(row);
    setHistory(null);
    setDelta(undefined);
    setReason('');
    setError(null);
    void loadHistory();
    // Chỉ đặt lại khi mở quỹ phép khác
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, userId, year]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!current) return;
    if (!delta) return setError('Vui lòng nhập số ngày cần cộng (+) hoặc trừ (−).');
    if (!reason.trim()) return setError('Vui lòng nhập lý do điều chỉnh.');
    setSaving(true);
    setError(null);
    try {
      const { data, message } = await leaveBalanceApi.adjust({ userId: current.userId, year: current.year, deltaDays: delta, reason: reason.trim() });
      toast.success(message);
      setCurrent(data);
      setDelta(undefined);
      setReason('');
      onChanged();
      await loadHistory();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const r = current;
  return (
    <Modal
      open={open}
      onClose={onClose}
      size="md"
      purpose="form"
      title={r ? `Quỹ phép năm ${r.year}` : 'Quỹ phép năm'}
      description={r ? `${r.fullName} (${r.employeeCode})` : undefined}
      footer={<Button label="Đóng" variant="secondary" onClick={onClose} />}
    >
      {r && (
        <VStack gap={4}>
          <HStack gap={3} vAlign="center">
            <Avatar name={r.fullName} url={r.avatarUrl} size={48} />
            <VStack gap={0.5}>
              <Heading level={4}>{r.fullName}</Heading>
              <Text type="supporting">{r.branchName ?? '—'}</Text>
            </VStack>
            {r.isDefault && <Badge tone="gray">Chưa khởi tạo · đang dùng số ngày mặc định</Badge>}
          </HStack>

          <Card variant="muted" padding={4}>
            <Grid columns={{ minWidth: 110, max: 4 }} gap={4}>
              <Figure label="Tổng quỹ" value={`${days(r.totalDays)} ngày`} />
              <Figure label="Đã dùng" value={`${days(r.usedDays)} ngày`} />
              <Figure label="Đang chờ duyệt" value={`${days(r.pendingDays)} ngày`} />
              <Figure label="Còn xin được" value={`${days(r.availableDays)} ngày`} hint={`Còn lại ${days(r.remainingDays)}`} />
            </Grid>
          </Card>

          {canAdjust && (
            <form onSubmit={submit} noValidate>
              <VStack gap={3}>
                <Heading level={4}>Điều chỉnh quỹ phép</Heading>
                <FormError message={error} />
                <Grid columns={{ minWidth: 200, max: 2 }} gap={3}>
                  <NumberInput
                    label="Số ngày cộng (+) / trừ (−)"
                    labelTooltip="Bước 0,5 ngày, tối đa ±30 ngày mỗi lần. Tổng quỹ sau điều chỉnh không được nhỏ hơn số ngày đã dùng."
                    min={-30}
                    max={30}
                    step={0.5}
                    value={delta}
                    onChange={(v) => setDelta(v ?? undefined)}
                  />
                  <TextInput label="Lý do" isRequired placeholder="VD: Thưởng thâm niên 5 năm" value={reason} onChange={setReason} />
                </Grid>
                <HStack hAlign="end">
                  <Button label="Áp dụng điều chỉnh" type="submit" variant="primary" isLoading={saving} />
                </HStack>
              </VStack>
            </form>
          )}
          {!canAdjust && <FormError message={error} />}

          <Divider />
          <VStack gap={2}>
            <Heading level={4}>Lịch sử điều chỉnh</Heading>
            {history === null ? (
              <Spinner />
            ) : history.length === 0 ? (
              <Text type="supporting">Chưa có lần điều chỉnh nào trong năm {r.year}.</Text>
            ) : (
              history.map((a) => (
                <HStack key={a.id} gap={3} vAlign="start" hAlign="between">
                  <VStack gap={0.5}>
                    <Text size="sm">{a.reason}</Text>
                    <Text type="supporting" size="sm">
                      {a.createdByName ?? '—'} · {formatDateTime(a.createdAt)}
                    </Text>
                  </VStack>
                  <Badge tone={a.deltaDays > 0 ? 'green' : 'red'}>
                    {a.deltaDays > 0 ? '+' : ''}
                    {days(a.deltaDays)} ngày
                  </Badge>
                </HStack>
              ))
            )}
          </VStack>
        </VStack>
      )}
    </Modal>
  );
}
