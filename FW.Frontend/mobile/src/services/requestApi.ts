import { api } from './apiClient';
import { formDataWith, multipartConfig, type PickedImage } from './multipart';
import type { PaginatedResult } from '@/types/api';
import type { CreateRequestInput, LeaveBalance, LeaveRequest, LeaveType, RequestCategory, RequestStatus, SwapOptions } from '@/types/requests';

export interface RequestListFilter {
  status?: RequestStatus;
  category?: RequestCategory;
  pageIndex?: number;
  pageSize?: number;
}

export const requestApi = {
  /** Loại đơn đang nhận đơn mới. */
  types: () => api.get<LeaveType[]>('/leave-types'),

  /** Đơn của tôi, mới nhất trước. */
  mine: (filter: RequestListFilter = {}) => api.get<PaginatedResult<LeaveRequest>>('/leave-requests/me', filter),

  /** Đơn đổi ca đồng nghiệp gửi tới tôi. */
  peer: (filter: RequestListFilter = {}) => api.get<PaginatedResult<LeaveRequest>>('/leave-requests/peer', filter),

  get: (id: number) => api.get<LeaveRequest>(`/leave-requests/${id}`),

  /** Gửi đơn (form-data, ảnh minh chứng tùy chọn). */
  create: async (input: CreateRequestInput, image?: PickedImage | null) =>
    api.post<LeaveRequest>('/leave-requests', await formDataWith({ ...input }, image), multipartConfig),

  cancel: (id: number) => api.post<LeaveRequest>(`/leave-requests/${id}/cancel`),

  /** Đồng nghiệp đồng ý / từ chối đổi ca. */
  respond: (id: number, accepted: boolean, note?: string) =>
    api.post<LeaveRequest>(`/leave-requests/${id}/peer-response`, { accepted, note: note || null }),

  balance: (year?: number) => api.get<LeaveBalance>('/leave-balances/me', year ? { year } : undefined),

  /** Ca của đồng nghiệp / người làm thay cho ca của tôi. */
  swapOptions: (sourceScheduleId: number) => api.get<SwapOptions>('/leave-requests/swap-options', { sourceScheduleId }),
};
