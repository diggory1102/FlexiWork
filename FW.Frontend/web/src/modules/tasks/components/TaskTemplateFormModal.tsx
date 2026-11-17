import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { Selector } from '@astryxdesign/core/Selector';
import { VStack } from '@astryxdesign/core/Stack';
import { Switch } from '@astryxdesign/core/Switch';
import { TextArea } from '@astryxdesign/core/TextArea';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Modal } from '@/components/ui/Modal';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { FormError } from '@/components/ui/Feedback';
import { shiftApi } from '@/services/schedulingApi';
import { taskApi } from '@/services/taskApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { Roles } from '@/types/auth';
import type { Shift } from '@/types/scheduling';
import type { TaskPhase, TaskTemplate, TaskTemplateRequest } from '@/types/tasks';
import { phaseOptions } from '../taskUi';

const ANY = 'any';

const empty = (branchId: number | null, shiftId?: number): TaskTemplateRequest => ({
  shiftId: shiftId ?? 0,
  branchId,
  positionId: null,
  title: '',
  description: '',
  phase: 'during',
  sortOrder: 0,
  isMandatory: true,
  requiresPhoto: false,
});

interface Props {
  open: boolean;
  task: TaskTemplate | null; // null = tạo mới
  /** Ca đang lọc ở danh sách — dùng làm ca mặc định khi thêm việc. */
  defaultShiftId?: number;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Thêm / sửa đầu việc mẫu của checklist (UC 6.1). Admin chọn việc áp dụng mọi chi nhánh hoặc riêng một chi nhánh; Quản lý luôn
 * là việc riêng của chi nhánh mình. Ca và chi nhánh chỉ chọn khi thêm mới (sửa thì khóa — muốn chuyển ca hãy tạo việc mới).
 */
export function TaskTemplateFormModal({ open, task, defaultShiftId, onClose, onSaved }: Props) {
  const isAdmin = useAuthStore((s) => s.hasAnyRole([Roles.Admin]));
  const ownBranchId = useAuthStore((s) => s.user?.branchId ?? null);
  const { branches, positions } = useLookupStore();

  const [shifts, setShifts] = useState<Shift[]>([]);
  const [form, setForm] = useState<TaskTemplateRequest>(empty(null));
  const { saving, error, setError, run } = useFormSubmit();

  useEffect(() => {
    if (!open) return;
    setError(null);
    setForm(
      task
        ? {
            shiftId: task.shiftId,
            branchId: task.branchId,
            positionId: task.positionId,
            title: task.title,
            description: task.description ?? '',
            phase: task.phase,
            sortOrder: task.sortOrder,
            isMandatory: task.isMandatory,
            requiresPhoto: task.requiresPhoto,
          }
        : empty(isAdmin ? null : ownBranchId, defaultShiftId),
    );
    shiftApi
      .list({ pageSize: 100, includeShared: true })
      .then(({ data }) => setShifts(data.items))
      .catch((err) => setError(getErrorMessage(err)));
  }, [open, task, defaultShiftId, isAdmin, ownBranchId]);

  const set = <K extends keyof TaskTemplateRequest>(key: K, value: TaskTemplateRequest[K]) => setForm((f) => ({ ...f, [key]: value }));

  const shift = shifts.find((s) => s.id === form.shiftId);
  // Ca riêng của chi nhánh → việc luôn thuộc chi nhánh đó
  const lockedBranchId = shift?.branchId ?? (isAdmin ? undefined : ownBranchId);
  const effectiveBranchId = lockedBranchId ?? form.branchId;

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!form.shiftId) return setError('Vui lòng chọn ca.');
    if (!form.title.trim()) return setError('Vui lòng nhập tên việc.');
    void run(
      async () => {
        const body = { ...form, branchId: effectiveBranchId ?? null, title: form.title.trim(), description: form.description?.trim() || null };
        const { message } = task ? await taskApi.updateTemplate(task.id, body) : await taskApi.createTemplate(body);
        return message;
      },
      () => {
        onSaved();
        onClose();
      },
    );
  };

  const branchName = branches.find((b) => b.id === effectiveBranchId)?.name;

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="md"
      purpose="form"
      title={task ? 'Sửa việc trong checklist' : 'Thêm việc vào checklist'}
      description="Việc được giao tự động cho nhân viên khi họ check-in vào ca."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" type="submit" form="task-template-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="task-template-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <TextInput
            label="Tên việc"
            isRequired
            placeholder="VD: Kiểm đếm tiền đầu ca tại két"
            value={form.title}
            onChange={(v) => set('title', v)}
            hasAutoFocus
          />
          <TextArea
            label="Mô tả"
            rows={2}
            maxLength={1000}
            placeholder="Hướng dẫn cách làm (không bắt buộc)"
            value={form.description ?? ''}
            onChange={(v) => set('description', v)}
          />

          <Grid columns={{ minWidth: 240, max: 2 }} gap={4}>
            <GridSpan columns="full">
              <Selector
                label="Ca làm việc"
                isRequired
                placeholder="Chọn ca"
                options={shifts.map((s) => ({
                  value: String(s.id),
                  label: `${s.name} (${s.startTime}–${s.endTime})${s.branchId == null ? '' : ' · ca riêng'}`,
                }))}
                value={form.shiftId ? String(form.shiftId) : ''}
                onChange={(v) => set('shiftId', v ? Number(v) : 0)}
                isDisabled={!!task}
                disabledMessage="Không đổi ca của việc đã tạo — hãy tạo việc mới."
              />
            </GridSpan>
            {isAdmin && !lockedBranchId ? (
              <Selector
                label="Áp dụng cho"
                options={[{ value: ANY, label: 'Mọi chi nhánh' }, ...branches.map((b) => ({ value: String(b.id), label: b.name }))]}
                value={form.branchId == null ? ANY : String(form.branchId)}
                onChange={(v) => set('branchId', !v || v === ANY ? null : Number(v))}
                isDisabled={!!task}
                disabledMessage="Không đổi phạm vi của việc đã tạo."
              />
            ) : (
              <TextInput label="Áp dụng cho" value={branchName ? `Chi nhánh ${branchName}` : 'Chi nhánh của bạn'} onChange={() => {}} isDisabled />
            )}
            <Selector
              label="Chức vụ"
              labelTooltip="Chỉ giao cho nhân viên giữ chức vụ này; để 'Mọi chức vụ' thì giao cho tất cả nhân viên của ca."
              options={[{ value: ANY, label: 'Mọi chức vụ' }, ...positions.map((p) => ({ value: String(p.id), label: p.title }))]}
              value={form.positionId == null ? ANY : String(form.positionId)}
              onChange={(v) => set('positionId', !v || v === ANY ? null : Number(v))}
            />
            <Selector label="Giai đoạn" options={phaseOptions} value={form.phase} onChange={(v) => v && set('phase', v as TaskPhase)} />
            <NumberInput
              label="Thứ tự"
              labelTooltip="Nhỏ hiện trước trong cùng giai đoạn."
              min={0}
              max={999}
              isIntegerOnly
              value={form.sortOrder}
              onChange={(v) => set('sortOrder', v ?? 0)}
            />
          </Grid>

          <Switch
            label="Bắt buộc hoàn thành trước khi hết ca"
            description="Việc bắt buộc còn dở sẽ được nhắc khi nhân viên check-out và hiện trong tiến độ của Quản lý."
            value={form.isMandatory}
            onChange={(v) => set('isMandatory', v)}
          />
          <Switch
            label="Phải chụp ảnh hiện trường"
            description="Nhân viên cần kèm ảnh (VD: quầy đã dọn sạch) mới tích hoàn thành được."
            value={form.requiresPhoto}
            onChange={(v) => set('requiresPhoto', v)}
          />
        </VStack>
      </form>
    </Modal>
  );
}
