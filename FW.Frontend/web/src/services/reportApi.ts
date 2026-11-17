import { api } from './apiClient';
import type {
  Dashboard,
  DashboardFilter,
  EmployeeViolationDetail,
  LeaveUsageFilter,
  LeaveUsageReport,
  ViolationFilter,
  ViolationReport,
} from '@/types/reports';

/** Báo cáo & dashboard (Admin / HR: mọi chi nhánh; Quản lý: chi nhánh của mình). */
export const reportApi = {
  dashboard: (filter: DashboardFilter) => api.get<Dashboard>('/reports/dashboard', filter),
  violations: (filter: ViolationFilter) => api.get<ViolationReport>('/reports/violations', filter),
  employeeViolations: (userId: number, range: { fromDate?: string; toDate?: string }) =>
    api.get<EmployeeViolationDetail>(`/reports/violations/employees/${userId}`, range),
  leaveUsage: (filter: LeaveUsageFilter) => api.get<LeaveUsageReport>('/reports/leave-usage', filter),
};
