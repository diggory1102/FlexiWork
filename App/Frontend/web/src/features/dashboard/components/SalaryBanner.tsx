import React from 'react';
import { RotateCw, ChevronRight } from 'lucide-react';

interface SalaryBannerProps {
  expectedSalary: number;
  actualSalary: number;
  onRefresh: () => void;
}

export const SalaryBanner: React.FC<SalaryBannerProps> = ({
  expectedSalary,
  actualSalary,
  onRefresh,
}) => {
  const formatVnd = (num: number) => {
    return num.toLocaleString('vi-VN') + 'đ';
  };

  return (
    <div className="bg-gradient-to-r from-[#0d6efd] via-[#1070fc] to-[#2080ff] text-white rounded-xl p-4 shadow-xs flex items-center justify-between">
      <div className="flex items-center space-x-8">
        {/* Lương ngày dự kiến */}
        <div>
          <div className="flex items-center space-x-1.5 font-bold text-lg leading-tight">
            <span>{formatVnd(expectedSalary)}</span>
            <button
              onClick={onRefresh}
              className="hover:rotate-180 transition-transform duration-300 text-xs text-blue-200"
              title="Cập nhật"
            >
              <RotateCw className="w-3 h-3" />
            </button>
          </div>
          <div className="text-[11px] text-blue-100 font-normal mt-0.5">
            Tổng lương ngày dự kiến
          </div>
        </div>

        {/* Lương tới thời điểm hiện tại */}
        <div>
          <div className="font-bold text-lg leading-tight">
            {formatVnd(actualSalary)}
          </div>
          <div className="text-[11px] text-blue-100 font-normal mt-0.5">
            Tới thời điểm hiện tại
          </div>
        </div>
      </div>

      <button className="w-7 h-7 rounded-full flex items-center justify-center text-white/80 hover:text-white hover:bg-white/10 transition-colors">
        <ChevronRight className="w-4 h-4" />
      </button>
    </div>
  );
};
