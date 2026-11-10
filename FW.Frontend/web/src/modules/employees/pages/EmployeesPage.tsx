import { useEffect, useState } from 'react';
import { KeyRound, Lock, Pencil, Plus, Search, ShieldCheck, Trash2, Unlock } from 'lucide-react';
import { PageHeader, Toolbar, Avatar } from '@/components/ui/Layout';
import { Button, IconButton } from '@/components/ui/Button';
import { Input, Select } from '@/components/ui/Form';
import { Badge } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { userApi } from '@/services/userApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { roleLabels, userStatusLabels } from '@/lib/format';
import { RoleGroups, Roles, type Role } from '@/types/auth';
import type { RoleOption, User, UserFilter, UserStatus } from '@/types/user';
import { EmployeeFormModal } from '../components/EmployeeFormModal';
import { EmployeeDetailModal, EmployeeRolesModal, EmployeeStatusModal } from '../components/EmployeeDialogs';
import { statusTone } from '../components/statusTone';

const roleTone: Record<Role, 'red' | 'purple' | 'blue' | 'gray'> = {
  ROLE_ADMIN: 'red',
  ROLE_HR: 'purple',
  ROLE_MANAGER: 'blue',
  ROLE_EMPLOYEE: 'gray',
};

/**
 * Danh sách nhân sự. Admin/HR: toàn công ty, đầy đủ thao tác (HR không thao tác trên tài khoản Admin).
 * Manager: chỉ xem nhân sự chi nhánh mình. Đổi vai trò: chỉ Admin.
 */
export function EmployeesPage() {
  const me = useAuthStore((s) => s.user);
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const canManage = hasAnyRole(RoleGroups.AdminOrHr);
  const isAdmin = hasAnyRole([Roles.Admin]);
  const { branches, departments, load: loadLookups } = useLookupStore();
  const { query, setQuery, page, rows, loading, error, reload } = usePagedList<User, UserFilter>(userApi.list, { pageSize: 20 });

  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [roleOptions, setRoleOptions] = useState<RoleOption[]>([]);
  const [editing, setEditing] = useState<User | null | undefined>(undefined);
  const [viewing, setViewing] = useState<User | null>(null);
  const [rolesFor, setRolesFor] = useState<User | null>(null);
  const [statusFor, setStatusFor] = useState<User | null>(null);
  const [reissueFor, setReissueFor] = useState<User | null>(null);
  const [deleting, setDeleting] = useState<User | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    void loadLookups().catch((err) => toast.error(getErrorMessage(err)));
    userApi
      .roles()
      .then(({ data }) => setRoleOptions(data))
      .catch(() => undefined);
  }, [loadLookups]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  /** HR không thao tác trên tài khoản Admin; không ai thao tác trên chính mình (trừ sửa hồ sơ). */
  const canActOn = (u: User) => canManage && u.id !== me?.id && (isAdmin || !u.roles.includes(Roles.Admin));
  const canEditProfile = (u: User) => canManage && (isAdmin || !u.roles.includes(Roles.Admin));

  const runAction = async (action: () => Promise<{ message: string }>, close: () => void) => {
    setBusy(true);
    try {
      toast.success((await action()).message);
      close();
      void reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const columns: Column<User>[] = [
    {
      key: 'name',
      header: 'Nhân viên',
      render: (u) => (
        <div className="flex items-center gap-3">
          <Avatar name={u.fullName} url={u.avatarUrl} size={34} />
          <div className="min-w-0">
            <p className="truncate font-medium">{u.fullName}</p>
            <p className="truncate text-xs text-muted">
              <span className="font-mono">{u.employeeCode}</span> · {u.email}
            </p>
          </div>
        </div>
      ),
    },
    { key: 'branch', header: 'Chi nhánh', render: (u) => <span className="whitespace-nowrap text-xs">{u.branchName ?? '—'}</span> },
    {
      key: 'org',
      header: 'Phòng ban / Chức vụ',
      render: (u) => (
        <div className="max-w-[200px] text-xs">
          <p className="truncate" title={u.departmentName ?? undefined}>
            {u.departmentName ?? '—'}
          </p>
          <p className="truncate text-muted" title={u.positionTitle ?? undefined}>
            {u.positionTitle ?? ''}
          </p>
        </div>
      ),
    },
    {
      key: 'roles',
      header: 'Vai trò',
      render: (u) => (
        <div className="flex flex-wrap gap-1">
          {u.roles.map((r) => (
            <Badge key={r} tone={roleTone[r]}>
              {roleLabels[r]}
            </Badge>
          ))}
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Trạng thái',
      render: (u) => (
        <div className="flex flex-col items-start gap-1">
          <Badge tone={statusTone(u.status)}>{userStatusLabels[u.status]}</Badge>
          {u.mustChangePassword && <span className="text-[11px] text-warning">Chưa đổi MK tạm</span>}
        </div>
      ),
    },
    {
      key: 'actions',
      header: '',
      className: 'w-44 text-right',
      render: (u) => (
        <div className="flex justify-end gap-0.5" onClick={(e) => e.stopPropagation()}>
          {canEditProfile(u) && (
            <IconButton label="Sửa hồ sơ" onClick={() => setEditing(u)}>
              <Pencil className="h-4 w-4" />
            </IconButton>
          )}
          {canActOn(u) && (
            <>
              <IconButton label={u.status === 'active' ? 'Khóa tài khoản' : 'Mở khóa / đổi trạng thái'} onClick={() => setStatusFor(u)}>
                {u.status === 'active' ? <Lock className="h-4 w-4" /> : <Unlock className="h-4 w-4" />}
              </IconButton>
              <IconButton label="Cấp lại mật khẩu" onClick={() => setReissueFor(u)}>
                <KeyRound className="h-4 w-4" />
              </IconButton>
              {isAdmin && (
                <IconButton label="Phân quyền" onClick={() => setRolesFor(u)}>
                  <ShieldCheck className="h-4 w-4" />
                </IconButton>
              )}
              <IconButton label="Xóa" tone="danger" onClick={() => setDeleting(u)}>
                <Trash2 className="h-4 w-4" />
              </IconButton>
            </>
          )}
        </div>
      ),
    },
  ];

  const toId = (value: string) => (value === '' ? '' : Number(value));

  return (
    <>
      <PageHeader
        title="Nhân viên"
        description={canManage ? 'Quản lý hồ sơ, tài khoản và phân quyền nhân sự toàn doanh nghiệp.' : 'Danh sách nhân sự thuộc chi nhánh bạn quản lý.'}
        actions={
          canManage && (
            <Button icon={<Plus className="h-4 w-4" />} onClick={() => setEditing(null)}>
              Thêm nhân viên
            </Button>
          )
        }
      />
      <div className="card overflow-hidden">
        <Toolbar>
          <div className="relative w-72">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted" />
            <Input className="pl-9" placeholder="Tìm mã, họ tên, email, SĐT…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          {canManage && (
            <Select className="w-48" value={query.branchId ?? ''} onChange={(e) => setQuery({ branchId: toId(e.target.value) })}>
              <option value="">Tất cả chi nhánh</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </Select>
          )}
          <Select className="w-48" value={query.departmentId ?? ''} onChange={(e) => setQuery({ departmentId: toId(e.target.value) })}>
            <option value="">Tất cả phòng ban</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
          <Select className="w-40" value={query.roleName ?? ''} onChange={(e) => setQuery({ roleName: e.target.value as Role | '' })}>
            <option value="">Tất cả vai trò</option>
            {Object.values(Roles).map((r) => (
              <option key={r} value={r}>
                {roleLabels[r]}
              </option>
            ))}
          </Select>
          <Select className="w-40" value={query.status ?? ''} onChange={(e) => setQuery({ status: e.target.value as UserStatus | '' })}>
            <option value="">Mọi trạng thái</option>
            <option value="active">{userStatusLabels.active}</option>
            <option value="suspended">{userStatusLabels.suspended}</option>
            <option value="left">{userStatusLabels.left}</option>
          </Select>
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(u) => u.id} loading={loading} emptyText="Không tìm thấy nhân viên" onRowClick={setViewing} />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </div>

      <EmployeeFormModal user={editing} roleOptions={roleOptions} onClose={() => setEditing(undefined)} onSaved={reload} />
      <EmployeeDetailModal user={viewing} onClose={() => setViewing(null)} />
      <EmployeeRolesModal user={rolesFor} roleOptions={roleOptions} onClose={() => setRolesFor(null)} onSaved={reload} />
      <EmployeeStatusModal user={statusFor} onClose={() => setStatusFor(null)} onSaved={reload} />
      <ConfirmDialog
        open={!!reissueFor}
        tone="primary"
        title="Cấp lại mật khẩu"
        message={
          <>
            Sinh mật khẩu tạm mới cho <b>{reissueFor?.fullName}</b> và gửi tới <b>{reissueFor?.email}</b>? Mật khẩu hiện tại sẽ không dùng được nữa.
          </>
        }
        confirmText="Cấp lại"
        loading={busy}
        onConfirm={() => reissueFor && runAction(() => userApi.reissuePassword(reissueFor.id), () => setReissueFor(null))}
        onClose={() => setReissueFor(null)}
      />
      <ConfirmDialog
        open={!!deleting}
        title="Xóa hồ sơ nhân sự"
        message={
          <>
            Xóa hồ sơ <b>{deleting?.fullName}</b>? Nếu nhân viên nghỉ việc, nên chuyển trạng thái <b>Đã nghỉ việc</b> để giữ lịch sử chấm công.
          </>
        }
        confirmText="Xóa"
        loading={busy}
        onConfirm={() => deleting && runAction(() => userApi.remove(deleting.id), () => setDeleting(null))}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}
