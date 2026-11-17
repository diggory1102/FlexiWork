import { api } from './apiClient';
import { formDataWith, multipartConfig, type PickedImage } from './multipart';
import type { ScheduleTasks } from '@/types/tasks';

export const taskApi = {
  /** Checklist các ca của tôi trong ngày (mặc định hôm nay; gồm ca đêm hôm trước còn đang làm). */
  mine: (date?: string) => api.get<ScheduleTasks[]>('/shift-tasks/me', date ? { date } : undefined),

  /** Tích hoàn thành, kèm ghi chú và ảnh hiện trường (bắt buộc với việc cần ảnh). */
  complete: async (assignmentId: number, note?: string, image?: PickedImage | null) =>
    api.post<ScheduleTasks>(`/shift-tasks/assignments/${assignmentId}/complete`, await formDataWith({ note }, image), multipartConfig),

  uncomplete: (assignmentId: number) => api.post<ScheduleTasks>(`/shift-tasks/assignments/${assignmentId}/uncomplete`),
};
