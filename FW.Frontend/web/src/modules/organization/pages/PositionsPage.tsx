import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { FormError } from '@/components/ui/Feedback';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { positionApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { formatCurrency } from '@/lib/format';
import { RoleGroups } from '@/types/auth';
import type { PagedQuery } from '@/types/api';
import type { Position, PositionRequest } from '@/types/organization';

/** Chức vụ & đơn giá công theo giờ (dùng tính lương tạm tính): Admin/HR thêm-sửa-xóa, Manager chỉ xem. */
export function PositionsPage() {
  const canEdit = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const invalidateLookups = useLookupStore((s) => s.invalidate);
  const { setQuery, page, rows, loading, error, reload } = usePagedList<Position, PagedQuery>(positionApi.list, { pageSize: 20 });
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [editing, setEditing] = useState<Position | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<Position | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const afterChange = () => {
    invalidateLookups();
    void reload();
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      toast.success((await positionApi.remove(deleting.id)).message);
      setDeleting(null);
      afterChange();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const columns: Column<Position>[] = [
    { key: 'title', header: 'Chức danh', render: (p) => <Text weight="medium">{p.title}</Text> },
    {
      key: 'salary',
      header: 'Đơn giá công / giờ',
      width: 200,
      align: 'end',
      render: (p) => (
        <Text weight="semibold" hasTabularNumbers>
          {formatCurrency(p.baseSalaryPerHour)}
        </Text>
      ),
    },
    { key: 'desc', header: 'Mô tả', render: (p) => <Text type="supporting">{p.description ?? '—'}</Text> },
    {
      key: 'actions',
      header: '',
      width: 110,
      align: 'end',
      render: (p) =>
        canEdit && (
          <HStack gap={0.5} hAlign="end">
            <IconButton variant="ghost" size="sm" label="Sửa" tooltip="Sửa" icon={<Icon icon={Pencil} />} onClick={() => setEditing(p)} />
            <IconButton variant="ghost" size="sm" label="Xóa" tooltip="Xóa" icon={<Icon icon={Trash2} color="error" />} onClick={() => setDeleting(p)} />
          </HStack>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Chức vụ & đơn giá công"
        description="Đơn giá công theo giờ dùng để tính lương tạm tính trên bảng công."
        actions={canEdit && <Button label="Thêm chức vụ" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />}
      />
      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm theo tên chức danh…" value={search} onChange={setSearch} />
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(p) => p.id} loading={loading} emptyText="Không có chức vụ nào" />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <PositionFormModal position={editing} onClose={() => setEditing(undefined)} onSaved={afterChange} />
      <ConfirmDialog
        open={!!deleting}
        title="Xóa chức vụ"
        message={
          <>
            Xóa chức vụ <b>{deleting?.title}</b>? Không thể xóa nếu vẫn còn nhân sự giữ chức danh này.
          </>
        }
        confirmText="Xóa"
        loading={busy}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}

function PositionFormModal({ position, onClose, onSaved }: { position: Position | null | undefined; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<PositionRequest>({ title: '', baseSalaryPerHour: 0, description: '' });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (position === undefined) return;
    setError(null);
    setForm(position ? { title: position.title, baseSalaryPerHour: position.baseSalaryPerHour, description: position.description ?? '' } : { title: '', baseSalaryPerHour: 0, description: '' });
  }, [position]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) return setError('Vui lòng nhập tên chức danh.');
    setSaving(true);
    setError(null);
    try {
      const body = { ...form, description: form.description || null };
      const { message } = position ? await positionApi.update(position.id, body) : await positionApi.create(body);
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
      open={position !== undefined}
      onClose={onClose}
      size="sm"
      purpose="form"
      title={position ? 'Sửa chức vụ' : 'Thêm chức vụ'}
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" type="submit" form="position-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="position-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <TextInput label="Tên chức danh" isRequired value={form.title} onChange={(title) => setForm({ ...form, title })} hasAutoFocus />
          <NumberInput
            label="Đơn giá công theo giờ"
            isRequired
            units="VNĐ"
            min={0}
            step={1000}
            isIntegerOnly
            description={`${formatCurrency(form.baseSalaryPerHour || 0)} / giờ`}
            value={form.baseSalaryPerHour}
            onChange={(v) => setForm({ ...form, baseSalaryPerHour: v ?? 0 })}
          />
          <TextArea label="Mô tả" value={form.description ?? ''} onChange={(description) => setForm({ ...form, description })} maxLength={255} />
        </VStack>
      </form>
    </Modal>
  );
}
