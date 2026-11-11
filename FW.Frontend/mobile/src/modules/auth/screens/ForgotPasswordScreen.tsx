import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Screen } from '@/components/ui/Screen';
import { TextField } from '@/components/ui/TextField';
import { Button } from '@/components/ui/Button';
import { Notice } from '@/components/ui/Feedback';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';
import { isEmail, validateNewPassword } from '@/lib/validation';
import { colors } from '@/theme';

/** Thời gian chờ trước khi được gửi lại OTP — khớp OtpResendCooldown ở backend. */
const RESEND_SECONDS = 60;

/** Quên mật khẩu 2 bước: (1) nhập email nhận OTP → (2) nhập OTP + mật khẩu mới. */
export function ForgotPasswordScreen() {
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

  const requestOtp = async () => {
    if (!isEmail(email)) return setError('Vui lòng nhập email hợp lệ.');
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

  const reset = async () => {
    if (otp.length !== 6) return setError('Mã OTP gồm 6 chữ số.');
    const passwordError = validateNewPassword(password, confirm);
    if (passwordError) return setError(passwordError);

    setLoading(true);
    setError(null);
    try {
      const { message } = await authApi.resetPassword(email.trim(), otp, password);
      // Quay về đúng màn hình đăng nhập đang có trong stack (không tạo thêm bản mới) kèm thông báo
      router.dismissTo({ pathname: '/login', params: { notice: message } });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Screen background={colors.surface} contentStyle={styles.container}>
      <Pressable onPress={() => (step === 2 ? setStep(1) : router.back())} style={styles.back} hitSlop={10} accessibilityLabel="Quay lại">
        <Ionicons name="arrow-back" size={24} color={colors.foreground} />
      </Pressable>

      <Text style={styles.title}>Quên mật khẩu</Text>
      <Text style={styles.subtitle}>
        {step === 1
          ? 'Nhập email tài khoản, hệ thống sẽ gửi mã OTP gồm 6 chữ số (hiệu lực 5 phút).'
          : `Nhập mã OTP đã gửi tới ${email.trim()} và đặt mật khẩu mới.`}
      </Text>

      <View style={styles.form}>
        <Notice tone="info" message={step === 2 ? info : null} />
        <Notice tone="danger" message={error} />

        {step === 1 ? (
          <>
            <TextField
              label="Email"
              icon="mail-outline"
              placeholder="ten@congty.vn"
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              autoCorrect={false}
              keyboardType="email-address"
              autoComplete="email"
              returnKeyType="send"
              onSubmitEditing={requestOtp}
            />
            <Button title="Gửi mã OTP" onPress={requestOtp} loading={loading} />
          </>
        ) : (
          <>
            <TextField
              label="Mã OTP"
              icon="keypad-outline"
              placeholder="6 chữ số"
              value={otp}
              onChangeText={(v) => setOtp(v.replace(/\D/g, '').slice(0, 6))}
              keyboardType="number-pad"
              autoComplete="one-time-code"
              textContentType="oneTimeCode"
              maxLength={6}
              style={styles.otp}
            />
            <TextField
              label="Mật khẩu mới"
              icon="lock-closed-outline"
              hint="Tối thiểu 8 ký tự, gồm chữ cái và chữ số."
              password
              value={password}
              onChangeText={setPassword}
              autoComplete="new-password"
              textContentType="newPassword"
            />
            <TextField
              label="Nhập lại mật khẩu mới"
              icon="lock-closed-outline"
              password
              value={confirm}
              onChangeText={setConfirm}
              autoComplete="new-password"
              textContentType="newPassword"
              returnKeyType="done"
              onSubmitEditing={reset}
            />
            <Button title="Đặt lại mật khẩu" onPress={reset} loading={loading} />
            <Pressable onPress={requestOtp} disabled={loading || cooldown > 0} style={styles.resend} hitSlop={8}>
              <Text style={[styles.link, cooldown > 0 && styles.linkDisabled]}>
                {cooldown > 0 ? `Gửi lại mã sau ${cooldown} giây` : 'Chưa nhận được mã? Gửi lại'}
              </Text>
            </Pressable>
          </>
        )}
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: { paddingHorizontal: 24 },
  back: { width: 40, height: 40, justifyContent: 'center', marginBottom: 16 },
  title: { fontSize: 26, fontWeight: '800', color: colors.foreground },
  subtitle: { marginTop: 6, fontSize: 14, lineHeight: 21, color: colors.muted },
  form: { marginTop: 28, gap: 16 },
  otp: { letterSpacing: 8, fontSize: 18, fontWeight: '600' },
  resend: { alignSelf: 'center', paddingVertical: 4 },
  link: { fontSize: 14, fontWeight: '600', color: colors.primary },
  linkDisabled: { color: colors.muted },
});
