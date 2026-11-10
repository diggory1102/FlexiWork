import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser, LoginResponse, Role } from '@/types/auth';

interface AuthState {
  accessToken: string | null;
  expiresAtUtc: string | null;
  user: AuthUser | null;
  setSession: (session: LoginResponse) => void;
  updateUser: (changes: Partial<AuthUser>) => void;
  logout: () => void;
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
      setSession: ({ accessToken, expiresAtUtc, user }) => set({ accessToken, expiresAtUtc, user }),
      updateUser: (changes) => {
        const user = get().user;
        if (user) set({ user: { ...user, ...changes } });
      },
      logout: () => set({ accessToken: null, expiresAtUtc: null, user: null }),
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
