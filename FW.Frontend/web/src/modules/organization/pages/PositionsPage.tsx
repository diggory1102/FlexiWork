import { useEffect, useState, type FormEvent } from 'react';
import { Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { Button, IconButton } from '@/components/ui/Button';
import { Field, FormError, Input, Textarea } from '@/components/ui/Form';
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
    { key: 'title', header: 'Chức danh', render: (p) => <span className="font-medium">{p.title}</span> },
    {
      key: 'salary',
      header: 'Đơn giá công / giờ',
      className: 'w-48 text-right',
      render: (p) => <span className="font-semibold tabular-nums">{formatCurrency(p.baseSalaryPerHour)}</span>,
    },
    { key: 'desc', header: 'Mô tả', render: (p) => <span className="text-xs text-muted">{p.description ?? '—'}</span> },
    {
      key: 'actions',
      header: '',
      className: 'w-24 text-right',
      render: (p) =>
        canEdit && (
          <div className="flex justify-end gap-0.5">
            <IconButton label="Sửa" onClick={() => setEditing(p)}>
              <Pencil className="h-4 w-4" />
            </IconButton>
            <IconButton label="Xóa" tone="danger" onClick={() => setDeleting(p)}>
              <Trash2 className="h-4 w-4" />
            </IconButton>
          </div>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Chức vụ & đơn giá công"
        description="Đơn giá công theo giờ dùng để tính lương tạm tính trên bảng công."
        actions={
          canEdit && (
            <Button icon={<Plus className="h-4 w-4" />} onClick={() => setEditing(null)}>
              Thêm chức vụ
            </Button>
          )
        }
      />
      <div className="card overflow-hidden">
        <Toolbar>
          <div className="relative w-72">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted" />
            <Input className="pl-9" placeholder="Tìm theo tên chức danh…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(p) => p.id} loading={loading} emptyText="Không có chức vụ nào" />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </div>

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
      title={position ? 'Sửa chức vụ' : 'Thêm chức vụ'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Hủy
          </Button>
          <Button type="submit" form="position-form" loading={saving}>
            Lưu
          </Button>
        </>
      }
    >
      <form id="position-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <Field label="Tên chức danh" required>
          <Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} maxLength={100} required autoFocus />
        </Field>
        <Field label="Đơn giá công theo giờ (VNĐ)" required hint={formatCurrency(form.baseSalaryPerHour || 0) + ' / giờ'}>
          <Input type="number" min={0} step={1000} value={form.baseSalaryPerHour} onChange={(e) => setForm({ ...form, baseSalaryPerHour: Number(e.target.value) })} required />
        </Field>
        <Field label="Mô tả">
          <Textarea value={form.description ?? ''} onChange={(e) => setForm({ ...form, description: e.target.value })} maxLength={255} />
        </Field>
      </form>
    </Modal>
  );
}
