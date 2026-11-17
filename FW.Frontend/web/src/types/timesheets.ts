import type { PagedQuery } from './api';

/** Khớp với FW.Application.Features.Timesheets (TimesheetDtos.cs). */

export type TimesheetStatus = 'draft' | 'confirmed' | 'locked' | 'finalized';

/** Kết quả của một ca khi tính bảng công. */
export type ShiftOutcome = 'worked' | 'absent' | 'leave' | 'pending' | 'rejected' | 'missing_checkout' | 'upcoming';

export interface TimesheetFilter extends PagedQuery {
  year?: number;
  month?: number;
  branchId?: number | '';
  status?: TimesheetStatus | '';
}

export interface TimesheetEmployee {
  id: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
  positionTitle: string | null;
}

export interface TimesheetFigures {
  scheduledShifts: number;
  workedShifts: number;
  absentShifts: number;
  pendingLogs: number;
  rejectedLogs: number;
  missingCheckouts: number;
  actualWorkDays: number;
  actualWorkHours: number;
  totalLateMinutes: number;
  totalEarlyMinutes: number;
  violationCount: number;
  deductedWorkDays: number;
  penaltyAmount: number;
  totalOvertimeHours: number;
  paidLeaveDays: number;
  unpaidLeaveDays: number;
  nightShifts: number;
  splitShiftDays: number;
}

export interface Timesheet {
  id: number | null;
  employee: TimesheetEmployee;
  branchId: number | null;
  branchName: string | null;
  year: number;
  month: number;
  standardWorkDays: number;
  status: TimesheetStatus;
  /** true = chưa có bảng công đã lưu (hoặc đang nháp) — số liệu tính trực tiếp từ dữ liệu hiện tại. */
  isPreview: boolean;
  figures: TimesheetFigures;
  calculatedSalary: number;
  calculatedAt: string | null;
  confirmedByName: string | null;
  confirmedAt: string | null;
  lockedByName: string | null;
  lockedAt: string | null;
  note: string | null;
}

export interface TimesheetLine {
  scheduleId: number;
  workDate: string;
  shiftName: string;
  startTime: string;
  endTime: string;
  outcome: ShiftOutcome;
  leaveKind: 'paid' | 'unpaid' | null;
  workUnits: number;
  workHours: number;
  lateMinutes: number;
  earlyMinutes: number;
  deductedUnits: number;
  penalty: number;
  exempted: boolean;
  logId: number | null;
  checkIn: string | null;
  checkOut: string | null;
}

export interface OvertimeLine {
  startAt: string;
  endAt: string;
  hours: number;
}

export interface TimesheetDetail {
  timesheet: Timesheet;
  live: TimesheetFigures;
  /** Dữ liệu nguồn đã đổi sau khi xác nhận / khóa — số liệu trực tiếp khác số liệu đã lưu. */
  drifted: boolean;
  lines: TimesheetLine[];
  overtime: OvertimeLine[];
  canConfirm: boolean;
  canLock: boolean;
  canReopen: boolean;
}

export interface TimesheetSummary {
  year: number;
  month: number;
  draft: number;
  confirmed: number;
  locked: number;
  finalized: number;
  monthEnded: boolean;
}

/** Phạm vi của thao tác tính / xác nhận / khóa. */
export interface TimesheetScopeRequest {
  year: number;
  month: number;
  branchId?: number | null;
  userIds?: number[] | null;
}

export interface CalculateTimesheetsResult {
  year: number;
  month: number;
  calculated: number;
  skippedConfirmed: number;
  skippedFrozen: number;
  message: string;
}

export interface TimesheetBlocker {
  userId: number;
  fullName: string;
  reason: string;
}

export interface ConfirmTimesheetsResult {
  confirmed: number;
  blocked: TimesheetBlocker[];
  message: string;
}

export interface LockTimesheetsResult {
  locked: number;
  remainingDrafts: number;
  message: string;
}
