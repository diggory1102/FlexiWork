import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Camera, KeyRound } from 'lucide-react';
import { PageHeader, Avatar } from '@/components/ui/Layout';
import { Button } from '@/components/ui/Button';
import { Field, Input } from '@/components/ui/Form';
import { Badge, Spinner } from '@/components/ui/Feedback';
import { profileApi } from '@/services/userApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';
import { employmentTypeLabels, formatDate, roleLabels, userStatusLabels } from '@/lib/format';
import type { User } from '@/types/user';

const MAX_AVATAR_BYTES = 5 * 1024 * 1024;

/** Hồ sơ cá nhân: xem thông tin, sửa số điện thoại, đổi ảnh đại diện. */
export function ProfilePage() {
  const updateAuthUser = useAuthStore((s) => s.updateUser);
  const [profile, setProfile] = useState<User | null>(null);
  const [phone, setPhone] = useState('');
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    profileApi
      .get()
      .then(({ data }) => {
        setProfile(data);
        setPhone(data.phoneNumber ?? '');
      })
      .catch((err) => toast.error(getErrorMessage(err)));
  }, []);

  const savePhone = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const { data, message } = await profileApi.update(phone.trim() || null);
      setProfile(data);
      toast.success(message);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const uploadAvatar = async (file: File | undefined) => {
    if (!file) return;
    if (file.size > MAX_AVATAR_BYTES) return toast.error('Ảnh đại diện tối đa 5 MB.');
    setUploading(true);
    try {
      const { data, message } = await profileApi.uploadAvatar(file);
      setProfile(data);
      updateAuthUser({ avatarUrl: data.avatarUrl });
      toast.success(message);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setUploading(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  if (!profile) {
    return (
      <div className="flex justify-center py-20">
        <Spinner />
      </div>
    );
  }

  const info: [string, string][] = [
    ['Mã nhân viên', profile.employeeCode],
    ['Email', profile.email],
    ['Chi nhánh', profile.branchName ?? '—'],
    ['Phòng ban', profile.departmentName ?? '—'],
    ['Chức vụ', profile.positionTitle ?? '—'],
    ['Loại hợp đồng', employmentTypeLabels[profile.employmentType]],
    ['Ngày vào làm', formatDate(profile.hireDate)],
  ];

  return (
    <div className="max-w-3xl">
      <PageHeader title="Hồ sơ cá nhân" description="Họ tên, phòng ban, chức vụ… do bộ phận Nhân sự quản lý. Bạn có thể cập nhật số điện thoại và ảnh đại diện." />
      <div className="grid gap-4 md:grid-cols-[240px_1fr]">
        <div className="card flex flex-col items-center p-6 text-center">
          <div className="relative">
            <Avatar name={profile.fullName} url={profile.avatarUrl} size={112} />
            <button
              type="button"
              onClick={() => fileInput.current?.click()}
              disabled={uploading}
              className="absolute bottom-0 right-0 flex h-9 w-9 items-center justify-center rounded-full border-2 border-surface bg-primary text-white shadow hover:bg-primary-dark disabled:opacity-60"
              aria-label="Đổi ảnh đại diện"
            >
              {uploading ? <Spinner className="h-4 w-4 text-white" /> : <Camera className="h-4 w-4" />}
            </button>
            <input ref={fileInput} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(e) => uploadAvatar(e.target.files?.[0])} />
          </div>
          <p className="mt-4 text-base font-semibold">{profile.fullName}</p>
          <div className="mt-2 flex flex-wrap justify-center gap-1">
            {profile.roles.map((r) => (
              <Badge key={r} tone="blue">
                {roleLabels[r]}
              </Badge>
            ))}
          </div>
          <p className="mt-2 text-xs text-muted">{userStatusLabels[profile.status]}</p>
          <p className="mt-3 text-[11px] text-muted">JPG, PNG hoặc WEBP · tối đa 5 MB</p>
        </div>

        <div className="space-y-4">
          <div className="card p-5">
            <dl className="grid grid-cols-[140px_1fr] gap-x-4 gap-y-2.5">
              {info.map(([label, value]) => (
                <div key={label} className="contents">
                  <dt className="text-muted">{label}</dt>
                  <dd>{value}</dd>
                </div>
              ))}
            </dl>
          </div>
          <form onSubmit={savePhone} className="card flex flex-wrap items-end gap-3 p-5">
            <Field label="Số điện thoại" className="flex-1">
              <Input value={phone} onChange={(e) => setPhone(e.target.value)} maxLength={15} placeholder="VD: 0912345678" />
            </Field>
            <Button type="submit" loading={saving}>
              Lưu
            </Button>
          </form>
          <Link to="/change-password" className="inline-flex items-center gap-2 text-sm font-medium text-primary hover:underline">
            <KeyRound className="h-4 w-4" /> Đổi mật khẩu
          </Link>
        </div>
      </div>
    </div>
  );
}
