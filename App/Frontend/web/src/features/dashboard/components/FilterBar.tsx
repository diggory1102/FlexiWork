import React from 'react';
import { Calendar as CalendarIcon, RotateCw } from 'lucide-react';

interface FilterBarProps {
  selectedBranch: string;
  onBranchChange: (branch: string) => void;
  selectedDate: string;
  onDateChange: (date: string) => void;
  onFilter: () => void;
  onRefresh: () => void;
  isRefreshing: boolean;
}

export const FilterBar: React.FC<FilterBarProps> = ({
  selectedBranch,
  onBranchChange,
  selectedDate,
  onDateChange,
  onFilter,
  onRefresh,
  isRefreshing,
}) => {
  return (
    <section className="flex flex-wrap items-center gap-2 mb-4">
      <div className="relative min-w-[210px]">
        <select
          value={selectedBranch}
          onChange={(e) => onBranchChange(e.target.value)}
          className="w-full bg-white border border-slate-300 rounded-md py-1.5 pl-3 pr-8 text-xs font-medium text-slate-700 shadow-xs focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
        >
          <option value="FlexiWork Cầu Giấy">FlexiWork Cầu Giấy (CN-HN-01)</option>
          <option value="FlexiWork Hoàn Kiếm">FlexiWork Hoàn Kiếm (CN-HN-02)</option>
        </select>
      </div>

      <div className="relative">
        <input
          type="text"
          value={selectedDate}
          onChange={(e) => onDateChange(e.target.value)}
          className="bg-white border border-slate-300 rounded-md py-1.5 pl-3 pr-8 text-xs font-medium text-slate-700 w-36 shadow-xs focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        <CalendarIcon className="w-3.5 h-3.5 text-slate-400 absolute right-2.5 top-2.5 pointer-events-none" />
      </div>

      <button
        onClick={onFilter}
        className="bg-[#1877f2] hover:bg-blue-600 text-white font-medium text-xs px-4 py-1.5 rounded-md shadow-xs transition-colors"
      >
        Lọc
      </button>

      <button
        onClick={onRefresh}
        className="w-8 h-8 bg-slate-600 hover:bg-slate-700 text-white rounded-md flex items-center justify-center shadow-xs transition-colors"
        title="Làm mới dữ liệu"
      >
        <RotateCw className={`w-3.5 h-3.5 ${isRefreshing ? 'animate-spin' : ''}`} />
      </button>
    </section>
  );
};
