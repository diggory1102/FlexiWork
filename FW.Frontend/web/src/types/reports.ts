import type { PagedQuery, PaginatedResult } from './api';

/** Khớp với FW.Application.Features.Reports (ReportDtos.cs). Ngày là chuỗi ISO "yyyy-MM-ddT00:00:00". */

export interface DashboardFilter {
  branchId?: number | '';
  date?: string;
}

export interface Headcount {
  scheduledEmployees: number;
  scheduledShifts: number;
  checkedInEmployees: number;
  workingEmployees: number;
  waitingCheckInShifts: number;
  upcomingShifts: number;
  completedShifts: number;
  absentShifts: number;
  onLeaveEmployees: number;
  lateShifts: number;
  /** % lượt có mặt không đi muộn; null nếu chưa ai chấm công. */
  onTimeRate: number | null;
  pendingAttendance: number;
  pendingRequests: number;
}

export interface BranchHeadcount {
  branchId: number;
  branchName: string;
  headcount: Headcount;
}

export interface DashboardTrend {
  date: string;
  scheduledShifts: number;
  attendedShifts: number;
  lateShifts: number;
  absentShifts: number;
  onTimeRate: number | null;
}

export interface Dashboard {
  asOf: string;
  date: string;
  totals: Headcount;
  branches: BranchHeadcount[];
  trend: DashboardTrend[];
}

export interface ViolationSummary {
  scheduledShifts: number;
  evaluatedShifts: number;
  attendedShifts: number;
  onLeaveShifts: number;
  lateCount: number;
  lateMinutes: number;
  earlyCount: number;
  earlyMinutes: number;
  absentCount: number;
  violatingShifts: number;
  violationRate: number | null;
  penaltyAmount: number;
  deductedWorkDays: number;
}

export interface BranchViolation {
  branchId: number;
  branchName: string;
  summary: ViolationSummary;
}

export interface EmployeeViolation {
  userId: number;
  employeeCode: string;
  fullName: string;
  branchId: number;
  branchName: string | null;
  summary: ViolationSummary;
}

export interface ViolationReport {
  fromDate: string;
  toDate: string;
  summary: ViolationSummary;
  branches: BranchViolation[];
  employees: PaginatedResult<EmployeeViolation>;
}

export interface ViolationFilter extends PagedQuery {
  branchId?: number | '';
  fromDate?: string;
  toDate?: string;
}

export type ViolationType = 'late' | 'early_leave' | 'absent';

export interface ViolationItem {
  workDate: string;
  shiftName: string;
  startTime: string;
  endTime: string;
  type: ViolationType;
  minutes: number;
  penaltyAmount: number;
  deductWorkUnits: number;
}

export interface EmployeeViolationDetail {
  employee: EmployeeViolation;
  fromDate: string;
  toDate: string;
  items: ViolationItem[];
}

export interface LeaveUsageEmployee {
  userId: number;
  employeeCode: string;
  fullName: string;
  branchName: string | null;
  totalDays: number;
  usedDays: number;
  remainingDays: number;
  isDefault: boolean;
}

export interface LeaveUsageSummary {
  employees: number;
  totalDays: number;
  usedDays: number;
  remainingDays: number;
  usageRate: number | null;
  pendingDays: number;
  withoutBalance: number;
  lowBalanceEmployees: number;
}

export interface BranchLeaveUsage {
  branchId: number | null;
  branchName: string;
  employees: number;
  totalDays: number;
  usedDays: number;
  remainingDays: number;
  usageRate: number | null;
}

export interface LeaveTypeUsage {
  leaveTypeId: number;
  name: string;
  isPaid: boolean;
  deductsBalance: boolean;
  approvedRequests: number;
  approvedDays: number;
  pendingDays: number;
}

export interface MonthUsage {
  month: number;
  days: number;
}

export interface LeaveUsageReport {
  year: number;
  defaultDays: number;
  lowBalanceDays: number;
  summary: LeaveUsageSummary;
  branches: BranchLeaveUsage[];
  byType: LeaveTypeUsage[];
  byMonth: MonthUsage[];
  topUsed: LeaveUsageEmployee[];
  lowRemaining: LeaveUsageEmployee[];
}

export interface LeaveUsageFilter {
  year?: number;
  branchId?: number | '';
}
