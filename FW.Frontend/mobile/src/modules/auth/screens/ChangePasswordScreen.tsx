import { useEffect, useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { Screen } from '@/components/ui/Screen';
import { TextField } from '@/components/ui/TextField';
import { Button } from '@/components/ui/Button';
import { Notice } from '@/components/ui/Feedback';
import { authApi } from '@/services/authApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { validateNewPassword } from '@/lib/validation';
import { notify } from '@/lib/dialog';
import { colors } from '@/theme';

/**
 * Đổi mật khẩu. Khi tài khoản đang dùng mật khẩu tạm (mustChangePassword), đây là màn hình
 * bắt buộc: không có tab, không quay lại được — chỉ có thể đổi mật khẩu hoặc đăng xuất.
 */
export function ChangePasswordScreen() {
  const forced = useAuthStore((s) => s.user?.mustChangePassword ?? false);
  const fullName = useAuthStore((s) => s.user?.fullName ?? '');
  const updateUser = useAuthStore((s) => s.updateUser);
  const logout = useAuthStore((s) => s.logout);

  const [current, setCurrent] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Vừa đổi xong mật khẩu tạm: các tab đã được mở khóa (layout gốc render trước) → về trang chủ.
  const wasForced = useRef(forced);
  useEffect(() => {
    if (wasForced.current && !forced) router.replace('/');
    wasForced.current = forced;
  }, [forced]);

  const submit = async () => {
    if (!current) return setError(forced ? 'Vui lòng nhập mật khẩu tạm.' : 'Vui lòng nhập mật khẩu hiện tại.');
    const passwordError = validateNewPassword(password, confirm) ?? (password === current ? 'Mật khẩu mới phải khác mật khẩu hiện tại.' : null);
    if (passwordError) return setError(passwordError);

    setLoading(true);
    setError(null);
    try {
      const { message } = await authApi.changePassword(current, password);
      if (forced) {
        // Gỡ cờ → layout gốc mở khóa các tab; effect bên dưới chuyển về trang chủ.
        updateUser({ mustChangePassword: false });
      } else {
        notify(message, 'Thành công');
        router.back();
      }
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Screen edges={forced ? ['top', 'bottom'] : ['bottom']} background={colors.surface} contentStyle={styles.container}>
      {forced && (
        <View style={styles.header}>
          <Text style={styles.title}>Đặt mật khẩu mới</Text>
          <Text style={styles.subtitle}>
            Xin chào {fullName}! Bạn đang dùng mật khẩu tạm do hệ thống cấp. Hãy đặt mật khẩu của riêng bạn để tiếp tục.
          </Text>
        </View>
      )}

      <View style={styles.form}>
        <Notice tone="danger" message={error} />
        <TextField
          label={forced ? 'Mật khẩu tạm (trong email)' : 'Mật khẩu hiện tại'}
          icon="key-outline"
          password
          value={current}
          onChangeText={setCurrent}
          autoComplete="current-password"
          textContentType="password"
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
          onSubmitEditing={submit}
        />
        <Button title="Lưu mật khẩu" onPress={submit} loading={loading} />
        {forced && <Button title="Đăng xuất" variant="ghost" onPress={logout} disabled={loading} />}
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: { paddingHorizontal: 24 },
  header: { marginTop: 24, marginBottom: 8 },
  title: { fontSize: 26, fontWeight: '800', color: colors.foreground },
  subtitle: { marginTop: 6, fontSize: 14, lineHeight: 21, color: colors.muted },
  form: { marginTop: 20, gap: 16 },
});
