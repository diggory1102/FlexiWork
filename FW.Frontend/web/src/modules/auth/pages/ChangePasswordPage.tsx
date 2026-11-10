import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';
import { AuthLayout } from '@/layouts/AuthLayout';
import { Button } from '@/components/ui/Button';
import { Field, FormError } from '@/components/ui/Form';
import { PageHeader } from '@/components/ui/Layout';
import { PasswordInput, validatePassword } from '../components/PasswordInput';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { toast } from '@/stores/toastStore';

/**
 * Đổi mật khẩu. Khi tài khoản đang dùng mật khẩu tạm (mustChangePassword) trang hiển thị dạng toàn màn hình
 * và người dùng không vào được các trang khác cho tới khi đổi xong.
 */
export function ChangePasswordPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const updateUser = useAuthStore((s) => s.updateUser);
  const logout = useAuthStore((s) => s.logout);
  const forced = !!user?.mustChangePassword;

  const [current, setCurrent] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const clientError = validatePassword(password) ?? (password !== confirm ? 'Mật khẩu xác nhận không khớp.' : null);
    if (clientError) return setError(clientError);

    setLoading(true);
    setError(null);
    try {
      const { message } = await authApi.changePassword(current, password);
      updateUser({ mustChangePassword: false });
      toast.success(message);
      navigate('/', { replace: true });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  const form = (
    <form onSubmit={submit} className="space-y-4">
      <FormError message={error} />
      <Field label={forced ? 'Mật khẩu tạm (trong email)' : 'Mật khẩu hiện tại'} required>
        <PasswordInput autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} required autoFocus />
      </Field>
      <Field label="Mật khẩu mới" required hint="Tối thiểu 8 ký tự, gồm chữ cái và chữ số, không có khoảng trắng.">
        <PasswordInput autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
      </Field>
      <Field label="Nhập lại mật khẩu mới" required>
        <PasswordInput autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
      </Field>
      <Button type="submit" className="w-full" loading={loading}>
        Đổi mật khẩu
      </Button>
    </form>
  );

  if (forced) {
    return (
      <AuthLayout title="Đặt mật khẩu mới" subtitle={`Xin chào ${user?.fullName}, bạn đang dùng mật khẩu tạm do hệ thống cấp.`}>
        <div className="mb-4 flex gap-2 rounded-md border border-warning/30 bg-warning/5 px-3 py-2 text-sm text-warning">
          <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
          Vì lý do bảo mật, bạn cần đổi mật khẩu trước khi tiếp tục sử dụng hệ thống.
        </div>
        {form}
        <button
          type="button"
          onClick={() => {
            logout();
            navigate('/login', { replace: true });
          }}
          className="mt-6 text-xs text-muted hover:text-foreground"
        >
          Đăng xuất
        </button>
      </AuthLayout>
    );
  }

  return (
    <div className="max-w-md">
      <PageHeader title="Đổi mật khẩu" description="Mật khẩu mới có hiệu lực cho cả Web Admin và ứng dụng di động." />
      <div className="card p-5">{form}</div>
    </div>
  );
}
