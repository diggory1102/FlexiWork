import { api } from './apiClient';
import { imageFormData, multipartConfig } from './multipart';
import type { PaginatedResult } from '@/types/api';
import type { AttendanceCapture, AttendanceLogItem, AttendanceResult, AttendanceToday } from '@/types/attendance';

const captureForm = (capture: AttendanceCapture, photoUri: string) =>
  imageFormData({ uri: photoUri, mimeType: 'image/jpeg', fileName: 'selfie.jpg' }, { ...capture });

export const attendanceApi = {
  /** Việc cần làm tiếp theo (check-in / check-out), chi nhánh của ca và các ca trong ngày. */
  today: () => api.get<AttendanceToday>('/attendance/today'),

  checkIn: async (capture: AttendanceCapture, photoUri: string) =>
    api.post<AttendanceResult>('/attendance/check-in', await captureForm(capture, photoUri), multipartConfig),

  checkOut: async (capture: AttendanceCapture, photoUri: string) =>
    api.post<AttendanceResult>('/attendance/check-out', await captureForm(capture, photoUri), multipartConfig),

  /** Lịch sử chấm công của tôi trong khoảng ngày (yyyy-MM-dd). */
  mine: (fromDate: string, toDate: string, pageIndex = 1, pageSize = 50) =>
    api.get<PaginatedResult<AttendanceLogItem>>('/attendance/me', { fromDate, toDate, pageIndex, pageSize }),
};
