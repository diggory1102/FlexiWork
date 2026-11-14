import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser, LoginResponse, Role } from '@/types/auth';

interface AuthState {
  accessToken: string | null;
  expiresAtUtc: string | null;
  user: AuthUser | null;
  /** Lý do phiên bị kết thúc từ phía máy chủ (VD: mật khẩu đã đổi trên thiết bị khác) — hiện ở trang đăng nhập. */
  signOutNotice: string | null;
  setSession: (session: LoginResponse) => void;
  updateUser: (changes: Partial<AuthUser>) => void;
  logout: (notice?: string) => void;
  hasAnyRole: (roles: Role[]) => boolean;
  isSessionValid: () => boolean;
}

/** Trạng thái đăng nhập toàn cục (Zustand), lưu vào localStorage. */
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
      hasAnyRole: (roles) => {
        const userRoles = get().user?.roles ?? [];
        return roles.some((r) => userRoles.includes(r));
      },
      isSessionValid: () => {
        const { accessToken, expiresAtUtc } = get();
        return !!accessToken && (!expiresAtUtc || new Date(expiresAtUtc).getTime() > Date.now());
      },
    }),
    { name: 'flexiwork-auth' },
  ),
);
