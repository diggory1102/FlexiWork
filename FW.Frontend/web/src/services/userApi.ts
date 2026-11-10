import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type { Role } from '@/types/auth';
import type {
  CreateUserRequest,
  CreateUserResult,
  RoleOption,
  User,
  UserFilter,
  UserProfileRequest,
  UserStatus,
} from '@/types/user';

export const userApi = {
  list: (filter: UserFilter) => api.get<PaginatedResult<User>>('/users', filter),
  get: (id: number) => api.get<User>(`/users/${id}`),
  create: (body: CreateUserRequest) => api.post<CreateUserResult>('/users', body),
  update: (id: number, body: UserProfileRequest) => api.put<User>(`/users/${id}`, body),
  updateStatus: (id: number, status: UserStatus) => api.patch<User>(`/users/${id}/status`, { status }),
  updateRoles: (id: number, roles: Role[]) => api.put<User>(`/users/${id}/roles`, { roles }),
  reissuePassword: (id: number) => api.post<null>(`/users/${id}/reissue-password`),
  remove: (id: number) => api.delete<null>(`/users/${id}`),
  roles: () => api.get<RoleOption[]>('/roles'),
};

export const profileApi = {
  get: () => api.get<User>('/profile'),
  update: (phoneNumber: string | null) => api.put<User>('/profile', { phoneNumber }),
  uploadAvatar: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return api.post<User>('/profile/avatar', form);
  },
};
