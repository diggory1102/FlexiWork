import { useEffect, useState, type ReactNode } from 'react';
import { Modal } from '@/components/ui/Modal';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, FormError, Select } from '@/components/ui/Form';
import { Avatar } from '@/components/ui/Layout';
import { Badge } from '@/components/ui/Feedback';
import { userApi } from '@/services/userApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { employmentTypeLabels, formatDate, formatDateTime, roleLabels, userStatusLabels } from '@/lib/format';
import type { Role } from '@/types/auth';
import type { RoleOption, User, UserStatus } from '@/types/user';
import { statusTone } from './statusTone';

/** Phân quyền (chỉ Admin). */
export function EmployeeRolesModal({ user, roleOptions, onClose, onSaved }: { user: User | null; roleOptions: RoleOption[]; onClose: () => void; onSaved: () => void }) {
  const [roles, setRoles] = useState<Role[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setRoles(user?.roles ?? []);
    setError(null);
  }, [user]);

  const save = async () => {
    if (!user) return;
    setSaving(true);
    setError(null);
    try {
      toast.success((await userApi.updateRoles(user.id, roles)).message);
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
      open={!!user}
      onClose={onClose}
      size="sm"
      title="Phân quyền"
      description={user ? `${user.fullName} (${user.employeeCode})` : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Hủy
          </Button>
          <Button onClick={save} loading={saving} disabled={roles.length === 0}>
            Lưu
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <FormError message={error} />
        {roleOptions.map((r) => (
          <Checkbox
            key={r.id}
            label={roleLabels[r.roleName]}
            description={r.description ?? undefined}
            checked={roles.includes(r.roleName)}
            onChange={() => setRoles((cur) => (cur.includes(r.roleName) ? cur.filter((x) => x !== r.roleName) : [...cur, r.roleName]))}
          />
        ))}
      </div>
    </Modal>
  );
}

/** Khóa / mở khóa / đánh dấu nghỉ việc. */
export function EmployeeStatusModal({ user, onClose, onSaved }: { user: User | null; onClose: () => void; onSaved: () => void }) {
  const [status, setStatus] = useState<UserStatus>('active');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (user) setStatus(user.status === 'active' ? 'suspended' : 'active');
    setError(null);
  }, [user]);

  const save = async () => {
    if (!user) return;
    setSaving(true);
    setError(null);
    try {
      toast.success((await userApi.updateStatus(user.id, status)).message);
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
      open={!!user}
      onClose={onClose}
      size="sm"
      title="Trạng thái tài khoản"
      description={user ? `${user.fullName} — hiện tại: ${userStatusLabels[user.status]}` : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Hủy
          </Button>
          <Button onClick={save} loading={saving} variant={status === 'active' ? 'primary' : 'danger'} disabled={status === user?.status}>
            Cập nhật
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <FormError message={error} />
        <Field label="Trạng thái mới" hint="Tài khoản bị khóa hoặc đã nghỉ việc sẽ không đăng nhập được.">
          <Select value={status} onChange={(e) => setStatus(e.target.value as UserStatus)}>
            <option value="active">{userStatusLabels.active}</option>
            <option value="suspended">{userStatusLabels.suspended}</option>
            <option value="left">{userStatusLabels.left}</option>
          </Select>
        </Field>
      </div>
    </Modal>
  );
}

/** Xem chi tiết hồ sơ. */
export function EmployeeDetailModal({ user, onClose }: { user: User | null; onClose: () => void }) {
  if (!user) return null;
  const rows: [string, ReactNode][] = [
    ['Mã nhân viên', <span className="font-mono">{user.employeeCode}</span>],
    ['Email', user.email],
    ['Số điện thoại', user.phoneNumber ?? '—'],
    ['Chi nhánh', user.branchName ?? '—'],
    ['Phòng ban', user.departmentName ?? '—'],
    ['Chức vụ', user.positionTitle ?? '—'],
    ['Loại hợp đồng', employmentTypeLabels[user.employmentType]],
    ['Ngày vào làm', formatDate(user.hireDate)],
    ['Vai trò', user.roles.map((r) => roleLabels[r]).join(', ')],
    ['Ngày tạo', formatDateTime(user.createdAt)],
  ];

  return (
    <Modal open onClose={onClose} title="Hồ sơ nhân viên" size="md">
      <div className="mb-4 flex items-center gap-3">
        <Avatar name={user.fullName} url={user.avatarUrl} size={56} />
        <div>
          <p className="text-base font-semibold">{user.fullName}</p>
          <div className="mt-1 flex gap-1.5">
            <Badge tone={statusTone(user.status)}>{userStatusLabels[user.status]}</Badge>
            {user.mustChangePassword && <Badge tone="amber">Chưa đổi mật khẩu tạm</Badge>}
          </div>
        </div>
      </div>
      <dl className="grid grid-cols-[140px_1fr] gap-x-4 gap-y-2.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted">{label}</dt>
            <dd className="text-foreground">{value}</dd>
          </div>
        ))}
      </dl>
    </Modal>
  );
}
