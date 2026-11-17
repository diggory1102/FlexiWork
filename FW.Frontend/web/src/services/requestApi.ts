import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type {
  AdjustBalanceRequest,
  LeaveBalanceAdjustment,
  LeaveBalanceFilter,
  LeaveBalanceRow,
  LeaveRequest,
  LeaveRequestFilter,
  LeaveType,
  LeaveTypeRequest,
} from '@/types/requests';

export const requestApi = {
  /** Đơn trong phạm vi quản lý (Admin/HR: mọi chi nhánh; Quản lý: chi nhánh mình). */
  list: (filter: LeaveRequestFilter) => api.get<PaginatedResult<LeaveRequest>>('/leave-requests', filter),
  get: (id: number) => api.get<LeaveRequest>(`/leave-requests/${id}`),
  /** Duyệt / từ chối đơn đang chờ duyệt (từ chối phải ghi lý do). Thông báo trả về tóm tắt các thay đổi đã áp dụng. */
  review: (id: number, approved: boolean, note: string | null) => api.put<LeaveRequest>(`/leave-requests/${id}/review`, { approved, note }),
};

export const leaveTypeApi = {
  list: (includeInactive = true) => api.get<LeaveType[]>('/leave-types', { includeInactive }),
  create: (body: LeaveTypeRequest) => api.post<LeaveType>('/leave-types', body),
  update: (id: number, body: LeaveTypeRequest) => api.put<LeaveType>(`/leave-types/${id}`, body),
  remove: (id: number) => api.delete<null>(`/leave-types/${id}`),
};

export const leaveBalanceApi = {
  list: (filter: LeaveBalanceFilter) => api.get<PaginatedResult<LeaveBalanceRow>>('/leave-balances', filter),
  adjust: (body: AdjustBalanceRequest) => api.post<LeaveBalanceRow>('/leave-balances/adjust', body),
  history: (userId: number, year: number) => api.get<LeaveBalanceAdjustment[]>(`/leave-balances/${userId}/adjustments`, { year }),
};
