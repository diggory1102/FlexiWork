import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type { Role } from '@/types/auth';
import type { FaceStatus } from '@/types/scheduling';
import type {
  CreateUserRequest,
  CreateUserResult,
  RoleOption,
  User,
  UserFilter,
  UserProfileRequest,
  UserStatus,
} from '@/types/user';

const list = (filter: UserFilter) => api.get<PaginatedResult<User>>('/users', filter);

/** API giới hạn số dòng mỗi trang (PagedQuery.MaxPageSize ở backend). */
const MAX_PAGE_SIZE = 100;

export const userApi = {
  list,
  /** Mọi nhân viên khớp bộ lọc (tải đủ các trang) — cho màn hình cần cả danh sách, VD lưới xếp lịch. */
  listAll: async (filter: Omit<UserFilter, 'pageIndex' | 'pageSize'>) => {
    const page = (pageIndex: number) => list({ ...filter, pageIndex, pageSize: MAX_PAGE_SIZE });
    const first = await page(1);
    const rest = await Promise.all(Array.from({ length: Math.max(first.data.totalPages - 1, 0) }, (_, i) => page(i + 2)));
    return [first, ...rest].flatMap((res) => res.data.items);
  },
  get: (id: number) => api.get<User>(`/users/${id}`),
  create: (body: CreateUserRequest) => api.post<CreateUserResult>('/users', body),
  update: (id: number, body: UserProfileRequest) => api.put<User>(`/users/${id}`, body),
  updateStatus: (id: number, status: UserStatus) => api.patch<User>(`/users/${id}/status`, { status }),
  updateRoles: (id: number, roles: Role[]) => api.put<User>(`/users/${id}/roles`, { roles }),
  reissuePassword: (id: number) => api.post<null>(`/users/${id}/reissue-password`),
  /** Đặt mật khẩu trực tiếp (không gửi email). */
  setPassword: (id: number, newPassword: string, mustChangePassword: boolean) =>
    api.put<null>(`/users/${id}/password`, { newPassword, mustChangePassword }),
  /** Trạng thái Face ID của nhân viên / đặt lại để nhân viên đăng ký lại. */
  faceStatus: (id: number) => api.get<FaceStatus>(`/users/${id}/face`),
  resetFace: (id: number) => api.delete<null>(`/users/${id}/face`),
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
