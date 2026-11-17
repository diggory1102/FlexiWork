/** Checklist công việc theo ca — khớp FW.Application/Features/ShiftTasks. */

export type TaskPhase = 'start' | 'during' | 'end';

export interface TaskItem {
  /** null = việc dự kiến (ca chưa check-in). */
  assignmentId: number | null;
  taskId: number;
  title: string;
  description: string | null;
  phase: TaskPhase;
  sortOrder: number;
  isMandatory: boolean;
  requiresPhoto: boolean;
  isCompleted: boolean;
  completedAt: string | null;
  proofImageUrl: string | null;
  note: string | null;
}

export interface ScheduleTasks {
  scheduleId: number;
  userId: number;
  employeeCode: string;
  fullName: string;
  workDate: string;
  shiftName: string;
  startTime: string;
  endTime: string;
  scheduleStatus: string;
  isAssigned: boolean;
  canUpdate: boolean;
  total: number;
  done: number;
  percent: number | null;
  tasks: TaskItem[];
}
