import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { CheckboxInput } from '@astryxdesign/core/CheckboxInput';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading } from '@astryxdesign/core/Text';
import { Modal } from '@/components/ui/Modal';
import { Avatar } from '@/components/ui/Layout';
import { Badge, FormError } from '@/components/ui/Feedback';
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
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" variant="primary" onClick={save} isLoading={saving} isDisabled={roles.length === 0} />
        </>
      }
    >
      <VStack gap={3}>
        <FormError message={error} />
        {roleOptions.map((r) => (
          <CheckboxInput
            key={r.id}
            label={roleLabels[r.roleName]}
            description={r.description ?? undefined}
            value={roles.includes(r.roleName)}
            onChange={() => setRoles((cur) => (cur.includes(r.roleName) ? cur.filter((x) => x !== r.roleName) : [...cur, r.roleName]))}
          />
        ))}
      </VStack>
    </Modal>
  );
}

const statusOptions = (Object.keys(userStatusLabels) as UserStatus[]).map((value) => ({ value, label: userStatusLabels[value] }));

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
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button
            label="Cập nhật"
            onClick={save}
            isLoading={saving}
            variant={status === 'active' ? 'primary' : 'destructive'}
            isDisabled={status === user?.status}
          />
        </>
      }
    >
      <VStack gap={3}>
        <FormError message={error} />
        <Selector
          label="Trạng thái mới"
          description="Tài khoản bị khóa hoặc đã nghỉ việc sẽ không đăng nhập được."
          options={statusOptions}
          value={status}
          onChange={(v) => v && setStatus(v as UserStatus)}
        />
      </VStack>
    </Modal>
  );
}

/** Xem chi tiết hồ sơ. */
export function EmployeeDetailModal({ user, onClose }: { user: User | null; onClose: () => void }) {
  if (!user) return null;
  return (
    <Modal open onClose={onClose} title="Hồ sơ nhân viên" size="md">
      <VStack gap={5}>
        <HStack gap={3} vAlign="center">
          <Avatar name={user.fullName} url={user.avatarUrl} size={60} />
          <VStack gap={1}>
            <Heading level={3}>{user.fullName}</Heading>
            <HStack gap={1.5} wrap="wrap">
              <Badge tone={statusTone(user.status)}>{userStatusLabels[user.status]}</Badge>
              {user.mustChangePassword && <Badge tone="amber">Chưa đổi mật khẩu tạm</Badge>}
            </HStack>
          </VStack>
        </HStack>
        <MetadataList label={{ position: 'start', width: 140 }}>
          <MetadataListItem label="Mã nhân viên">
            <span className="font-mono">{user.employeeCode}</span>
          </MetadataListItem>
          <MetadataListItem label="Email">{user.email}</MetadataListItem>
          <MetadataListItem label="Số điện thoại">{user.phoneNumber ?? '—'}</MetadataListItem>
          <MetadataListItem label="Chi nhánh">{user.branchName ?? '—'}</MetadataListItem>
          <MetadataListItem label="Phòng ban">{user.departmentName ?? '—'}</MetadataListItem>
          <MetadataListItem label="Chức vụ">{user.positionTitle ?? '—'}</MetadataListItem>
          <MetadataListItem label="Loại hợp đồng">{employmentTypeLabels[user.employmentType]}</MetadataListItem>
          <MetadataListItem label="Ngày vào làm">{formatDate(user.hireDate)}</MetadataListItem>
          <MetadataListItem label="Vai trò">{user.roles.map((r) => roleLabels[r]).join(', ')}</MetadataListItem>
          <MetadataListItem label="Ngày tạo">{formatDateTime(user.createdAt)}</MetadataListItem>
        </MetadataList>
      </VStack>
    </Modal>
  );
}
