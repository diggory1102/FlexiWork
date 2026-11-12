import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { CheckboxInput } from '@astryxdesign/core/CheckboxInput';
import { DateInput } from '@astryxdesign/core/DateInput';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { Selector } from '@astryxdesign/core/Selector';
import { VStack } from '@astryxdesign/core/Stack';
import { Heading } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Modal } from '@/components/ui/Modal';
import { FormError } from '@/components/ui/Feedback';
import { userApi } from '@/services/userApi';
import { getErrorMessage } from '@/services/apiClient';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { employmentTypeLabels, roleLabels, toIsoDate } from '@/lib/format';
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

const employmentOptions = (Object.keys(employmentTypeLabels) as EmploymentType[]).map((value) => ({ value, label: employmentTypeLabels[value] }));
const toId = (value: string | null) => (value ? Number(value) : null);

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
  const toggleRole = (role: Role) => setRoles((r) => (r.includes(role) ? r.filter((x) => x !== role) : [...r, role]));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!form.employeeCode.trim() || !form.fullName.trim() || !form.email.trim()) return setError('Vui lòng nhập mã nhân viên, họ tên và email.');
    if (isCreate && roles.length === 0) return setError('Vui lòng chọn ít nhất một vai trò.');
    if (roles.includes(Roles.Manager) && !form.branchId) return setError('Quản lý chi nhánh phải được gán vào một chi nhánh.');

    setSaving(true);
    setError(null);
    try {
      const body = { ...form, phoneNumber: form.phoneNumber || null, hireDate: form.hireDate || null };
      if (isCreate) {
        const { data, message } = await userApi.create({ ...body, roles });
        if (data.invitationSent) toast.success(message);
        else toast.error(message);
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
      purpose="form"
      title={isCreate ? 'Thêm nhân viên' : `Sửa hồ sơ ${user?.employeeCode ?? ''}`}
      description={isCreate ? 'Hệ thống sẽ sinh mật khẩu tạm và gửi email mời đăng nhập; nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên.' : undefined}
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label={isCreate ? 'Tạo tài khoản' : 'Lưu'} type="submit" form="employee-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="employee-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <Grid columns={{ minWidth: 240, max: 2 }} gap={4}>
            <TextInput label="Mã nhân viên" isRequired placeholder="VD: EMP010" value={form.employeeCode} onChange={(v) => set('employeeCode', v)} hasAutoFocus />
            <TextInput label="Họ và tên" isRequired value={form.fullName} onChange={(v) => set('fullName', v)} />
            <TextInput label="Email đăng nhập" isRequired type="email" value={form.email} onChange={(v) => set('email', v)} />
            <TextInput label="Số điện thoại" type="tel" value={form.phoneNumber ?? ''} onChange={(v) => set('phoneNumber', v)} />
            <Selector
              label="Chi nhánh"
              hasSearch
              hasClear
              placeholder="Không thuộc chi nhánh"
              searchPlaceholder="Tìm chi nhánh…"
              options={branches.map((b) => ({ value: String(b.id), label: b.name }))}
              value={form.branchId ? String(form.branchId) : null}
              onChange={(v) => set('branchId', toId(v))}
            />
            <Selector
              label="Phòng ban"
              hasClear
              placeholder="Chưa chọn"
              options={departments.map((d) => ({ value: String(d.id), label: d.name }))}
              value={form.departmentId ? String(form.departmentId) : null}
              onChange={(v) => set('departmentId', toId(v))}
            />
            <Selector
              label="Chức vụ"
              hasClear
              placeholder="Chưa chọn"
              options={positions.map((p) => ({ value: String(p.id), label: p.title }))}
              value={form.positionId ? String(form.positionId) : null}
              onChange={(v) => set('positionId', toId(v))}
            />
            <Selector label="Loại hợp đồng" options={employmentOptions} value={form.employmentType} onChange={(v) => v && set('employmentType', v as EmploymentType)} />
            <DateInput label="Ngày vào làm" hasClear format="date" value={toIsoDate(form.hireDate)} onChange={(v) => set('hireDate', v ?? null)} />

            {isCreate && (
              <GridSpan columns="full">
                <Card variant="muted" padding={4}>
                  <VStack gap={3}>
                    <Heading level={4}>Vai trò *</Heading>
                    <Grid columns={{ minWidth: 220, max: 2 }} gap={3}>
                      {roleOptions.map((r) => (
                        <CheckboxInput
                          key={r.id}
                          label={roleLabels[r.roleName]}
                          description={r.assignable ? (r.description ?? undefined) : 'Chỉ Quản trị viên được gán vai trò này'}
                          value={roles.includes(r.roleName)}
                          isDisabled={!r.assignable}
                          onChange={() => toggleRole(r.roleName)}
                        />
                      ))}
                    </Grid>
                  </VStack>
                </Card>
              </GridSpan>
            )}
          </Grid>
        </VStack>
      </form>
    </Modal>
  );
}
