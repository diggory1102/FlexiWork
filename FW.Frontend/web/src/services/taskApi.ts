import { api } from './apiClient';
import type { ScheduleTasks, TaskProgress, TaskTemplate, TaskTemplateFilter, TaskTemplateRequest } from '@/types/tasks';

export const taskApi = {
  templates: (filter: TaskTemplateFilter) => api.get<TaskTemplate[]>('/shift-tasks/templates', filter),
  createTemplate: (body: TaskTemplateRequest) => api.post<TaskTemplate>('/shift-tasks/templates', body),
  updateTemplate: (id: number, body: TaskTemplateRequest) => api.put<TaskTemplate>(`/shift-tasks/templates/${id}`, body),
  removeTemplate: (id: number) => api.delete<null>(`/shift-tasks/templates/${id}`),

  /** Tiến độ checklist các ca trong ngày (yyyy-MM-dd; mặc định hôm nay). */
  progress: (date?: string, branchId?: number | '') => api.get<TaskProgress>('/shift-tasks/progress', { date, branchId }),
  scheduleTasks: (scheduleId: number) => api.get<ScheduleTasks>(`/shift-tasks/schedules/${scheduleId}`),

  /** Quản lý bỏ tích một việc (việc làm chung thì mở lại cho cả ca). */
  uncomplete: (assignmentId: number) => api.post<ScheduleTasks>(`/shift-tasks/assignments/${assignmentId}/uncomplete`),
};
