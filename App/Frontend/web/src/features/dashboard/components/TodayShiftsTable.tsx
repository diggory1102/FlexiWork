import React from 'react';
import { TodayShiftItem } from '../types';

interface TodayShiftsTableProps {
  shifts: TodayShiftItem[];
}

export const TodayShiftsTable: React.FC<TodayShiftsTableProps> = ({ shifts }) => {
  const getStatusBadge = (status: TodayShiftItem['status']) => {
    switch (status) {
      case 'checked_in':
        return (
          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-700">
            Đã Check-in
          </span>
        );
      case 'upcoming':
        return (
          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-200 text-slate-600">
            Sắp bắt đầu
          </span>
        );
      case 'not_started':
      default:
        return (
          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-200 text-slate-600">
            Chưa diễn ra
          </span>
        );
    }
  };

  return (
    <div className="bg-white rounded-xl border border-slate-200/80 shadow-xs p-5 min-h-[385px] flex flex-col justify-between">
      <div>
        <div className="flex justify-between items-center pb-3 border-b border-slate-100">
          <h3 className="font-bold text-sm text-slate-800">Lịch phân ca hôm nay</h3>
          <span className="text-[11px] font-semibold text-blue-600 bg-blue-50 px-2 py-0.5 rounded">
            {shifts.length} Nhân sự
          </span>
        </div>

        {/* Danh sách ca kíp thực tế từ flexiwork_seed.sql */}
        <div className="mt-3 space-y-3 text-xs">
          {shifts.map((item) => (
            <div
              key={item.id}
              className="p-3 bg-slate-50/80 rounded-xl border border-slate-100 flex justify-between items-center"
            >
              <div>
                <div className="font-semibold text-slate-800">
                  {item.employeeName} ({item.employeeCode})
                </div>
                <div className="text-slate-500 text-[11px] mt-0.5">
                  {item.shiftName}: {item.shiftHours} ({item.coefficient} công)
                </div>
              </div>
              {getStatusBadge(item.status)}
            </div>
          ))}
        </div>
      </div>

      <div className="pt-3 border-t border-slate-100 text-center">
        <a href="#" className="text-xs font-semibold text-blue-600 hover:underline">
          Xem toàn bộ lịch tuần & phân ca &rarr;
        </a>
      </div>
    </div>
  );
};
