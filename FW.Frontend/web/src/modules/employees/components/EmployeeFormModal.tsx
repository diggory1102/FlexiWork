import { useEffect, useState, type FormEvent } from 'react';
import { Modal } from '@/components/ui/Modal';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, FormError, Input, Select } from '@/components/ui/Form';
import { userApi } from '@/services/userApi';
import { getErrorMessage } from '@/services/apiClient';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { roleLabels } from '@/lib/format';
import { Roles, type Role } from '@/types/auth';
import type { EmploymentType, RoleOption, User, UserProfileRequest } from '@/types/user';

interface Props {
  user: User | null | undefined; // undefined = đóng, null = tạo mới
  roleOptions: RoleOption[];
  onClose: () => void;
  onSaved: () => void;
}

const emptyForm: UserProfileRequest = {
  employeeCode: '',
  email: '',
  fullName: '',
  phoneNumber: '',
  branchId: null,
  departmentId: null,
  positionId: null,
  employmentType: 'full_time',
  hireDate: null,
};

/** Tạo / sửa hồ sơ nhân sự. Khi tạo mới, hệ thống gửi mật khẩu tạm qua email cho nhân viên. */
export function EmployeeFormModal({ user, roleOptions, onClose, onSaved }: Props) {
  const { branches, departments, positions } = useLookupStore();
  const [form, setForm] = useState<UserProfileRequest>(emptyForm);
  const [roles, setRoles] = useState<Role[]>([Roles.Employee]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isCreate = user === null;

  useEffect(() => {
    if (user === undefined) return;
    setError(null);
    setRoles([Roles.Employee]);
    setForm(
      user
        ? {
            employeeCode: user.employeeCode,
            email: user.email,
            fullName: user.fullName,
            phoneNumber: user.phoneNumber ?? '',
            branchId: user.branchId,
            departmentId: user.departmentId,
            positionId: user.positionId,
            employmentType: user.employmentType,
            hireDate: user.hireDate,
          }
        : emptyForm,
    );
  }, [user]);

  const set = <K extends keyof UserProfileRequest>(key: K, value: UserProfileRequest[K]) => setForm((f) => ({ ...f, [key]: value }));
  const toId = (value: string) => (value === '' ? null : Number(value));
  const toggleRole = (role: Role) => setRoles((r) => (r.includes(role) ? r.filter((x) => x !== role) : [...r, role]));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (isCreate && roles.length === 0) return setError('Vui lòng chọn ít nhất một vai trò.');
    if (roles.includes(Roles.Manager) && !form.branchId) return setError('Quản lý chi nhánh phải được gán vào một chi nhánh.');

    setSaving(true);
    setError(null);
    try {
      const body = { ...form, phoneNumber: form.phoneNumber || null, hireDate: form.hireDate || null };
      if (isCreate) {
        const { data, message } = await userApi.create({ ...body, roles });
        data.invitationSent ? toast.success(message) : toast.error(message);
      } else {
        toast.success((await userApi.update(user!.id, body)).message);
      }
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
      open={user !== undefined}
      onClose={onClose}
      size="lg"
      title={isCreate ? 'Thêm nhân viên' : `Sửa hồ sơ ${user?.employeeCode ?? ''}`}
      description={isCreate ? 'Hệ thống sẽ sinh mật khẩu tạm và gửi email mời đăng nhập; nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên.' : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Hủy
          </Button>
          <Button type="submit" form="employee-form" loading={saving}>
            {isCreate ? 'Tạo tài khoản' : 'Lưu'}
          </Button>
        </>
      }
    >
      <form id="employee-form" onSubmit={submit} className="grid gap-4 md:grid-cols-2">
        <div className="md:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Mã nhân viên" required>
          <Input value={form.employeeCode} onChange={(e) => set('employeeCode', e.target.value)} maxLength={20} placeholder="VD: EMP010" required autoFocus />
        </Field>
        <Field label="Họ và tên" required>
          <Input value={form.fullName} onChange={(e) => set('fullName', e.target.value)} maxLength={100} required />
        </Field>
        <Field label="Email đăng nhập" required>
          <Input type="email" value={form.email} onChange={(e) => set('email', e.target.value)} maxLength={120} required />
        </Field>
        <Field label="Số điện thoại">
          <Input value={form.phoneNumber ?? ''} onChange={(e) => set('phoneNumber', e.target.value)} maxLength={15} />
        </Field>
        <Field label="Chi nhánh">
          <Select value={form.branchId ?? ''} onChange={(e) => set('branchId', toId(e.target.value))}>
            <option value="">— Không thuộc chi nhánh —</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Phòng ban">
          <Select value={form.departmentId ?? ''} onChange={(e) => set('departmentId', toId(e.target.value))}>
            <option value="">— Chưa chọn —</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Chức vụ">
          <Select value={form.positionId ?? ''} onChange={(e) => set('positionId', toId(e.target.value))}>
            <option value="">— Chưa chọn —</option>
            {positions.map((p) => (
              <option key={p.id} value={p.id}>
                {p.title}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Loại hợp đồng">
          <Select value={form.employmentType} onChange={(e) => set('employmentType', e.target.value as EmploymentType)}>
            <option value="full_time">Toàn thời gian</option>
            <option value="part_time">Bán thời gian</option>
          </Select>
        </Field>
        <Field label="Ngày vào làm">
          <Input type="date" value={form.hireDate ?? ''} onChange={(e) => set('hireDate', e.target.value || null)} />
        </Field>

        {isCreate && (
          <div className="md:col-span-2">
            <p className="mb-2 text-xs font-medium">
              Vai trò <span className="text-danger">*</span>
            </p>
            <div className="grid gap-2 rounded-lg border border-border p-3 sm:grid-cols-2">
              {roleOptions.map((r) => (
                <Checkbox
                  key={r.id}
                  label={roleLabels[r.roleName]}
                  description={r.assignable ? r.description ?? undefined : 'Chỉ Quản trị viên được gán vai trò này'}
                  checked={roles.includes(r.roleName)}
                  disabled={!r.assignable}
                  onChange={() => toggleRole(r.roleName)}
                />
              ))}
            </div>
          </div>
        )}
      </form>
    </Modal>
  );
}
