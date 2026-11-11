import { api } from './apiClient';
import type { AuthUser, LoginResponse } from '@/types/auth';

export const authApi = {
  login: (email: string, password: string) => api.post<LoginResponse>('/auth/login', { email, password }),
  me: () => api.get<AuthUser>('/auth/me'),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<null>('/auth/change-password', { currentPassword, newPassword }),
  forgotPassword: (email: string) => api.post<null>('/auth/forgot-password', { email }),
  resetPassword: (email: string, otp: string, newPassword: string) =>
    api.post<null>('/auth/reset-password', { email, otp, newPassword }),
};
