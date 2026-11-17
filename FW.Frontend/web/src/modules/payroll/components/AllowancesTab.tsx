import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Switch } from '@astryxdesign/core/Switch';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Plus } from 'lucide-react';
import { Badge, FormError } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { ListCard } from '@/components/ui/ListCard';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { RowActions } from '@/components/ui/RowActions';
import { useApiData } from '@/hooks/useApiData';
import { useDeleteDialog } from '@/hooks/useDeleteDialog';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { payrollConfigApi } from '@/services/payrollApi';
import { formatCurrency } from '@/lib/format';
import type { Allowance, AllowanceBasis, AllowanceRequest } from '@/types/payroll';
import { basisLabels, basisOptions, basisUnit } from '../payrollUi';

const emptyForm: AllowanceRequest = { name: '', basis: 'monthly', amount: 0, applyToAll: false, isActive: true, description: '' };

/** Danh mục phụ cấp: tiền phụ cấp = số lượng × mức tiền (theo tháng, theo ca đêm hoặc theo ngày ca gãy). */
export function AllowancesTab() {
  const { data, loading, reload } = useApiData(() => payrollConfigApi.allowances(), []);
  const [editing, setEditing] = useState<Allowance | null | undefined>(undefined); // undefined = đóng, null = tạo mới
  const del = useDeleteDialog((item: Allowance) => payrollConfigApi.removeAllowance(item.id), () => void reload());

  const columns: Column<Allowance>[] = [
    {
      key: 'name',
      header: 'Phụ cấp',
      render: (a) => (
        <VStack gap={0.5}>
          <Text weight="medium">{a.name}</Text>
          {a.description && (
            <Text type="supporting" maxLines={1}>
              {a.description}
            </Text>
          )}
        </VStack>
      ),
    },
    { key: 'basis', header: 'Cách tính', width: 190, render: (a) => <Text>{basisLabels[a.basis]}</Text> },
    {
      key: 'amount',
      header: 'Mức tiền',
      width: 190,
      align: 'end',
      render: (a) => (
        <Text weight="semibold" hasTabularNumbers>
          {formatCurrency(a.amount)} <span className="font-normal text-secondary">{basisUnit[a.basis]}</span>
        </Text>
      ),
    },
    {
      key: 'scope',
      header: 'Áp dụng cho',
      width: 190,
      render: (a) => (a.applyToAll ? <Badge tone="blue">Tất cả nhân viên</Badge> : <Text type="supporting">{a.assignedCount} nhân viên được gán</Text>),
    },
    { key: 'status', header: 'Trạng thái', width: 130, render: (a) => <Badge tone={a.isActive ? 'green' : 'gray'}>{a.isActive ? 'Đang dùng' : 'Tạm ngừng'}</Badge> },
    { key: 'actions', header: '', width: 100, align: 'end', render: (a) => <RowActions onEdit={() => setEditing(a)} onDelete={() => del.ask(a)} /> },
  ];

  return (
    <VStack gap={3}>
      <HStack hAlign="between" vAlign="center" wrap="wrap" gap={2}>
        <Text type="supporting">Phụ cấp áp dụng cho tất cả hoặc gán riêng cho từng nhân viên (ở tab “Lương nhân viên”).</Text>
        <Button label="Thêm phụ cấp" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />
      </HStack>
      <ListCard>
        <DataTable columns={columns} rows={data ?? []} rowKey={(a) => a.id} loading={loading} emptyText="Chưa có phụ cấp nào" onRowClick={setEditing} />
      </ListCard>

      <AllowanceFormModal allowance={editing} onClose={() => setEditing(undefined)} onSaved={() => void reload()} />
      <ConfirmDialog
        {...del.dialog}
        title="Xóa phụ cấp"
        message={
          <>
            Xóa phụ cấp <b>{del.target?.name}</b>? Phiếu lương đã chốt vẫn giữ nguyên; các kỳ tính sau sẽ không còn khoản này.
          </>
        }
        confirmText="Xóa"
      />
    </VStack>
  );
}

function AllowanceFormModal({ allowance, onClose, onSaved }: { allowance: Allowance | null | undefined; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<AllowanceRequest>(emptyForm);
  const { saving, error, setError, run } = useFormSubmit();

  useEffect(() => {
    if (allowance === undefined) return;
    setError(null);
    setForm(
      allowance
        ? { name: allowance.name, basis: allowance.basis, amount: allowance.amount, applyToAll: allowance.applyToAll, isActive: allowance.isActive, description: allowance.description ?? '' }
        : emptyForm,
    );
  }, [allowance, setError]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) return setError('Vui lòng nhập tên phụ cấp.');
    if (form.amount < 0) return setError('Mức tiền không được âm.');
    const body = { ...form, name: form.name.trim(), description: form.description?.trim() || null };
    void run(
      async () => {
        await (allowance ? payrollConfigApi.updateAllowance(allowance.id, body) : payrollConfigApi.createAllowance(body));
        return allowance ? 'Đã cập nhật phụ cấp.' : 'Đã thêm phụ cấp.';
      },
      () => {
        onSaved();
        onClose();
      },
    );
  };

  return (
    <Modal
      open={allowance !== undefined}
      onClose={onClose}
      size="sm"
      purpose="form"
      title={allowance ? 'Sửa phụ cấp' : 'Thêm phụ cấp'}
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" type="submit" form="allowance-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="allowance-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <TextInput label="Tên phụ cấp" isRequired placeholder="VD: Phụ cấp ăn trưa" value={form.name} onChange={(name) => setForm({ ...form, name })} hasAutoFocus />
          <Selector
            label="Cách tính"
            isRequired
            options={basisOptions}
            value={form.basis}
            description="Số lượng nhân với mức tiền: cố định mỗi tháng, theo số ca đêm hoặc theo số ngày làm ca gãy."
            onChange={(v) => v && setForm({ ...form, basis: v as AllowanceBasis })}
          />
          <NumberInput
            label="Mức tiền"
            isRequired
            units="VNĐ"
            min={0}
            step={1000}
            isIntegerOnly
            description={`${formatCurrency(form.amount || 0)} ${basisUnit[form.basis]}`}
            value={form.amount}
            onChange={(v) => setForm({ ...form, amount: v ?? 0 })}
          />
          <Switch label="Áp dụng cho tất cả nhân viên" description="Tắt: chỉ nhân viên được gán riêng mới hưởng." value={form.applyToAll} onChange={(applyToAll) => setForm({ ...form, applyToAll })} />
          <Switch label="Đang sử dụng" description="Tạm ngừng thì không tính vào bảng lương mới." value={form.isActive} onChange={(isActive) => setForm({ ...form, isActive })} />
          <TextArea label="Mô tả" value={form.description ?? ''} onChange={(description) => setForm({ ...form, description })} maxLength={255} />
        </VStack>
      </form>
    </Modal>
  );
}
