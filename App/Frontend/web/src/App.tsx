import React, { useState } from 'react';
import { MobileCheckInView } from './Views/MobileCheckInView';
import { DashboardView } from './Views/DashboardView';
import { Smartphone, Monitor } from 'lucide-react';

export const App: React.FC = () => {
  const [viewMode, setViewMode] = useState<'desktop' | 'mobile'>('desktop');

  return (
    <div className="min-h-screen bg-slate-900 font-sans text-slate-100 flex flex-col">
      {/* Top Demo Bar Switcher */}
      <header className="bg-[#202538] border-b border-slate-700/80 px-5 py-2.5 flex justify-between items-center z-50 shadow-md">
        <div className="flex items-center space-x-4">
          {/* Logo HRM FlexiWork */}
          <div className="flex flex-col justify-center leading-tight">
            <div className="flex items-baseline space-x-1">
              <span className="text-xl font-black tracking-wider text-white">HRM</span>
              <span className="text-xs font-bold text-indigo-400">FlexiWork</span>
            </div>
            <span className="text-[9px] text-slate-400 font-medium tracking-tight -mt-0.5">
              Powered by <strong className="text-slate-200">DATN .NET 8 + Dapper</strong>
            </span>
          </div>

          <div className="h-4 w-[1px] bg-slate-700 hidden sm:block"></div>

          <span className="text-xs font-semibold text-slate-200 hidden sm:inline-block">
            {viewMode === 'desktop' ? 'Tổng quan Quản trị (Desktop)' : 'Ứng dụng Điểm danh (Mobile)'}
          </span>
        </div>

        {/* Nút chuyển đổi view mode */}
        <div className="flex bg-slate-800/80 p-1 rounded-xl border border-slate-700 text-xs">
          <button
            onClick={() => setViewMode('desktop')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg font-medium transition-all ${
              viewMode === 'desktop'
                ? 'bg-blue-600 text-white shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Monitor className="w-3.5 h-3.5" />
            <span>Giao diện Quản trị Web HRM</span>
          </button>

          <button
            onClick={() => setViewMode('mobile')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg font-medium transition-all ${
              viewMode === 'mobile'
                ? 'bg-indigo-600 text-white shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Smartphone className="w-3.5 h-3.5" />
            <span>Giao diện Mobile (Nhân viên)</span>
          </button>
        </div>
      </header>

      {/* Main Body Container */}
      <main className="flex-1 flex overflow-hidden">
        {viewMode === 'desktop' ? (
          <DashboardView />
        ) : (
          <div className="flex-1 overflow-y-auto bg-slate-950 flex flex-col items-center justify-center p-4">
            <div className="text-xs text-slate-400 mb-3 text-center">
              Mô phỏng app chấm công (Có thể mở trực tiếp bằng điện thoại thật qua cùng mạng Wi-Fi)
            </div>
            <MobileCheckInView />
          </div>
        )}
      </main>
    </div>
  );
};

