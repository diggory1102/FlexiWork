import { useEffect, useRef } from 'react';
import { AppState } from 'react-native';
import { authApi } from '@/services/authApi';
import { useAuthStore } from '@/stores/authStore';

/** Không gọi /auth/me quá dày khi người dùng chuyển qua lại giữa các ứng dụng. */
const REFRESH_INTERVAL_MS = 60_000;

/**
 * Giữ phiên đăng nhập luôn đúng:
 * - Khi mở app / quay lại app: token hết hạn → đăng xuất.
 * - Còn hạn → đồng bộ lại thông tin người dùng (/auth/me) để nhận thay đổi từ HR
 *   (bị khóa → 401 → đăng xuất; được cấp lại mật khẩu → bắt buộc đổi mật khẩu).
 */
export function useSessionGuard(hydrated: boolean) {
  const lastSync = useRef(0);

  useEffect(() => {
    if (!hydrated) return;

    const check = () => {
      const { accessToken, isSessionValid, logout, updateUser } = useAuthStore.getState();
      if (!accessToken) return;
      if (!isSessionValid()) return logout();
      if (Date.now() - lastSync.current < REFRESH_INTERVAL_MS) return;

      lastSync.current = Date.now();
      authApi
        .me()
        .then(({ data }) => updateUser(data))
        .catch(() => undefined); // 401 đã được interceptor xử lý; lỗi mạng thì giữ phiên hiện tại
    };

    check();
    const subscription = AppState.addEventListener('change', (state) => state === 'active' && check());
    return () => subscription.remove();
  }, [hydrated]);
}
