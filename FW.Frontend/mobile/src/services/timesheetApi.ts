import { api } from './apiClient';
import type { Timesheet, TimesheetDetail } from '@/types/timesheet';

/** Bảng công của chính tôi theo tháng. */
export const timesheetApi = {
  /** Bảng công tháng (chưa có bản đã lưu thì trả số liệu tính trực tiếp, `id` = null). */
  mine: (year: number, month: number) => api.get<Timesheet>('/timesheets/me', { year, month }),
  /** Chi tiết từng ca trong tháng (chỉ khi đã có bảng công đã lưu). */
  detail: (id: number) => api.get<TimesheetDetail>(`/timesheets/${id}`),
};
