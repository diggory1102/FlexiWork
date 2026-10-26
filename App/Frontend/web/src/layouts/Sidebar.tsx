import React from 'react';
import {
  CalendarCheck,
  Clock,
  Users,
  UserPlus,
  FileText,
  CalendarDays,
  Fingerprint,
  CheckCircle2,
  Megaphone,
  Banknote,
  BarChart3,
  Calculator,
  Shapes,
  Settings,
  ChevronDown
} from 'lucide-react';

export const Sidebar: React.FC = () => {
  return (
    <aside className="w-[210px] bg-white border-r border-slate-200 flex flex-col justify-between overflow-y-auto shrink-0 select-none text-[13px] font-medium py-2 shadow-xs">
      <nav className="space-y-0.5">
        {/* Tổng quan (Active) */}
        <a
          href="#"
          className="flex items-center px-4 py-2.5 text-blue-600 bg-blue-50/80 font-semibold border-r-2 border-blue-600"
        >
          <CalendarCheck className="w-4 h-4 mr-2.5 text-blue-600" />
          <span>Tổng quan</span>
        </a>

        {/* Yêu cầu chờ duyệt */}
        <a
          href="#"
          className="flex items-center px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <Clock className="w-4 h-4 mr-2.5 text-slate-500" />
          <span>Yêu cầu chờ duyệt</span>
        </a>

        {/* Nhân viên */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <Users className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Nhân viên</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Tuyển dụng */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <UserPlus className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Tuyển dụng</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Hợp đồng */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <FileText className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Hợp đồng</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Lịch làm việc */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <CalendarDays className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Lịch làm việc</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Chấm công */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <Fingerprint className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Chấm công</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Công việc */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <CheckCircle2 className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Công việc</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Truyền thông nội bộ */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <Megaphone className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Truyền thông nội bộ</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Lương */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <Banknote className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Lương</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Báo cáo */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <BarChart3 className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Báo cáo</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Cấu hình tính lương */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <Calculator className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Cấu hình tính lương</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Danh mục */}
        <a
          href="#"
          className="flex items-center justify-between px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <div className="flex items-center">
            <Shapes className="w-4 h-4 mr-2.5 text-slate-500" />
            <span>Danh mục</span>
          </div>
          <ChevronDown className="w-3 h-3 text-slate-400" />
        </a>

        {/* Cài đặt */}
        <a
          href="#"
          className="flex items-center px-4 py-2 text-slate-600 hover:bg-slate-50 hover:text-slate-900 transition-colors"
        >
          <Settings className="w-4 h-4 mr-2.5 text-slate-500" />
          <span>Cài đặt</span>
        </a>
      </nav>
    </aside>
  );
};
