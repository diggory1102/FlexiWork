/** Checklist công việc theo ca — khớp FW.Application/Features/ShiftTasks. */

export type TaskPhase = 'start' | 'during' | 'end';

/** each = mỗi người một bản việc; shared = làm chung, một người xong là cả ca xong. */
export type TaskAssignMode = 'each' | 'shared';

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
  assignMode: TaskAssignMode;
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
  assignMode: TaskAssignMode;
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

/** Một việc làm chung của một ca: ai đã làm hoặc chưa ai nhận. */
export interface SharedTask {
  taskId: number;
  title: string;
  phase: TaskPhase;
  isMandatory: boolean;
  branchId: number;
  branchName: string;
  shiftName: string;
  startTime: string;
  endTime: string;
  participants: number;
  isDone: boolean;
  completedByName: string | null;
  completedAt: string | null;
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
  sharedTasks: SharedTask[];
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
  assignMode: TaskAssignMode;
  /** Người đã hoàn thành (việc làm chung có thể là đồng nghiệp). */
  completedByUserId: number | null;
  completedByName: string | null;
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
