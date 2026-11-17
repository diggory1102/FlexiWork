import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Plus } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { FormError } from '@/components/ui/Feedback';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { RowActions } from '@/components/ui/RowActions';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { useDeleteDialog } from '@/hooks/useDeleteDialog';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { departmentApi } from '@/services/organizationApi';
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

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const afterChange = () => {
    invalidateLookups();
    void reload();
  };

  const del = useDeleteDialog((item: Department) => departmentApi.remove(item.id), afterChange);

  const columns: Column<Department>[] = [
    { key: 'code', header: 'Mã', width: 140, render: (d) => <span className="font-mono text-sm font-medium">{d.code}</span> },
    { key: 'name', header: 'Tên phòng ban', render: (d) => <Text weight="medium">{d.name}</Text> },
    { key: 'desc', header: 'Mô tả', render: (d) => <Text type="supporting">{d.description ?? '—'}</Text> },
    { key: 'created', header: 'Ngày tạo', width: 130, render: (d) => <Text type="supporting">{formatDate(d.createdAt)}</Text> },
    {
      key: 'actions',
      header: '',
      width: 110,
      align: 'end',
      render: (d) => canEdit && <RowActions onEdit={() => setEditing(d)} onDelete={() => del.ask(d)} />,
    },
  ];

  return (
    <>
      <PageHeader
        title="Phòng ban"
        description="Cơ cấu tổ chức của doanh nghiệp."
        actions={canEdit && <Button label="Thêm phòng ban" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />}
      />
      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm theo mã, tên…" value={search} onChange={setSearch} />
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(d) => d.id} loading={loading} emptyText="Không có phòng ban nào" />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <DepartmentFormModal department={editing} onClose={() => setEditing(undefined)} onSaved={afterChange} />
      <ConfirmDialog
        {...del.dialog}
        title="Xóa phòng ban"
        message={
          <>
            Xóa phòng ban <b>{del.target?.name}</b>? Không thể xóa nếu phòng ban vẫn còn nhân sự.
          </>
        }
        confirmText="Xóa"
      />
    </>
  );
}

function DepartmentFormModal({ department, onClose, onSaved }: { department: Department | null | undefined; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<DepartmentRequest>({ code: '', name: '', description: '' });
  const { saving, error, setError, run } = useFormSubmit();

  useEffect(() => {
    if (department === undefined) return;
    setError(null);
    setForm(department ? { code: department.code, name: department.name, description: department.description ?? '' } : { code: '', name: '', description: '' });
  }, [department, setError]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!form.code.trim() || !form.name.trim()) return setError('Vui lòng nhập mã và tên phòng ban.');
    const body = { ...form, description: form.description || null };
    void run(
      async () => (department ? await departmentApi.update(department.id, body) : await departmentApi.create(body)).message,
      () => {
        onSaved();
        onClose();
      },
    );
  };

  return (
    <Modal
      open={department !== undefined}
      onClose={onClose}
      size="sm"
      purpose="form"
      title={department ? 'Sửa phòng ban' : 'Thêm phòng ban'}
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" type="submit" form="department-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="department-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <TextInput
            label="Mã phòng ban"
            isRequired
            description="VD: DEP-OPS — tự chuyển chữ hoa, không trùng."
            value={form.code}
            onChange={(code) => setForm({ ...form, code })}
            hasAutoFocus
          />
          <TextInput label="Tên phòng ban" isRequired value={form.name} onChange={(name) => setForm({ ...form, name })} />
          <TextArea label="Mô tả" value={form.description ?? ''} onChange={(description) => setForm({ ...form, description })} maxLength={255} />
        </VStack>
      </form>
    </Modal>
  );
}
