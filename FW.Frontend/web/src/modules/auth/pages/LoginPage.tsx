import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { LogIn } from 'lucide-react';
import { AuthLayout } from '@/layouts/AuthLayout';
import { Button } from '@/components/ui/Button';
import { Field, FormError, Input } from '@/components/ui/Form';
import { PasswordInput } from '../components/PasswordInput';
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
      <form onSubmit={submit} className="space-y-4">
        {notice && <div className="rounded-md border border-success/30 bg-success/5 px-3 py-2 text-sm text-success">{notice}</div>}
        <FormError message={error} />
        <Field label="Email" required>
          <Input type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="ten@congty.vn" required autoFocus />
        </Field>
        <Field label="Mật khẩu" required>
          <PasswordInput autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </Field>
        <div className="flex justify-end">
          <Link to="/forgot-password" className="text-xs font-medium text-primary hover:underline">
            Quên mật khẩu?
          </Link>
        </div>
        <Button type="submit" className="w-full" loading={loading} icon={<LogIn className="h-4 w-4" />}>
          Đăng nhập
        </Button>
      </form>
    </AuthLayout>
  );
}
