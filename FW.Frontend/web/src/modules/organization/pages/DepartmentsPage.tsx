import { useEffect, useState, type FormEvent } from 'react';
import { Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { Button, IconButton } from '@/components/ui/Button';
import { Field, FormError, Input, Textarea } from '@/components/ui/Form';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { departmentApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { formatDate } from '@/lib/format';
import { RoleGroups } from '@/types/auth';
import type { PagedQuery } from '@/types/api';
import type { Department, DepartmentRequest } from '@/types/organization';

/** Danh mục phòng ban: Admin/HR thêm-sửa-xóa, Manager chỉ xem. */
export function DepartmentsPage() {
  const canEdit = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const invalidateLookups = useLookupStore((s) => s.invalidate);
  const { setQuery, page, rows, loading, error, reload } = usePagedList<Department, PagedQuery>(departmentApi.list, { pageSize: 20 });
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [editing, setEditing] = useState<Department | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<Department | null>(null);
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
      toast.success((await departmentApi.remove(deleting.id)).message);
      setDeleting(null);
      afterChange();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const columns: Column<Department>[] = [
    { key: 'code', header: 'Mã', className: 'w-36', render: (d) => <span className="font-mono text-xs font-semibold">{d.code}</span> },
    { key: 'name', header: 'Tên phòng ban', render: (d) => <span className="font-medium">{d.name}</span> },
    { key: 'desc', header: 'Mô tả', render: (d) => <span className="text-xs text-muted">{d.description ?? '—'}</span> },
    { key: 'created', header: 'Ngày tạo', className: 'w-28', render: (d) => <span className="text-xs text-muted">{formatDate(d.createdAt)}</span> },
    {
      key: 'actions',
      header: '',
      className: 'w-24 text-right',
      render: (d) =>
        canEdit && (
          <div className="flex justify-end gap-0.5">
            <IconButton label="Sửa" onClick={() => setEditing(d)}>
              <Pencil className="h-4 w-4" />
            </IconButton>
            <IconButton label="Xóa" tone="danger" onClick={() => setDeleting(d)}>
              <Trash2 className="h-4 w-4" />
            </IconButton>
          </div>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Phòng ban"
        description="Cơ cấu tổ chức của doanh nghiệp."
        actions={
          canEdit && (
            <Button icon={<Plus className="h-4 w-4" />} onClick={() => setEditing(null)}>
              Thêm phòng ban
            </Button>
          )
        }
      />
      <div className="card overflow-hidden">
        <Toolbar>
          <div className="relative w-72">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted" />
            <Input className="pl-9" placeholder="Tìm theo mã, tên…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(d) => d.id} loading={loading} emptyText="Không có phòng ban nào" />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </div>

      <DepartmentFormModal department={editing} onClose={() => setEditing(undefined)} onSaved={afterChange} />
      <ConfirmDialog
        open={!!deleting}
        title="Xóa phòng ban"
        message={
          <>
            Xóa phòng ban <b>{deleting?.name}</b>? Không thể xóa nếu phòng ban vẫn còn nhân sự.
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

function DepartmentFormModal({ department, onClose, onSaved }: { department: Department | null | undefined; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<DepartmentRequest>({ code: '', name: '', description: '' });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (department === undefined) return;
    setError(null);
    setForm(department ? { code: department.code, name: department.name, description: department.description ?? '' } : { code: '', name: '', description: '' });
  }, [department]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const body = { ...form, description: form.description || null };
      const { message } = department ? await departmentApi.update(department.id, body) : await departmentApi.create(body);
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
      open={department !== undefined}
      onClose={onClose}
      size="sm"
      title={department ? 'Sửa phòng ban' : 'Thêm phòng ban'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Hủy
          </Button>
          <Button type="submit" form="department-form" loading={saving}>
            Lưu
          </Button>
        </>
      }
    >
      <form id="department-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <Field label="Mã phòng ban" required hint="VD: DEP-OPS — tự chuyển chữ hoa, không trùng.">
          <Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} maxLength={20} required autoFocus />
        </Field>
        <Field label="Tên phòng ban" required>
          <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} maxLength={100} required />
        </Field>
        <Field label="Mô tả">
          <Textarea value={form.description ?? ''} onChange={(e) => setForm({ ...form, description: e.target.value })} maxLength={255} />
        </Field>
      </form>
    </Modal>
  );
}
