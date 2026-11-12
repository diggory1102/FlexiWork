import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { KeyRound, Lock, Pencil, Plus, ShieldCheck, Trash2, Unlock } from 'lucide-react';
import { Avatar, PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
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

const roleFilterOptions = [{ value: '', label: 'Tất cả vai trò' }, ...Object.values(Roles).map((r) => ({ value: r, label: roleLabels[r] }))];
const statusFilterOptions = [
  { value: '', label: 'Mọi trạng thái' },
  ...(Object.keys(userStatusLabels) as UserStatus[]).map((s) => ({ value: s, label: userStatusLabels[s] })),
];

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
      width: 260,
      render: (u) => (
        <HStack gap={3} vAlign="center">
          <Avatar name={u.fullName} url={u.avatarUrl} size={36} />
          <VStack gap={0.5} className="min-w-0">
            <Text weight="medium" maxLines={1}>
              {u.fullName}
            </Text>
            <Text type="supporting" maxLines={1}>
              <span className="font-mono">{u.employeeCode}</span> · {u.email}
            </Text>
          </VStack>
        </HStack>
      ),
    },
    { key: 'branch', header: 'Chi nhánh', render: (u) => <Text type="supporting">{u.branchName ?? '—'}</Text> },
    {
      key: 'org',
      header: 'Phòng ban / Chức vụ',
      render: (u) => (
        <VStack gap={0.5} className="max-w-[220px]">
          <Text maxLines={1}>{u.departmentName ?? '—'}</Text>
          <Text type="supporting" maxLines={1}>
            {u.positionTitle ?? ''}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'roles',
      header: 'Vai trò',
      render: (u) => (
        <HStack gap={1} wrap="wrap">
          {u.roles.map((r) => (
            <Badge key={r} tone={roleTone[r]}>
              {roleLabels[r]}
            </Badge>
          ))}
        </HStack>
      ),
    },
    {
      key: 'status',
      header: 'Trạng thái',
      render: (u) => (
        <VStack gap={1} hAlign="start">
          <Badge tone={statusTone(u.status)}>{userStatusLabels[u.status]}</Badge>
          {u.mustChangePassword && (
            <Text type="supporting" color="secondary">
              Chưa đổi MK tạm
            </Text>
          )}
        </VStack>
      ),
    },
    {
      key: 'actions',
      header: '',
      width: 200,
      align: 'end',
      render: (u) => (
        <div onClick={(e) => e.stopPropagation()}>
          <HStack gap={0.5} hAlign="end">
            {canEditProfile(u) && <IconButton variant="ghost" size="sm" label="Sửa hồ sơ" tooltip="Sửa hồ sơ" icon={<Icon icon={Pencil} />} onClick={() => setEditing(u)} />}
            {canActOn(u) && (
              <>
                <IconButton
                  variant="ghost"
                  size="sm"
                  label={u.status === 'active' ? 'Khóa tài khoản' : 'Mở khóa / đổi trạng thái'}
                  tooltip={u.status === 'active' ? 'Khóa tài khoản' : 'Mở khóa / đổi trạng thái'}
                  icon={<Icon icon={u.status === 'active' ? Lock : Unlock} />}
                  onClick={() => setStatusFor(u)}
                />
                <IconButton variant="ghost" size="sm" label="Cấp lại mật khẩu" tooltip="Cấp lại mật khẩu" icon={<Icon icon={KeyRound} />} onClick={() => setReissueFor(u)} />
                {isAdmin && <IconButton variant="ghost" size="sm" label="Phân quyền" tooltip="Phân quyền" icon={<Icon icon={ShieldCheck} />} onClick={() => setRolesFor(u)} />}
                <IconButton variant="ghost" size="sm" label="Xóa" tooltip="Xóa" icon={<Icon icon={Trash2} color="error" />} onClick={() => setDeleting(u)} />
              </>
            )}
          </HStack>
        </div>
      ),
    },
  ];

  const toId = (value: string | null) => (value ? Number(value) : '');

  return (
    <>
      <PageHeader
        title="Nhân viên"
        description={canManage ? 'Quản lý hồ sơ, tài khoản và phân quyền nhân sự toàn doanh nghiệp.' : 'Danh sách nhân sự thuộc chi nhánh bạn quản lý.'}
        actions={canManage && <Button label="Thêm nhân viên" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />}
      />
      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm mã, họ tên, email, SĐT…" value={search} onChange={setSearch} />
          {canManage && (
            <Selector
              label="Chi nhánh"
              isLabelHidden
              width={210}
              options={[{ value: '', label: 'Tất cả chi nhánh' }, ...branches.map((b) => ({ value: String(b.id), label: b.name }))]}
              value={String(query.branchId ?? '')}
              onChange={(v) => setQuery({ branchId: toId(v) })}
            />
          )}
          <Selector
            label="Phòng ban"
            isLabelHidden
            width={210}
            options={[{ value: '', label: 'Tất cả phòng ban' }, ...departments.map((d) => ({ value: String(d.id), label: d.name }))]}
            value={String(query.departmentId ?? '')}
            onChange={(v) => setQuery({ departmentId: toId(v) })}
          />
          <Selector label="Vai trò" isLabelHidden width={180} options={roleFilterOptions} value={query.roleName ?? ''} onChange={(v) => setQuery({ roleName: (v ?? '') as Role | '' })} />
          <Selector label="Trạng thái" isLabelHidden width={180} options={statusFilterOptions} value={query.status ?? ''} onChange={(v) => setQuery({ status: (v ?? '') as UserStatus | '' })} />
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(u) => u.id} loading={loading} emptyText="Không tìm thấy nhân viên" onRowClick={setViewing} />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

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
