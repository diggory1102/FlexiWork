import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { CheckboxInput } from '@astryxdesign/core/CheckboxInput';
import { SegmentedControl, SegmentedControlItem } from '@astryxdesign/core/SegmentedControl';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Thumbnail } from '@astryxdesign/core/Thumbnail';
import { Lock } from 'lucide-react';
import { Modal, ConfirmDialog } from '@/components/ui/Modal';
import { Badge, ErrorList, Notice, Spinner, type ErrorDetail } from '@/components/ui/Feedback';
import { userApi } from '@/services/userApi';
import { getErrorDetail } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatDateTime } from '@/lib/format';
import { validateNewPassword } from '@/lib/validation';
import type { FaceStatus } from '@/types/scheduling';
import type { User } from '@/types/user';

type PasswordMode = 'email' | 'manual';

/**
 * Mật khẩu của nhân viên (Admin/HR): gửi mật khẩu tạm qua email, hoặc tự đặt mật khẩu khi email mời không tới được.
 * Cả hai cách đều đăng xuất nhân viên khỏi mọi thiết bị.
 */
export function EmployeePasswordModal({ user, onClose, onSaved }: { user: User | null; onClose: () => void; onSaved: () => void }) {
  const [mode, setMode] = useState<PasswordMode>('email');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [mustChange, setMustChange] = useState(true);
  const [error, setError] = useState<ErrorDetail | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!user) return;
    setMode('email');
    setPassword('');
    setConfirm('');
    setMustChange(true);
    setError(null);
  }, [user]);

  if (!user) return null;

  const submit = async () => {
    if (mode === 'manual') {
      const invalid = validateNewPassword(password, confirm);
      if (invalid) return setError({ message: invalid, errors: [] });
    }
    setSaving(true);
    setError(null);
    try {
      const { message } = mode === 'email' ? await userApi.reissuePassword(user.id) : await userApi.setPassword(user.id, password, mustChange);
      toast.success(message);
      onSaved();
      onClose();
    } catch (err) {
      setError(getErrorDetail(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open
      onClose={onClose}
      size="md"
      purpose="form"
      title={`Mật khẩu của ${user.fullName}`}
      description="Mật khẩu cũ sẽ không dùng được nữa và nhân viên bị đăng xuất khỏi mọi thiết bị."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label={mode === 'email' ? 'Gửi mật khẩu tạm' : 'Đặt mật khẩu'} variant="primary" isLoading={saving} onClick={submit} />
        </>
      }
    >
      <VStack gap={4}>
        <SegmentedControl label="Cách cấp mật khẩu" value={mode} onChange={(v) => setMode(v as PasswordMode)} layout="fill">
          <SegmentedControlItem value="email" label="Gửi qua email" />
          <SegmentedControlItem value="manual" label="Tự đặt mật khẩu" />
        </SegmentedControl>

        <ErrorList error={error} />

        {mode === 'email' ? (
          <Text as="p">
            Hệ thống sinh mật khẩu tạm ngẫu nhiên và gửi tới <b>{user.email}</b>. Nhân viên phải đổi mật khẩu ở lần đăng nhập tới.
          </Text>
        ) : (
          <>
            <Notice
              status="warning"
              title="Dùng khi email mời không tới được"
              description="Báo mật khẩu trực tiếp cho nhân viên (gặp mặt hoặc nhắn riêng). Nên để nhân viên đổi mật khẩu ở lần đăng nhập tới."
            />
            <TextInput
              label="Mật khẩu mới"
              type="password"
              autoComplete="new-password"
              description="Tối thiểu 8 ký tự, gồm chữ cái và chữ số, không có khoảng trắng."
              startIcon={Lock}
              value={password}
              onChange={setPassword}
              hasAutoFocus
            />
            <TextInput label="Nhập lại mật khẩu" type="password" autoComplete="new-password" startIcon={Lock} value={confirm} onChange={setConfirm} />
            <CheckboxInput label="Bắt nhân viên đổi mật khẩu ở lần đăng nhập tới" value={mustChange} onChange={() => setMustChange((v) => !v)} />
          </>
        )}
      </VStack>
    </Modal>
  );
}

/**
 * Trạng thái Face ID của nhân viên trong hồ sơ: ảnh mẫu, ngày đăng ký và nút đặt lại để nhân viên đăng ký lại
 * (Quản lý: nhân viên chi nhánh mình — backend kiểm tra phạm vi).
 */
export function EmployeeFaceSection({ user, canReset }: { user: User; canReset: boolean }) {
  const [status, setStatus] = useState<FaceStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setStatus(null);
    setError(null);
    userApi
      .faceStatus(user.id)
      .then(({ data }) => setStatus(data))
      .catch((err) => setError(getErrorDetail(err).message));
  }, [user.id]);

  const reset = async () => {
    setBusy(true);
    try {
      const { message } = await userApi.resetFace(user.id);
      toast.success(message);
      setStatus({ registered: false, sampleImageUrl: null, modelName: null, registeredAt: null, updatedAt: null });
      setConfirming(false);
    } catch (err) {
      toast.error(getErrorDetail(err).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <VStack gap={3}>
      <Heading level={4}>Khuôn mặt (Face ID)</Heading>
      {error ? (
        <Text type="supporting">{error}</Text>
      ) : !status ? (
        <HStack>
          <Spinner size="sm" />
        </HStack>
      ) : status.registered ? (
        <HStack gap={3} vAlign="center" wrap="wrap">
          {status.sampleImageUrl && <Thumbnail src={status.sampleImageUrl} alt={`Ảnh khuôn mặt của ${user.fullName}`} style={{ width: 64, height: 64 }} />}
          <VStack gap={1}>
            <HStack gap={1.5}>
              <Badge tone="green">Đã đăng ký</Badge>
            </HStack>
            <Text type="supporting">Ngày đăng ký: {formatDateTime(status.registeredAt)}</Text>
          </VStack>
          {canReset && <Button label="Đặt lại Face ID" variant="secondary" size="sm" onClick={() => setConfirming(true)} />}
        </HStack>
      ) : (
        <HStack gap={2} vAlign="center" wrap="wrap">
          <Badge tone="amber">Chưa đăng ký</Badge>
          <Text type="supporting">Nhân viên tự chụp ảnh đăng ký trên ứng dụng di động (Hồ sơ → Khuôn mặt).</Text>
        </HStack>
      )}

      <ConfirmDialog
        open={confirming}
        title="Đặt lại Face ID"
        message={
          <>
            Xóa dữ liệu khuôn mặt của <b>{user.fullName}</b>? Nhân viên sẽ không chấm công được cho tới khi đăng ký lại trên ứng dụng.
          </>
        }
        confirmText="Đặt lại"
        loading={busy}
        onConfirm={reset}
        onClose={() => setConfirming(false)}
      />
    </VStack>
  );
}
