import { api } from './apiClient';
import type { AttritionAlert, AttritionLevel, AttritionNarrative, AttritionRunResult, AttritionSummary } from '@/types/assistant';

/** Nhân sự có dấu hiệu nghỉ việc (UC 10.3 – 10.4). Chỉ Quản lý / HR; Quản lý chỉ thấy chi nhánh của mình. Là tín hiệu tham khảo, không phải kết luận. */
export const attritionApi = {
  alerts: (params: { branchId?: number; minLevel?: AttritionLevel; limit?: number } = {}) => api.get<AttritionAlert[]>('/attrition/alerts', params),
  summary: (params: { branchId?: number } = {}) => api.get<AttritionSummary>('/attrition/summary', params),
  /** Điểm các tuần gần đây của một nhân viên, cũ → mới. */
  trend: (userId: number) => api.get<AttritionAlert[]>(`/attrition/employees/${userId}/trend`),
  /** Đoạn diễn giải bằng lời do mô hình ngôn ngữ viết (gọi theo yêu cầu, có thể mất vài giây). */
  narrative: (userId: number) => api.get<AttritionNarrative>(`/attrition/employees/${userId}/narrative`),
  /** Chấm lại ngay (Admin / HR). */
  run: () => api.post<AttritionRunResult>('/attrition/run'),
};
