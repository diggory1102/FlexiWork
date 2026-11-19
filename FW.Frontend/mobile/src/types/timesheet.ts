/** Khớp với FW.Application.Features.Timesheets (TimesheetDtos.cs) — phần nhân viên xem bảng công của mình. */
export type TimesheetStatus = 'draft' | 'confirmed' | 'locked' | 'finalized';

/** Kết quả của một ca khi tính bảng công. */
export type ShiftOutcome = 'worked' | 'absent' | 'leave' | 'pending' | 'rejected' | 'missing_checkout' | 'upcoming';

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
  year: number;
  month: number;
  standardWorkDays: number;
  status: TimesheetStatus;
  /** true = chưa có bảng công đã lưu (hoặc đang nháp): số liệu tính trực tiếp từ dữ liệu hiện tại. */
  isPreview: boolean;
  figures: TimesheetFigures;
  calculatedAt: string | null;
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
  lines: TimesheetLine[];
  overtime: OvertimeLine[];
}
