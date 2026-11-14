import { useEffect, useState } from 'react';
import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';
import { create } from 'zustand';
import { createJSONStorage, persist, type StateStorage } from 'zustand/middleware';
import type { AuthUser, LoginResponse } from '@/types/auth';

/**
 * Lưu phiên đăng nhập vào bộ nhớ bảo mật của thiết bị (iOS Keychain / Android Keystore).
 * Bản web (chỉ dùng để xem thử giao diện khi phát triển) không có SecureStore nên dùng localStorage.
 */
const secureStorage: StateStorage =
  Platform.OS === 'web'
    ? {
        getItem: (name) => globalThis.localStorage?.getItem(name) ?? null,
        setItem: (name, value) => globalThis.localStorage?.setItem(name, value),
        removeItem: (name) => globalThis.localStorage?.removeItem(name),
      }
    : {
        getItem: (name) => SecureStore.getItemAsync(name),
        setItem: (name, value) => SecureStore.setItemAsync(name, value),
        removeItem: (name) => SecureStore.deleteItemAsync(name),
      };

interface AuthState {
  accessToken: string | null;
  expiresAtUtc: string | null;
  user: AuthUser | null;
  /** Lý do phiên bị kết thúc từ phía máy chủ (VD: mật khẩu đã đổi trên thiết bị khác) — hiện ở trang đăng nhập. */
  signOutNotice: string | null;
  setSession: (session: LoginResponse) => void;
  updateUser: (changes: Partial<AuthUser>) => void;
  logout: (notice?: string) => void;
  isSessionValid: () => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      expiresAtUtc: null,
      user: null,
      signOutNotice: null,
      setSession: ({ accessToken, expiresAtUtc, user }) => set({ accessToken, expiresAtUtc, user, signOutNotice: null }),
      updateUser: (changes) => {
        const user = get().user;
        if (user) set({ user: { ...user, ...changes } });
      },
      logout: (notice) => set({ accessToken: null, expiresAtUtc: null, user: null, signOutNotice: notice ?? null }),
      isSessionValid: () => {
        const { accessToken, expiresAtUtc } = get();
        return !!accessToken && (!expiresAtUtc || new Date(expiresAtUtc).getTime() > Date.now());
      },
    }),
    {
      name: 'flexiwork-auth',
      storage: createJSONStorage(() => secureStorage),
      partialize: ({ accessToken, expiresAtUtc, user }) => ({ accessToken, expiresAtUtc, user }),
    },
  ),
);

/** true khi phiên đã được đọc xong từ bộ nhớ bảo mật (đọc bất đồng bộ lúc mở app). */
export function useAuthHydrated(): boolean {
  const [hydrated, setHydrated] = useState(() => useAuthStore.persist.hasHydrated());
  useEffect(() => {
    const unsubscribe = useAuthStore.persist.onFinishHydration(() => setHydrated(true));
    setHydrated(useAuthStore.persist.hasHydrated());
    return unsubscribe;
  }, []);
  return hydrated;
}
