import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type { AttendanceLogDetail, AttendanceLogFilter, AttendanceLogItem } from '@/types/attendance';

export const attendanceApi = {
  /** Nhật ký chấm công theo phạm vi vai trò (Admin/HR: mọi chi nhánh; Quản lý: chi nhánh mình). */
  logs: (filter: AttendanceLogFilter) => api.get<PaginatedResult<AttendanceLogItem>>('/attendance/logs', filter),
  get: (id: number) => api.get<AttendanceLogDetail>(`/attendance/logs/${id}`),
  /** Chấp nhận / từ chối lượt đang chờ duyệt (từ chối phải ghi lý do). */
  /** Quản lý / HR nhập giờ ra thay nhân viên quên check-out (giờ Việt Nam, dạng yyyy-MM-ddTHH:mm:ss). */
  adjustCheckout: (id: number, checkoutTime: string, note: string) => api.put<AttendanceLogDetail>(`/attendance/logs/${id}/checkout`, { checkoutTime, note }),
  review: (id: number, approved: boolean, note: string | null) => api.put<AttendanceLogDetail>(`/attendance/logs/${id}/review`, { approved, note }),
};
