export interface MonthlyTimesheet {
  id: number;
  userId: number;
  month: number;
  year: number;
  standardWorkDays: number;
  actualWorkDays: number;
  paidLeaveDays: number;
  unpaidLeaveDays: number;
  overtimeHours: number;
  lateDeductionAmount: number;
  grossSalary: number;
  netSalary: number;
  status: 'draft' | 'approved' | 'paid';
}
