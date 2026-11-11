import { useEffect } from 'react';
import { SplashScreen, Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useAuthHydrated, useAuthStore } from '@/stores/authStore';
import { useSessionGuard } from '@/hooks/useSessionGuard';
import { colors } from '@/theme';

// Giữ màn hình khởi động cho tới khi đọc xong phiên đăng nhập từ SecureStore
void SplashScreen.preventAutoHideAsync().catch(() => undefined);

/**
 * Layout gốc (Expo Router). Điều hướng theo trạng thái phiên bằng Stack.Protected:
 * - Chưa đăng nhập        → đăng nhập, quên mật khẩu.
 * - Đang dùng mật khẩu tạm → chỉ màn hình đổi mật khẩu (bắt buộc).
 * - Đã đăng nhập          → các tab chính.
 */
export default function RootLayout() {
  const hydrated = useAuthHydrated();
  const token = useAuthStore((s) => s.accessToken);
  const user = useAuthStore((s) => s.user);
  useSessionGuard(hydrated);

  useEffect(() => {
    if (hydrated) void SplashScreen.hideAsync().catch(() => undefined);
  }, [hydrated]);

  if (!hydrated) return null;

  const signedIn = !!token && !!user;
  const mustChangePassword = signedIn && user.mustChangePassword;

  return (
    <>
      <StatusBar style="dark" />
      <Stack
        screenOptions={{
          headerTitleAlign: 'center',
          headerTintColor: colors.foreground,
          headerStyle: { backgroundColor: colors.surface },
          headerTitleStyle: { fontWeight: '700' },
          headerBackButtonDisplayMode: 'minimal',
          contentStyle: { backgroundColor: colors.background },
        }}
      >
        <Stack.Protected guard={!signedIn}>
          <Stack.Screen name="login" options={{ headerShown: false }} />
          <Stack.Screen name="forgot-password" options={{ headerShown: false }} />
        </Stack.Protected>

        <Stack.Protected guard={signedIn && !mustChangePassword}>
          <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
          <Stack.Screen name="tasks" options={{ title: 'Công việc theo ca' }} />
          <Stack.Screen name="chat" options={{ title: 'Tin nhắn' }} />
        </Stack.Protected>

        <Stack.Protected guard={signedIn}>
          <Stack.Screen
            name="change-password"
            options={{ title: 'Đổi mật khẩu', headerShown: !mustChangePassword, gestureEnabled: !mustChangePassword }}
          />
        </Stack.Protected>
      </Stack>
    </>
  );
}
