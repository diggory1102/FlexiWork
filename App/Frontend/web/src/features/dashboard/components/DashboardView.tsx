import React, { useState } from 'react';
import { useAppStore } from '../../../stores/useAppStore';
import { useDashboardStore } from '../store/useDashboardStore';
import { FilterBar } from './FilterBar';
import { HeroCard } from './HeroCard';
import { KpiGrid } from './KpiGrid';
import { SalaryBanner } from './SalaryBanner';
import { TodayShiftsTable } from './TodayShiftsTable';

export const DashboardView: React.FC = () => {
  const { currentBranch } = useAppStore();
  const {
    kpis,
    todayShifts,
    selectedDate,
    setSelectedDate,
    isRefreshing,
    refreshDashboard,
  } = useDashboardStore();

  const [selectedBranchName, setSelectedBranchName] = useState(
    currentBranch?.name || 'FlexiWork Cầu Giấy'
  );

  const handleBranchChange = (branchName: string) => {
    setSelectedBranchName(branchName);
  };

  const handleFilter = () => {
    refreshDashboard();
  };

  return (
    <div>
      {/* FILTER BAR */}
      <FilterBar
        selectedBranch={selectedBranchName}
        onBranchChange={handleBranchChange}
        selectedDate={selectedDate}
        onDateChange={setSelectedDate}
        onFilter={handleFilter}
        onRefresh={refreshDashboard}
        isRefreshing={isRefreshing}
      />

      {/* DASHBOARD GRID 12 COLUMNS */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
        {/* CỘT TRÁI (7 COLS): TỔNG CA, CHỈ SỐ, CHECKLIST TASK */}
        <div className="lg:col-span-7 space-y-4">
          <HeroCard
            totalShifts={kpis.totalShifts}
            scheduledEmployees={kpis.scheduledEmployees}
            checkedInEmployees={kpis.checkedInEmployees}
          />

          <KpiGrid kpis={kpis} />
        </div>

        {/* CỘT PHẢI (5 COLS): BANNER TỔNG LƯƠNG & LỊCH LÀM VIỆC */}
        <div className="lg:col-span-5 space-y-4">
          <SalaryBanner
            expectedSalary={kpis.expectedDailySalary}
            actualSalary={kpis.currentActualSalary}
            onRefresh={refreshDashboard}
          />

          <TodayShiftsTable shifts={todayShifts} />
        </div>
      </div>
    </div>
  );
};
