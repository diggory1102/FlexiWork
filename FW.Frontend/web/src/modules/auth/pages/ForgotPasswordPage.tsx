import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { Link } from '@astryxdesign/core/Link';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { TextInput } from '@astryxdesign/core/TextInput';
import { ArrowLeft, KeyRound, Lock, Mail } from 'lucide-react';
import { AuthLayout } from '@/layouts/AuthLayout';
import { FormError, Notice } from '@/components/ui/Feedback';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';
import { validateNewPassword } from '@/lib/validation';

/** Thời gian chờ trước khi gửi lại OTP — khớp OtpResendCooldown ở backend. */
const RESEND_SECONDS = 60;

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
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (cooldown <= 0) return;
    const timer = setTimeout(() => setCooldown((s) => s - 1), 1000);
    return () => clearTimeout(timer);
  }, [cooldown]);

  const requestOtp = async (e?: FormEvent) => {
    e?.preventDefault();
    if (!email.trim()) return setError('Vui lòng nhập email.');
    setLoading(true);
    setError(null);
    try {
      const { message } = await authApi.forgotPassword(email.trim());
      setInfo(message);
      setStep(2);
      setCooldown(RESEND_SECONDS);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  const reset = async (e: FormEvent) => {
    e.preventDefault();
    if (otp.length !== 6) return setError('Mã OTP gồm 6 chữ số.');
    const passwordError = validateNewPassword(password, confirm);
    if (passwordError) return setError(passwordError);

    setLoading(true);
    setError(null);
    try {
      const { message } = await authApi.resetPassword(email.trim(), otp, password);
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
      subtitle={step === 1 ? 'Nhập email tài khoản, hệ thống sẽ gửi mã OTP gồm 6 chữ số (hiệu lực 5 phút).' : `Nhập mã OTP đã gửi tới ${email.trim()} và đặt mật khẩu mới.`}
    >
      {step === 1 ? (
        <form onSubmit={requestOtp} noValidate>
          <VStack gap={4}>
            <FormError message={error} />
            <TextInput label="Email" type="email" autoComplete="email" placeholder="ten@congty.vn" startIcon={Mail} value={email} onChange={setEmail} size="lg" hasAutoFocus />
            <Button type="submit" label="Gửi mã OTP" variant="primary" size="lg" width="100%" isLoading={loading} icon={<Icon icon={Mail} />} />
          </VStack>
        </form>
      ) : (
        <form onSubmit={reset} noValidate>
          <VStack gap={4}>
            <Notice status="info" title={info} />
            <FormError message={error} />
            <TextInput
              label="Mã OTP"
              placeholder="6 chữ số"
              inputMode="numeric"
              autoComplete="one-time-code"
              startIcon={KeyRound}
              value={otp}
              onChange={(v) => setOtp(v.replace(/\D/g, '').slice(0, 6))}
              size="lg"
              hasAutoFocus
            />
            <TextInput
              label="Mật khẩu mới"
              type="password"
              autoComplete="new-password"
              description="Tối thiểu 8 ký tự, gồm chữ cái và chữ số."
              startIcon={Lock}
              value={password}
              onChange={setPassword}
              size="lg"
            />
            <TextInput label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" startIcon={Lock} value={confirm} onChange={setConfirm} size="lg" />
            <Button type="submit" label="Đặt lại mật khẩu" variant="primary" size="lg" width="100%" isLoading={loading} />
            <Button
              label={cooldown > 0 ? `Gửi lại mã sau ${cooldown} giây` : 'Chưa nhận được mã? Gửi lại'}
              variant="ghost"
              width="100%"
              isDisabled={loading || cooldown > 0}
              onClick={() => requestOtp()}
            />
          </VStack>
        </form>
      )}
      <HStack>
        <Link href="/login" type="supporting">
          <HStack gap={1} vAlign="center">
            <Icon icon={ArrowLeft} size="sm" /> Quay lại đăng nhập
          </HStack>
        </Link>
      </HStack>
    </AuthLayout>
  );
}
