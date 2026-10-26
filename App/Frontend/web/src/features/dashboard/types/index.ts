export interface DashboardKPIs {
  totalShifts: number;
  scheduledEmployees: number;
  checkedInEmployees: number;
  lateCheckins: number;
  earlyCheckouts: number;
  forgotCheckin: number;
  forgotCheckout: number;
  leaveWithPermission: number;
  leaveWithoutPermission: number;
  unexpectedShifts: number;
  deviceDuplicates: number;
  expectedDailySalary: number;
  currentActualSalary: number;
}

export interface TodayShiftItem {
  id: number;
  employeeName: string;
  employeeCode: string;
  shiftName: string;
  shiftHours: string;
  coefficient: number;
  status: 'checked_in' | 'upcoming' | 'not_started';
}
