import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { VStack } from '@astryxdesign/core/Stack';
import { TextInput } from '@astryxdesign/core/TextInput';
import { KeyRound, Lock } from 'lucide-react';
import { AuthLayout } from '@/layouts/AuthLayout';
import { FormError, Notice } from '@/components/ui/Feedback';
import { PageHeader } from '@/components/ui/Layout';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';
import { validateNewPassword } from '@/lib/validation';

/**
 * Đổi mật khẩu. Khi tài khoản đang dùng mật khẩu tạm (mustChangePassword) trang hiển thị dạng toàn màn hình
 * và người dùng không vào được các trang khác cho tới khi đổi xong.
 */
export function ChangePasswordPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const setSession = useAuthStore((s) => s.setSession);
  const logout = useAuthStore((s) => s.logout);
  const forced = !!user?.mustChangePassword;

  const [current, setCurrent] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!current) return setError(forced ? 'Vui lòng nhập mật khẩu tạm.' : 'Vui lòng nhập mật khẩu hiện tại.');
    const clientError = validateNewPassword(password, confirm);
    if (clientError) return setError(clientError);

    setLoading(true);
    setError(null);
    try {
      // Máy chủ thu hồi mọi token cũ (các thiết bị khác bị đăng xuất) và cấp phiên mới cho thiết bị này
      const { data, message } = await authApi.changePassword(current, password);
      setSession(data);
      toast.success(message);
      navigate('/', { replace: true });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  const form = (
    <form onSubmit={submit} noValidate>
      <VStack gap={4}>
        <FormError message={error} />
        <TextInput
          label={forced ? 'Mật khẩu tạm (trong email)' : 'Mật khẩu hiện tại'}
          type="password"
          autoComplete="current-password"
          startIcon={KeyRound}
          value={current}
          onChange={setCurrent}
          hasAutoFocus
        />
        <TextInput
          label="Mật khẩu mới"
          type="password"
          autoComplete="new-password"
          description="Tối thiểu 8 ký tự, gồm chữ cái và chữ số, không có khoảng trắng."
          startIcon={Lock}
          value={password}
          onChange={setPassword}
        />
        <TextInput label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" startIcon={Lock} value={confirm} onChange={setConfirm} />
        <Button type="submit" label="Đổi mật khẩu" variant="primary" width="100%" isLoading={loading} />
      </VStack>
    </form>
  );

  if (forced) {
    return (
      <AuthLayout title="Đặt mật khẩu mới" subtitle={`Xin chào ${user?.fullName}, bạn đang dùng mật khẩu tạm do hệ thống cấp.`}>
        <Notice status="warning" title="Bạn cần đổi mật khẩu trước khi tiếp tục sử dụng hệ thống." />
        {form}
        <Button
          label="Đăng xuất"
          variant="ghost"
          onClick={() => {
            logout();
            navigate('/login', { replace: true });
          }}
        />
      </AuthLayout>
    );
  }

  return (
    <VStack maxWidth={480}>
      <PageHeader
        title="Đổi mật khẩu"
        description="Mật khẩu mới có hiệu lực cho cả Web Admin và ứng dụng di động. Sau khi đổi, các thiết bị khác sẽ bị đăng xuất và bạn nhận email thông báo."
      />
      <Card padding={6}>{form}</Card>
    </VStack>
  );
}
