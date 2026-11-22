/** Checklist công việc theo ca — khớp FW.Application/Features/ShiftTasks. */

export type TaskPhase = 'start' | 'during' | 'end';

export interface TaskTemplate {
  id: number;
  shiftId: number;
  shiftCode: string;
  shiftName: string;
  branchId: number | null;
  branchName: string | null;
  positionId: number | null;
  positionTitle: string | null;
  title: string;
  description: string | null;
  phase: TaskPhase;
  sortOrder: number;
  isMandatory: boolean;
  requiresPhoto: boolean;
  canEdit: boolean;
}

export interface TaskTemplateRequest {
  shiftId: number;
  branchId: number | null;
  positionId: number | null;
  title: string;
  description: string | null;
  phase: TaskPhase;
  sortOrder: number;
  isMandatory: boolean;
  requiresPhoto: boolean;
}

export interface TaskTemplateFilter {
  shiftId?: number | '';
  branchId?: number | '';
}

export interface ScheduleProgress {
  scheduleId: number;
  userId: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
  branchId: number;
  branchName: string;
  shiftName: string;
  startTime: string;
  endTime: string;
  scheduleStatus: string;
  total: number;
  done: number;
  mandatoryTotal: number;
  mandatoryDone: number;
  percent: number | null;
  /** Số việc mẫu sẽ giao cho người làm ca này — hiện "dự kiến" khi họ chưa check-in. */
  expected: number;
  expectedMandatory: number;
}

export interface TaskProgress {
  date: string;
  branchId: number | null;
  totalTasks: number;
  doneTasks: number;
  percent: number | null;
  mandatoryPending: number;
  notStarted: number;
  schedules: ScheduleProgress[];
}

export interface TaskItem {
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
