import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type {
  CalculateTimesheetsResult,
  ConfirmTimesheetsResult,
  LockTimesheetsResult,
  Timesheet,
  TimesheetDetail,
  TimesheetFilter,
  TimesheetScopeRequest,
  TimesheetSummary,
} from '@/types/timesheets';

/** Bảng công tháng: Quản lý xem / xác nhận chi nhánh mình, Admin / HR xem tất cả và khóa. */
export const timesheetApi = {
  list: (filter: TimesheetFilter) => api.get<PaginatedResult<Timesheet>>('/timesheets', filter),
  summary: (year: number, month: number, branchId?: number | '') => api.get<TimesheetSummary>('/timesheets/summary', { year, month, branchId }),
  detail: (id: number) => api.get<TimesheetDetail>(`/timesheets/${id}`),
  /** Bảng công của chính mình (dùng ở ứng dụng di động). */
  mine: (year?: number, month?: number) => api.get<Timesheet>('/timesheets/me', { year, month }),
  calculate: (body: TimesheetScopeRequest) => api.post<CalculateTimesheetsResult>('/timesheets/calculate', body),
  confirm: (body: TimesheetScopeRequest) => api.post<ConfirmTimesheetsResult>('/timesheets/confirm', body),
  lock: (body: TimesheetScopeRequest) => api.post<LockTimesheetsResult>('/timesheets/lock', body),
  reopen: (id: number, note: string) => api.post<Timesheet>(`/timesheets/${id}/reopen`, { note }),
  exportUrl: '/timesheets/export',
};
