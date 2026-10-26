import { create } from 'zustand';
import { DashboardKPIs, TodayShiftItem } from '../types';

interface DashboardState {
  kpis: DashboardKPIs;
  todayShifts: TodayShiftItem[];
  selectedDate: string;
  isRefreshing: boolean;
  setSelectedDate: (date: string) => void;
  refreshDashboard: () => Promise<void>;
}

export const useDashboardStore = create<DashboardState>((set) => ({
  kpis: {
    totalShifts: 4,
    scheduledEmployees: 3,
    checkedInEmployees: 2,
    lateCheckins: 1,
    earlyCheckouts: 0,
    forgotCheckin: 0,
    forgotCheckout: 0,
    leaveWithPermission: 0,
    leaveWithoutPermission: 0,
    unexpectedShifts: 0,
    deviceDuplicates: 0,
    expectedDailySalary: 1120000,
    currentActualSalary: 560000,
  },
  todayShifts: [
    {
      id: 1,
      employeeName: 'Lê Văn Nhân Viên',
      employeeCode: 'EMP003',
      shiftName: 'Ca sáng chuẩn',
      shiftHours: '08:00 - 12:00',
      coefficient: 1.0,
      status: 'checked_in',
    },
    {
      id: 2,
      employeeName: 'Trần Quản Lý',
      employeeCode: 'EMP002',
      shiftName: 'Ca chiều chuẩn',
      shiftHours: '13:00 - 17:00',
      coefficient: 1.0,
      status: 'upcoming',
    },
    {
      id: 3,
      employeeName: 'Nguyễn Quản Trị',
      employeeCode: 'EMP001',
      shiftName: 'Ca đêm',
      shiftHours: '22:00 - 06:00',
      coefficient: 1.5,
      status: 'not_started',
    },
  ],
  selectedDate: '25/10/2026',
  isRefreshing: false,
  setSelectedDate: (date) => set({ selectedDate: date }),
  refreshDashboard: async () => {
    set({ isRefreshing: true });
    // Simulate API fetch delay
    await new Promise((resolve) => setTimeout(resolve, 600));
    set({ isRefreshing: false });
  },
}));
