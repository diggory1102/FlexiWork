import React from 'react';

interface HeroCardProps {
  totalShifts: number;
  scheduledEmployees: number;
  checkedInEmployees: number;
}

export const HeroCard: React.FC<HeroCardProps> = ({
  totalShifts,
  scheduledEmployees,
  checkedInEmployees,
}) => {
  return (
    <div className="relative bg-gradient-to-r from-[#e3edf7] via-[#e8f1fc] to-[#eff6ff] rounded-xl p-5 shadow-xs border border-blue-100 overflow-hidden min-h-[220px] flex flex-col justify-between">
      <div>
        <h2 className="text-slate-700 text-sm font-semibold tracking-tight">Tổng ca làm việc</h2>
        <div className="text-5xl font-bold text-slate-800 my-1 font-sans">{totalShifts}</div>
      </div>

      {/* Stats breakdown */}
      <div className="space-y-1.5 z-10 text-xs font-medium text-slate-700">
        <div className="flex items-center space-x-2">
          <span className="font-bold text-slate-800 text-sm">{scheduledEmployees}</span>
          <span>Nhân viên được xếp lịch ca</span>
        </div>
        <div className="flex items-center space-x-2">
          <span className="font-bold text-emerald-600 text-sm">{checkedInEmployees}</span>
          <span>Nhân viên đã chấm công (Face ID + GPS)</span>
        </div>
      </div>

      {/* Vector SVG Minh Họa Nhân Viên Nhà Hàng / F&B */}
      <div className="absolute bottom-0 right-4 w-72 h-44 pointer-events-none hidden sm:flex items-end justify-end opacity-90">
        <svg className="w-full h-full object-contain" fill="none" viewBox="0 0 320 200">
          <path d="M50,190 Q50,60 140,60 Q230,60 230,190 Z" fill="#cce2f7" opacity="0.4"></path>
          <path d="M160,190 Q160,80 250,80 Q320,80 320,190 Z" fill="#bfdbfe" opacity="0.3"></path>
          <circle cx="140" cy="50" fill="#93c5fd" opacity="0.4" r="14"></circle>
          <circle cx="240" cy="65" fill="#93c5fd" opacity="0.4" r="10"></circle>
          {/* Waiter */}
          <ellipse cx="180" cy="185" fill="#1e293b" rx="36" ry="28"></ellipse>
          <path d="M175,150 L185,150 L188,185 L172,185 Z" fill="#ffffff"></path>
          <polygon fill="#ef4444" points="177,152 183,152 180,157"></polygon>
          <circle cx="180" cy="125" fill="#fcd34d" r="16"></circle>
          <path d="M166,120 Q180,105 194,120 Q190,110 180,110 Q170,110 166,120 Z" fill="#451a03"></path>
          {/* Tray & Cloche */}
          <line stroke="#fcd34d" strokeLinecap="round" strokeWidth="5" x1="140" x2="175" y1="140" y2="152"></line>
          <ellipse cx="138" cy="138" fill="#94a3b8" rx="22" ry="5"></ellipse>
          <path d="M120,137 Q138,110 156,137 Z" fill="#cbd5e1"></path>
          {/* Chef */}
          <ellipse cx="255" cy="180" fill="#ffffff" rx="28" ry="22" stroke="#e2e8f0"></ellipse>
          <circle cx="255" cy="142" fill="#fcd34d" r="14"></circle>
          <path d="M243,130 C240,115 250,110 255,110 C260,110 270,115 267,130 Z" fill="#ffffff"></path>
        </svg>
      </div>
    </div>
  );
};
