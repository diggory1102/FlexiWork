/** Khớp với FW.Application.Features.Payroll (PayrollDtos.cs) — phiếu lương cá nhân (chỉ kỳ đã chốt). */
export type PayslipItemKind = 'basic' | 'allowance' | 'overtime' | 'penalty' | 'adjustment';

export interface Payslip {
  id: number;
  periodId: number;
  month: number;
  year: number;
  employmentType: 'full_time' | 'part_time';
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
  branchName: string | null;
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
