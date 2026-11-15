import { api } from './apiClient';
import type { Schedule } from '@/types/scheduling';

export const scheduleApi = {
  /** Lịch đã công bố của tôi trong khoảng ngày (yyyy-MM-dd, tối đa 62 ngày). */
  mine: (fromDate: string, toDate: string) => api.get<Schedule[]>('/schedules/me', { fromDate, toDate }),
};
