import { useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, View, type TextInputInstance } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { Screen } from '@/components/ui/Screen';
import { TextField } from '@/components/ui/TextField';
import { Button } from '@/components/ui/Button';
import { Notice } from '@/components/ui/Feedback';
import { Logo } from '@/components/common/Logo';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { isEmail } from '@/lib/validation';
import { colors } from '@/theme';

/**
 * Đăng nhập bằng email + mật khẩu. Đăng nhập thành công → bộ định tuyến tự chuyển
 * tới các tab, hoặc tới màn hình đổi mật khẩu nếu đang dùng mật khẩu tạm.
 */
export function LoginScreen() {
  const { notice } = useLocalSearchParams<{ notice?: string }>();
  const setSession = useAuthStore((s) => s.setSession);
  const signOutNotice = useAuthStore((s) => s.signOutNotice);
  const passwordRef = useRef<TextInputInstance>(null);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async () => {
    if (!isEmail(email)) return setError('Vui lòng nhập email hợp lệ.');
    if (!password) return setError('Vui lòng nhập mật khẩu.');

    setLoading(true);
    setError(null);
    try {
      const { data } = await authApi.login(email.trim(), password);
      setSession(data);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Screen background={colors.surface} contentStyle={styles.container}>
      <View style={styles.brand}>
        <Logo />
      </View>

      <Text style={styles.title}>Đăng nhập</Text>
      <Text style={styles.subtitle}>Dùng tài khoản do bộ phận Nhân sự cấp để chấm công, xem lịch ca và gửi đơn từ.</Text>

      <View style={styles.form}>
        <Notice tone="success" message={notice} />
        <Notice tone="warning" message={signOutNotice} />
        <Notice tone="danger" message={error} />
        <TextField
          testID="login-email"
          label="Email"
          icon="mail-outline"
          placeholder="ten@congty.vn"
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="email-address"
          autoComplete="email"
          textContentType="username"
          returnKeyType="next"
          onSubmitEditing={() => passwordRef.current?.focus()}
        />
        <TextField
          ref={passwordRef}
          testID="login-password"
          label="Mật khẩu"
          icon="lock-closed-outline"
          placeholder="Nhập mật khẩu"
          password
          value={password}
          onChangeText={setPassword}
          autoComplete="current-password"
          textContentType="password"
          returnKeyType="go"
          onSubmitEditing={submit}
        />
        <Pressable onPress={() => router.push('/forgot-password')} style={styles.forgot} hitSlop={8}>
          <Text style={styles.link}>Quên mật khẩu?</Text>
        </Pressable>
        <Button testID="login-submit" title="Đăng nhập" onPress={submit} loading={loading} />
      </View>

      <Text style={styles.footer}>Chưa có tài khoản? Liên hệ bộ phận Nhân sự hoặc Quản lý chi nhánh.</Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: { justifyContent: 'center', paddingHorizontal: 24 },
  brand: { marginBottom: 40 },
  title: { fontSize: 26, fontWeight: '800', color: colors.foreground },
  subtitle: { marginTop: 6, fontSize: 14, lineHeight: 21, color: colors.muted },
  form: { marginTop: 28, gap: 16 },
  forgot: { alignSelf: 'flex-end', marginTop: -6 },
  link: { fontSize: 14, fontWeight: '600', color: colors.primary },
  footer: { marginTop: 32, fontSize: 12, lineHeight: 18, color: colors.muted, textAlign: 'center' },
});
