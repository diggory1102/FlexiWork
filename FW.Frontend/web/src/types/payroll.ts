import type { PagedQuery } from './api';

/** Khớp với FW.Application.Features.Payroll (PayrollConfigDtos.cs, PayrollDtos.cs). */

export type AllowanceBasis = 'monthly' | 'night_shift' | 'split_day';
export type EmploymentKind = 'full_time' | 'part_time';
export type PayrollPeriodStatus = 'draft' | 'finalized';
export type PayslipStatus = 'draft' | 'sent';
export type PayslipItemKind = 'basic' | 'allowance' | 'overtime' | 'penalty' | 'adjustment';

// ---------------------------------------------------------------- Cấu hình

export interface PayrollSettings {
  overtimeMultiplier: number;
  standardWorkDays: number;
  standardHoursPerDay: number;
}

export interface Allowance {
  id: number;
  name: string;
  basis: AllowanceBasis;
  basisLabel: string;
  amount: number;
  applyToAll: boolean;
  isActive: boolean;
  description: string | null;
  assignedCount: number;
}

export interface AllowanceRequest {
  name: string;
  basis: AllowanceBasis;
  amount: number;
  applyToAll: boolean;
  isActive: boolean;
  description: string | null;
}

export interface PayrollEmployeeFilter extends PagedQuery {
  branchId?: number | '';
  employmentType?: EmploymentKind | '';
  configured?: boolean | '';
}

export interface EmployeePay {
  userId: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
  status: string;
  branchId: number | null;
  branchName: string | null;
  positionTitle: string | null;
  employmentType: EmploymentKind;
  baseSalary: number | null;
  /** Đơn giá giờ riêng (null = theo chức vụ). */
  hourlyRate: number | null;
  positionHourlyRate: number | null;
  effectiveHourlyRate: number;
  overtimeHourlyRate: number;
  isConfigured: boolean;
  allowanceCount: number;
}

export interface EmployeeAllowance {
  allowanceId: number;
  name: string;
  basis: AllowanceBasis;
  basisLabel: string;
  defaultAmount: number;
  applyToAll: boolean;
  isActive: boolean;
  assigned: boolean;
  overrideAmount: number | null;
  effectiveAmount: number;
}

export interface EmployeePayDetail {
  pay: EmployeePay;
  allowances: EmployeeAllowance[];
}

export interface UpdateEmployeePayRequest {
  baseSalary: number | null;
  hourlyRate: number | null;
}

export interface EmployeeAllowanceInput {
  allowanceId: number;
  amount: number | null;
}

// ---------------------------------------------------------------- Kỳ lương

export interface PayrollPeriod {
  id: number;
  month: number;
  year: number;
  status: PayrollPeriodStatus;
  standardWorkDays: number;
  overtimeMultiplier: number;
  employeeCount: number;
  totalNet: number;
  calculatedAt: string | null;
  createdByName: string | null;
  finalizedByName: string | null;
  finalizedAt: string | null;
  payslipCount: number;
  sentCount: number;
  note: string | null;
}

export interface PayslipTotals {
  count: number;
  basic: number;
  allowance: number;
  overtime: number;
  penalty: number;
  net: number;
}

export interface PayrollReadiness {
  monthEnded: boolean;
  lockedTimesheets: number;
  unlockedTimesheets: number;
  missingPayslips: number;
  canFinalize: boolean;
  problems: string[];
}

export interface PayrollPeriodDetail {
  period: PayrollPeriod;
  totals: PayslipTotals;
  readiness: PayrollReadiness;
}

export interface PayrollSkipped {
  userId: number;
  employeeCode: string;
  fullName: string;
  reason: string;
}

export interface CalculatePayrollResult {
  period: PayrollPeriod;
  calculated: number;
  unlockedTimesheets: number;
  skipped: PayrollSkipped[];
  message: string;
}

export interface FinalizePayrollResult {
  period: PayrollPeriod;
  payslips: number;
  totalNet: number;
  message: string;
}

export interface SendPayslipsResult {
  sent: number;
  failed: number;
  failures: PayrollSkipped[];
  message: string;
}

// ---------------------------------------------------------------- Phiếu lương

export interface PayslipFilter extends PagedQuery {
  branchId?: number | '';
  status?: PayslipStatus | '';
}

export interface Payslip {
  id: number;
  periodId: number;
  month: number;
  year: number;
  periodStatus: PayrollPeriodStatus;
  employee: { id: number; employeeCode: string; fullName: string; avatarUrl: string | null; positionTitle: string | null };
  branchId: number | null;
  branchName: string | null;
  employmentType: EmploymentKind;
  baseSalary: number;
  hourlyRate: number;
  standardWorkDays: number;
  workDays: number;
  workHours: number;
  overtimeHours: number;
  basicPay: number;
  allowanceTotal: number;
  overtimePay: number;
  penaltyTotal: number;
  netPay: number;
  status: PayslipStatus;
  sentAt: string | null;
}

export interface PayslipItem {
  kind: PayslipItemKind;
  name: string;
  quantity: number;
  unitAmount: number;
  /** Thành tiền; khoản trừ là số âm. */
  amount: number;
  note: string | null;
}

export interface PayslipDetail {
  payslip: Payslip;
  items: PayslipItem[];
}
