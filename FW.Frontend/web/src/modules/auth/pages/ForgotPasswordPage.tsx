import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Mail } from 'lucide-react';
import { AuthLayout } from '@/layouts/AuthLayout';
import { Button } from '@/components/ui/Button';
import { Field, FormError, Input } from '@/components/ui/Form';
import { PasswordInput, validatePassword } from '../components/PasswordInput';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';

/** Quên mật khẩu 2 bước: (1) nhập email nhận OTP → (2) nhập OTP + mật khẩu mới. */
export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState<1 | 2>(1);
  const [email, setEmail] = useState('');
  const [otp, setOtp] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [info, setInfo] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const requestOtp = async (e?: FormEvent) => {
    e?.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const { message } = await authApi.forgotPassword(email.trim());
      setInfo(message);
      setStep(2);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  const reset = async (e: FormEvent) => {
    e.preventDefault();
    const passwordError = validatePassword(password) ?? (password !== confirm ? 'Mật khẩu xác nhận không khớp.' : null);
    if (passwordError) return setError(passwordError);

    setLoading(true);
    setError(null);
    try {
      const { message } = await authApi.resetPassword(email.trim(), otp.trim(), password);
      navigate('/login', { replace: true, state: { notice: message } });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Quên mật khẩu"
      subtitle={step === 1 ? 'Nhập email tài khoản, hệ thống sẽ gửi mã OTP gồm 6 chữ số.' : `Nhập mã OTP đã gửi tới ${email} và đặt mật khẩu mới.`}
    >
      {step === 1 ? (
        <form onSubmit={requestOtp} className="space-y-4">
          <FormError message={error} />
          <Field label="Email" required>
            <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="ten@congty.vn" required autoFocus />
          </Field>
          <Button type="submit" className="w-full" loading={loading} icon={<Mail className="h-4 w-4" />}>
            Gửi mã OTP
          </Button>
        </form>
      ) : (
        <form onSubmit={reset} className="space-y-4">
          {info && <div className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-sm text-primary">{info}</div>}
          <FormError message={error} />
          <Field label="Mã OTP" required>
            <Input
              inputMode="numeric"
              maxLength={6}
              value={otp}
              onChange={(e) => setOtp(e.target.value.replace(/\D/g, ''))}
              placeholder="6 chữ số"
              className="tracking-[0.4em]"
              required
              autoFocus
            />
          </Field>
          <Field label="Mật khẩu mới" required hint="Tối thiểu 8 ký tự, gồm chữ cái và chữ số.">
            <PasswordInput autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </Field>
          <Field label="Nhập lại mật khẩu mới" required>
            <PasswordInput autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
          </Field>
          <Button type="submit" className="w-full" loading={loading}>
            Đặt lại mật khẩu
          </Button>
          <button type="button" onClick={() => requestOtp()} disabled={loading} className="w-full text-xs text-primary hover:underline disabled:opacity-50">
            Chưa nhận được mã? Gửi lại (sau 60 giây)
          </button>
        </form>
      )}
      <Link to="/login" className="mt-6 inline-flex items-center gap-1 text-xs text-muted hover:text-foreground">
        <ArrowLeft className="h-3.5 w-3.5" /> Quay lại đăng nhập
      </Link>
    </AuthLayout>
  );
}
