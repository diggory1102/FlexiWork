import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Camera, KeyRound, Phone } from 'lucide-react';
import { Avatar, PageHeader } from '@/components/ui/Layout';
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
      <HStack hAlign="center" padding={10}>
        <Spinner />
      </HStack>
    );
  }

  return (
    <VStack maxWidth={960}>
      <PageHeader title="Hồ sơ cá nhân" description="Họ tên, phòng ban, chức vụ… do bộ phận Nhân sự quản lý. Bạn có thể cập nhật số điện thoại và ảnh đại diện." />
      <Grid columns={{ minWidth: 260, max: 2 }} gap={4} align="start">
        <Card padding={6}>
          <VStack gap={3} hAlign="center">
            <Avatar name={profile.fullName} url={profile.avatarUrl} size={128} />
            <Heading level={2} justify="center">
              {profile.fullName}
            </Heading>
            <HStack gap={1} wrap="wrap" hAlign="center">
              {profile.roles.map((r) => (
                <Badge key={r} tone="blue">
                  {roleLabels[r]}
                </Badge>
              ))}
              <Badge tone={profile.status === 'active' ? 'green' : 'amber'}>{userStatusLabels[profile.status]}</Badge>
            </HStack>
            <Button label="Đổi ảnh đại diện" icon={<Icon icon={Camera} />} isLoading={uploading} onClick={() => fileInput.current?.click()} />
            <input ref={fileInput} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(e) => uploadAvatar(e.target.files?.[0])} />
            <Text type="supporting">JPG, PNG hoặc WEBP · tối đa 5 MB</Text>
          </VStack>
        </Card>

        <VStack gap={4}>
          <Card padding={5}>
            <MetadataList title={<Heading level={3}>Thông tin công việc</Heading>} label={{ position: 'start', width: 140 }}>
              <MetadataListItem label="Mã nhân viên">{profile.employeeCode}</MetadataListItem>
              <MetadataListItem label="Email">{profile.email}</MetadataListItem>
              <MetadataListItem label="Chi nhánh">{profile.branchName ?? '—'}</MetadataListItem>
              <MetadataListItem label="Phòng ban">{profile.departmentName ?? '—'}</MetadataListItem>
              <MetadataListItem label="Chức vụ">{profile.positionTitle ?? '—'}</MetadataListItem>
              <MetadataListItem label="Loại hợp đồng">{employmentTypeLabels[profile.employmentType]}</MetadataListItem>
              <MetadataListItem label="Ngày vào làm">{formatDate(profile.hireDate)}</MetadataListItem>
            </MetadataList>
          </Card>
          <Card padding={5}>
            <form onSubmit={savePhone} noValidate>
              <HStack gap={3} vAlign="end" wrap="wrap">
                <div className="min-w-56 flex-1">
                  <TextInput label="Số điện thoại" type="tel" placeholder="VD: 0912345678" startIcon={Phone} value={phone} onChange={setPhone} width="100%" />
                </div>
                <Button label="Lưu" type="submit" variant="primary" isLoading={saving} />
              </HStack>
            </form>
          </Card>
          <HStack>
            <Button label="Đổi mật khẩu" variant="ghost" icon={<Icon icon={KeyRound} />} href="/change-password" />
          </HStack>
        </VStack>
      </Grid>
    </VStack>
  );
}
