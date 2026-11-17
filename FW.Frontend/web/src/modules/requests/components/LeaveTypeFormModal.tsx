import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Selector } from '@astryxdesign/core/Selector';
import { VStack } from '@astryxdesign/core/Stack';
import { Switch } from '@astryxdesign/core/Switch';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Modal } from '@/components/ui/Modal';
import { FormError } from '@/components/ui/Feedback';
import { leaveTypeApi } from '@/services/requestApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import type { LeaveType, LeaveTypeRequest, RequestCategory } from '@/types/requests';
import { categoryLabels } from '../requestUi';

const categoryHints: Record<RequestCategory, string> = {
  leave: 'Nghỉ theo ngày (phép năm, ốm, việc riêng…). Có thể trừ vào quỹ phép năm.',
  shift_swap: 'Đổi ca với đồng nghiệp hoặc nhờ đồng nghiệp làm thay — đồng nghiệp đồng ý trước khi Quản lý duyệt.',
  overtime: 'Đăng ký làm thêm giờ ngoài ca đã xếp (có giờ bắt đầu / kết thúc).',
  explanation: 'Giải trình một lượt chấm công đi muộn, về sớm hoặc đang chờ duyệt.',
};

const categoryOptions = (Object.keys(categoryLabels) as RequestCategory[]).map((c) => ({ value: c, label: categoryLabels[c] }));

const empty: LeaveTypeRequest = {
  name: '',
  category: 'leave',
  description: '',
  isPaid: true,
  deductsBalance: false,
  requiresAttachment: false,
  isActive: true,
};

interface Props {
  open: boolean;
  type: LeaveType | null; // null = tạo mới
  onClose: () => void;
  onSaved: () => void;
}

/** Thêm / sửa loại đơn (UC 1.4). Tên loại tùy HR đặt; cách xử lý đơn do nhóm quyết định. */
export function LeaveTypeFormModal({ open, type, onClose, onSaved }: Props) {
  const [form, setForm] = useState<LeaveTypeRequest>(empty);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setForm(type ? { ...type, description: type.description ?? '' } : empty);
  }, [open, type]);

  const set = <K extends keyof LeaveTypeRequest>(key: K, value: LeaveTypeRequest[K]) => setForm((f) => ({ ...f, [key]: value }));

  const changeCategory = (category: RequestCategory) =>
    setForm((f) => ({ ...f, category, deductsBalance: category === 'leave' ? f.deductsBalance : false }));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) return setError('Vui lòng nhập tên loại đơn.');
    setSaving(true);
    setError(null);
    try {
      const body = { ...form, name: form.name.trim(), description: form.description?.trim() || null };
      const { message } = type ? await leaveTypeApi.update(type.id, body) : await leaveTypeApi.create(body);
      toast.success(message);
      onSaved();
      onClose();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="md"
      purpose="form"
      title={type ? 'Sửa loại đơn' : 'Thêm loại đơn'}
      description="Nhóm đơn quyết định nhân viên phải điền gì và hệ thống xử lý ra sao khi duyệt."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" type="submit" form="leave-type-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="leave-type-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <TextInput label="Tên loại đơn" isRequired placeholder="VD: Nghỉ cưới" value={form.name} onChange={(v) => set('name', v)} hasAutoFocus />
          <Selector
            label="Nhóm đơn"
            isRequired
            description={categoryHints[form.category]}
            options={categoryOptions}
            value={form.category}
            onChange={(v) => v && changeCategory(v as RequestCategory)}
          />
          <TextInput
            label="Mô tả"
            labelTooltip="Hiện dưới tên loại đơn trên ứng dụng của nhân viên."
            value={form.description ?? ''}
            onChange={(v) => set('description', v)}
          />

          {form.category === 'leave' && (
            <>
              <Switch
                label="Nghỉ có hưởng lương"
                description="Dùng khi tổng hợp bảng công: ngày nghỉ có lương không bị trừ công."
                value={form.isPaid}
                onChange={(v) => setForm((f) => ({ ...f, isPaid: v, deductsBalance: v ? f.deductsBalance : false }))}
              />
              <Switch
                label="Trừ vào quỹ phép năm"
                description="Khi duyệt, số ngày nghỉ được trừ vào quỹ phép năm của nhân viên; không đủ ngày thì không xin / duyệt được."
                value={form.deductsBalance}
                onChange={(v) => set('deductsBalance', v)}
                isDisabled={!form.isPaid}
                disabledMessage="Chỉ loại nghỉ có hưởng lương mới trừ phép năm."
              />
            </>
          )}
          <Switch
            label="Bắt buộc ảnh minh chứng"
            description="Nhân viên phải chụp / chọn ảnh (VD giấy khám bệnh) mới gửi được đơn."
            value={form.requiresAttachment}
            onChange={(v) => set('requiresAttachment', v)}
          />
          {type && (
            <Switch
              label="Đang nhận đơn mới"
              description="Tắt để ngừng nhận đơn loại này (các đơn cũ vẫn giữ). Nên dùng thay cho xóa khi loại đơn đã có đơn."
              value={form.isActive}
              onChange={(v) => set('isActive', v)}
            />
          )}
        </VStack>
      </form>
    </Modal>
  );
}
