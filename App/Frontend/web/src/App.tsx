import React, { useState } from 'react';
import { MobileCheckInView } from './Views/MobileCheckInView';
import { Smartphone, Monitor } from 'lucide-react';
import { useAppStore } from './ViewModels/useAppStore';

export const App: React.FC = () => {
  const [viewMode, setViewMode] = useState<'mobile' | 'desktop'>('mobile');
  const { currentBranch } = useAppStore();

  return (
    <div className="min-h-screen bg-slate-950 font-sans text-slate-100 flex flex-col">
      {/* Top Demo Bar Switcher */}
      <header className="bg-slate-900 border-b border-slate-800 px-6 py-3 flex justify-between items-center z-50">
        <div className="flex items-center space-x-3">
          <div className="h-8 w-8 rounded-lg bg-indigo-600 flex items-center justify-center font-bold text-white shadow-md">
            FW
          </div>
          <div>
            <h1 className="font-bold text-sm tracking-wide text-white">FlexiWork Live Demo</h1>
            <p className="text-[11px] text-slate-400">Microservices • Clean Architecture • Dapper</p>
          </div>
        </div>

        {/* Chuyển đổi qua lại giữa Màn hình Mobile Nhân Viên & Web Quản Lý */}
        <div className="flex bg-slate-800 p-1 rounded-xl border border-slate-700/60 text-xs">
          <button
            onClick={() => setViewMode('mobile')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg font-medium transition-all ${
              viewMode === 'mobile'
                ? 'bg-indigo-600 text-white shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Smartphone className="w-4 h-4" />
            <span>Màn hình App Điện Thoại (Nhân viên)</span>
          </button>

          <button
            onClick={() => setViewMode('desktop')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg font-medium transition-all ${
              viewMode === 'desktop'
                ? 'bg-indigo-600 text-white shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Monitor className="w-4 h-4" />
            <span>Màn hình Web Dashboard (Quản lý)</span>
          </button>
        </div>
      </header>

      {/* Body Content */}
      <main className="flex-1 flex items-center justify-center p-4">
        {viewMode === 'mobile' ? (
          <div className="w-full flex flex-col items-center">
            <div className="text-xs text-slate-400 mb-3 flex items-center space-x-1.5">
              <span>Đang mô phỏng giao diện Mobile (Có thể mở trên điện thoại thật bằng IP LAN)</span>
            </div>
            <MobileCheckInView />
          </div>
        ) : (
          /* Desktop Web Dashboard Preview */
          <div className="w-full max-w-5xl bg-slate-900 border border-slate-800 rounded-3xl p-8 shadow-2xl">
            <div className="flex justify-between items-center mb-6">
              <div>
                <h2 className="text-xl font-bold text-white">Bảng Điều Khiển Chi Nhánh ({currentBranch?.name})</h2>
                <p className="text-xs text-slate-400">Giám sát điểm danh thời gian thực và phân ca làm việc</p>
              </div>
              <span className="px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                Hệ thống hoạt động bình thường
              </span>
            </div>

            {/* Quick Metrics */}
            <div className="grid grid-cols-4 gap-4 mb-8">
              <div className="p-4 bg-slate-800/60 rounded-2xl border border-slate-700/50">
                <span className="text-xs text-slate-400 font-medium">Tổng nhân sự ca sáng</span>
                <div className="text-2xl font-bold text-white mt-1">12 / 12</div>
                <span className="text-[11px] text-emerald-400">Đã có mặt đủ 100%</span>
              </div>

              <div className="p-4 bg-slate-800/60 rounded-2xl border border-slate-700/50">
                <span className="text-xs text-slate-400 font-medium">Đúng giờ (On-time)</span>
                <div className="text-2xl font-bold text-indigo-400 mt-1">11</div>
                <span className="text-[11px] text-slate-400">Tỷ lệ 91.6%</span>
              </div>

              <div className="p-4 bg-slate-800/60 rounded-2xl border border-slate-700/50">
                <span className="text-xs text-slate-400 font-medium">Đi muộn (Late)</span>
                <div className="text-2xl font-bold text-amber-400 mt-1">1</div>
                <span className="text-[11px] text-amber-400">Có đơn giải trình</span>
              </div>

              <div className="p-4 bg-slate-800/60 rounded-2xl border border-slate-700/50">
                <span className="text-xs text-slate-400 font-medium">Khuôn mặt Face ID</span>
                <div className="text-2xl font-bold text-emerald-400 mt-1">100%</div>
                <span className="text-[11px] text-slate-400">ArcFace 512D</span>
              </div>
            </div>

            {/* Attendance Table Sample */}
            <div className="bg-slate-950/60 rounded-2xl border border-slate-800/80 overflow-hidden">
              <div className="p-4 bg-slate-800/40 border-b border-slate-800 font-semibold text-xs text-slate-300">
                Nhật Ký Điểm Danh Gần Nhất (Truy vấn tốc độ cao qua Micro-ORM Dapper)
              </div>
              <div className="p-4 divide-y divide-slate-800 text-xs">
                <div className="py-2.5 flex justify-between items-center">
                  <div className="flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-indigo-500/20 text-indigo-400 flex items-center justify-center font-bold">
                      NV
                    </div>
                    <div>
                      <div className="font-semibold text-slate-200">Lê Văn Nhân Viên (EMP003)</div>
                      <div className="text-[11px] text-slate-400">Check-in: 07:55 AM • Khoảng cách GPS: 18m</div>
                    </div>
                  </div>
                  <span className="px-2.5 py-1 rounded-full text-[11px] font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                    Hợp lệ (3 Lớp)
                  </span>
                </div>

                <div className="py-2.5 flex justify-between items-center">
                  <div className="flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-purple-500/20 text-purple-400 flex items-center justify-center font-bold">
                      QL
                    </div>
                    <div>
                      <div className="font-semibold text-slate-200">Trần Quản Lý (EMP002)</div>
                      <div className="text-[11px] text-slate-400">Check-in: 07:45 AM • Khoảng cách GPS: 5m</div>
                    </div>
                  </div>
                  <span className="px-2.5 py-1 rounded-full text-[11px] font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                    Hợp lệ (3 Lớp)
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
};
