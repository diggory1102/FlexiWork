import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type {
  Allowance,
  AllowanceRequest,
  CalculatePayrollResult,
  EmployeeAllowanceInput,
  EmployeePay,
  EmployeePayDetail,
  FinalizePayrollResult,
  PayrollEmployeeFilter,
  PayrollPeriod,
  PayrollPeriodDetail,
  PayrollSettings,
  Payslip,
  PayslipDetail,
  PayslipFilter,
  SendPayslipsResult,
  UpdateEmployeePayRequest,
} from '@/types/payroll';

/** Cấu hình lương (Admin / HR): tham số chung, danh mục phụ cấp, lương và phụ cấp từng nhân viên. */
export const payrollConfigApi = {
  settings: () => api.get<PayrollSettings>('/payroll-config/settings'),
  updateSettings: (overtimeMultiplier: number) => api.put<PayrollSettings>('/payroll-config/settings', { overtimeMultiplier }),

  allowances: () => api.get<Allowance[]>('/payroll-config/allowances'),
  createAllowance: (body: AllowanceRequest) => api.post<Allowance>('/payroll-config/allowances', body),
  updateAllowance: (id: number, body: AllowanceRequest) => api.put<Allowance>(`/payroll-config/allowances/${id}`, body),
  removeAllowance: (id: number) => api.delete<void>(`/payroll-config/allowances/${id}`),

  employees: (filter: PayrollEmployeeFilter) => api.get<PaginatedResult<EmployeePay>>('/payroll-config/employees', filter),
  employee: (userId: number) => api.get<EmployeePayDetail>(`/payroll-config/employees/${userId}`),
  updateEmployeePay: (userId: number, body: UpdateEmployeePayRequest) => api.put<EmployeePayDetail>(`/payroll-config/employees/${userId}`, body),
  setEmployeeAllowances: (userId: number, items: EmployeeAllowanceInput[]) =>
    api.put<EmployeePayDetail>(`/payroll-config/employees/${userId}/allowances`, { items }),
};

/** Bảng lương theo kỳ (Admin / HR): tính từ bảng công đã khóa, chốt kỳ, gửi phiếu lương. */
export const payrollApi = {
  periods: (year?: number) => api.get<PayrollPeriod[]>('/payroll/periods', { year }),
  period: (id: number) => api.get<PayrollPeriodDetail>(`/payroll/periods/${id}`),
  calculate: (year: number, month: number) => api.post<CalculatePayrollResult>('/payroll/periods/calculate', { year, month }),
  finalize: (id: number) => api.post<FinalizePayrollResult>(`/payroll/periods/${id}/finalize`),
  send: (id: number, payslipIds?: number[]) => api.post<SendPayslipsResult>(`/payroll/periods/${id}/send`, payslipIds ? { payslipIds } : {}),
  payslips: (id: number, filter: PayslipFilter) => api.get<PaginatedResult<Payslip>>(`/payroll/periods/${id}/payslips`, filter),
  payslip: (id: number) => api.get<PayslipDetail>(`/payslips/${id}`),
  exportUrl: (id: number) => `/payroll/periods/${id}/export`,
};
