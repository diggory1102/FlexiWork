import { api, cleanParams } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type {
  AttendancePolicy,
  AttendancePolicyRequest,
  Schedule,
  ScheduleBatchResult,
  ScheduleItem,
  ScheduleRange,
  Shift,
  ShiftDetail,
  ShiftFilter,
  ShiftRequest,
} from '@/types/scheduling';

export const shiftApi = {
  list: (filter: ShiftFilter) => api.get<PaginatedResult<Shift>>('/shifts', filter),
  get: (id: number) => api.get<ShiftDetail>(`/shifts/${id}`),
  /** Ca chi nhánh được phép xếp: ca dùng chung + ca riêng của chi nhánh. */
  available: (branchId: number) => api.get<Shift[]>(`/shifts/available/${branchId}`),
  create: (body: ShiftRequest) => api.post<Shift>('/shifts', body),
  update: (id: number, body: ShiftRequest) => api.put<Shift>(`/shifts/${id}`, body),
  remove: (id: number) => api.delete<null>(`/shifts/${id}`),
};

export const attendancePolicyApi = {
  get: () => api.get<AttendancePolicy>('/attendance-policy'),
  update: (body: AttendancePolicyRequest) => api.put<AttendancePolicy>('/attendance-policy', body),
};

export const scheduleApi = {
  /** Lưới lịch chi nhánh (gồm lịch nháp và ca đã hủy). */
  branch: (range: ScheduleRange) => api.get<Schedule[]>('/schedules', range),
  mine: (fromDate?: string, toDate?: string) => api.get<Schedule[]>('/schedules/me', { fromDate, toDate }),
  /** Xếp nhiều ca (bản nháp). Có xung đột → lỗi kèm danh sách lý do, không lưu ca nào. */
  create: (branchId: number | undefined, items: ScheduleItem[]) => api.post<ScheduleBatchResult>('/schedules', { branchId, items }),
  copy: (body: { branchId?: number; sourceFrom: string; targetFrom: string; days: number }) =>
    api.post<ScheduleBatchResult>('/schedules/copy', body),
  publish: (range: ScheduleRange) => api.post<number>('/schedules/publish', range),
  deleteDrafts: (range: ScheduleRange) =>
    api.delete<number>(`/schedules/drafts?${new URLSearchParams(cleanParams(range) as Record<string, string>)}`),
  /** Nháp → xóa hẳn; đã công bố & chưa tới giờ → hủy ca. */
  remove: (id: number) => api.delete<null>(`/schedules/${id}`),
};
