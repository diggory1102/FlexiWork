import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { Link } from '@astryxdesign/core/Link';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Lock, LogIn, Mail } from 'lucide-react';
import { AuthLayout } from '@/layouts/AuthLayout';
import { FormError, Notice } from '@/components/ui/Feedback';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuthStore((s) => s.setSession);
  const isSessionValid = useAuthStore((s) => s.isSessionValid);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (isSessionValid()) return <Navigate to="/" replace />;

  const notice = (location.state as { notice?: string } | null)?.notice;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password) return setError('Vui lòng nhập email và mật khẩu.');
    setLoading(true);
    setError(null);
    try {
      const { data } = await authApi.login(email.trim(), password);
      setSession(data);
      navigate(data.user.mustChangePassword ? '/change-password' : '/', { replace: true });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout title="Đăng nhập" subtitle="Trang quản trị dành cho Quản trị viên, Nhân sự và Quản lý chi nhánh.">
      <form onSubmit={submit} noValidate>
        <VStack gap={4}>
          <Notice status="success" title={notice} />
          <FormError message={error} />
          <TextInput
            label="Email"
            type="email"
            autoComplete="username"
            placeholder="ten@congty.vn"
            startIcon={Mail}
            value={email}
            onChange={setEmail}
            size="lg"
            hasAutoFocus
          />
          <VStack gap={1.5}>
            <TextInput
              label="Mật khẩu"
              type="password"
              autoComplete="current-password"
              placeholder="Nhập mật khẩu"
              startIcon={Lock}
              value={password}
              onChange={setPassword}
              size="lg"
            />
            <HStack hAlign="end">
              <Link href="/forgot-password" type="supporting">
                Quên mật khẩu?
              </Link>
            </HStack>
          </VStack>
          <Button type="submit" label="Đăng nhập" variant="primary" size="lg" width="100%" isLoading={loading} icon={<Icon icon={LogIn} />} />
        </VStack>
      </form>
    </AuthLayout>
  );
}
