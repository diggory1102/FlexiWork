import React, { useState } from 'react';
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
  Menu,
  RotateCw,
  Calendar as CalendarIcon,
  ChevronRight,
  Headphones,
  MessageSquare,
  Bell,
  ChevronDown
} from 'lucide-react';
import { useAppStore } from '../ViewModels/useAppStore';

export const DashboardView: React.FC = () => {
  const { currentBranch } = useAppStore();
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [selectedBranch, setSelectedBranch] = useState(currentBranch?.name || 'FlexiWork Cầu Giấy');

  const handleRefresh = () => {
    setIsRefreshing(true);
    setTimeout(() => setIsRefreshing(false), 800);
  };

  return (
    <div className="flex-1 flex overflow-hidden bg-[#f2f4f8] text-slate-700 antialiased font-sans">
      {/* ========================================================= */}
      {/* SIDEBAR NAVIGATION (Chuẩn HRM Style 205px) */}
      {/* ========================================================= */}
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

      {/* ========================================================= */}
      {/* MAIN DASHBOARD CONTENT */}
      {/* ========================================================= */}
      <main className="flex-1 overflow-y-auto p-4 md:p-5">
        {/* FILTER BAR */}
        <section className="flex flex-wrap items-center gap-2 mb-4">
          <div className="relative min-w-[210px]">
            <select
              value={selectedBranch}
              onChange={(e) => setSelectedBranch(e.target.value)}
              className="w-full bg-white border border-slate-300 rounded-md py-1.5 pl-3 pr-8 text-xs font-medium text-slate-700 shadow-xs focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="FlexiWork Cầu Giấy">FlexiWork Cầu Giấy (CN-HN-01)</option>
              <option value="FlexiWork Hoàn Kiếm">FlexiWork Hoàn Kiếm (CN-HN-02)</option>
            </select>
          </div>

          <div className="relative">
            <input
              type="text"
              defaultValue="25/10/2026"
              className="bg-white border border-slate-300 rounded-md py-1.5 pl-3 pr-8 text-xs font-medium text-slate-700 w-36 shadow-xs focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
            <CalendarIcon className="w-3.5 h-3.5 text-slate-400 absolute right-2.5 top-2.5 pointer-events-none" />
          </div>

          <button className="bg-[#1877f2] hover:bg-blue-600 text-white font-medium text-xs px-4 py-1.5 rounded-md shadow-xs transition-colors">
            Lọc
          </button>

          <button
            onClick={handleRefresh}
            className="w-8 h-8 bg-slate-600 hover:bg-slate-700 text-white rounded-md flex items-center justify-center shadow-xs transition-colors"
            title="Làm mới dữ liệu"
          >
            <RotateCw className={`w-3.5 h-3.5 ${isRefreshing ? 'animate-spin' : ''}`} />
          </button>
        </section>

        {/* DASHBOARD GRID 12 COLUMNS */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
          {/* CỘT TRÁI (7 COLS): TỔNG CA, CHỈ SỐ, CHECKLIST TASK */}
          <div className="lg:col-span-7 space-y-4">
            {/* HERO CARD: TỔNG CA LÀM VIỆC */}
            <div className="relative bg-gradient-to-r from-[#e3edf7] via-[#e8f1fc] to-[#eff6ff] rounded-xl p-5 shadow-xs border border-blue-100 overflow-hidden min-h-[220px] flex flex-col justify-between">
              <div>
                <h2 className="text-slate-700 text-sm font-semibold tracking-tight">Tổng ca làm việc</h2>
                <div className="text-5xl font-bold text-slate-800 my-1 font-sans">4</div>
              </div>

              {/* Stats breakdown */}
              <div className="space-y-1.5 z-10 text-xs font-medium text-slate-700">
                <div className="flex items-center space-x-2">
                  <span className="font-bold text-slate-800 text-sm">3</span>
                  <span>Nhân viên được xếp lịch ca</span>
                </div>
                <div className="flex items-center space-x-2">
                  <span className="font-bold text-emerald-600 text-sm">2</span>
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

            {/* HÀNG CHỈ SỐ 1: Đi muộn, Về sớm, Quên Checkin, Quên Checkout */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-amber-500">1</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Đi muộn</span>
              </div>
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-slate-800">0</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Về sớm</span>
              </div>
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-slate-800">0</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Quên check-in</span>
              </div>
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-slate-800">0</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Quên check-out</span>
              </div>
            </div>

            {/* HÀNG CHỈ SỐ 2: Nghỉ phép, Không phép, Ca phát sinh, Trùng thiết bị */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-slate-800">0</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Nghỉ phép</span>
              </div>
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-slate-800">0</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Nghỉ không phép</span>
              </div>
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-slate-800">0</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Ca phát sinh</span>
              </div>
              <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
                <span className="text-2xl font-bold text-emerald-600">0</span>
                <span className="text-xs text-slate-500 mt-1 font-medium">Trùng thiết bị</span>
              </div>
            </div>

            {/* CHECKLIST CÔNG VIỆC THEO CA */}
            <div className="bg-white p-5 rounded-xl border border-slate-200/80 shadow-xs flex items-center justify-between">
              <div className="space-y-1 pr-4">
                <h3 className="text-sm font-bold text-slate-800">Công việc theo ca (Shift Tasks)</h3>
                <p className="text-xs text-slate-500">
                  Đã cấu hình 4 đầu việc checklist bắt buộc cho Ca sáng & Ca tối (Mở cửa, kiểm két, bàn giao).
                </p>
                <div className="pt-2">
                  <a href="#" className="text-xs font-semibold text-blue-600 hover:text-blue-800 transition-colors inline-block">
                    Xem checklist ca trực &rarr;
                  </a>
                </div>
              </div>
              <div className="shrink-0 w-12 h-12 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center font-bold">
                <CheckCircle2 className="w-6 h-6" />
              </div>
            </div>
          </div>

          {/* CỘT PHẢI (5 COLS): BANNER TỔNG LƯƠNG & LỊCH LÀM VIỆC */}
          <div className="lg:col-span-5 space-y-4">
            {/* BANNER TỔNG LƯƠNG NGÀY (BLUE GRADIENT) */}
            <div className="bg-gradient-to-r from-[#0d6efd] via-[#1070fc] to-[#2080ff] text-white rounded-xl p-4 shadow-xs flex items-center justify-between">
              <div className="flex items-center space-x-8">
                {/* Lương ngày dự kiến */}
                <div>
                  <div className="flex items-center space-x-1.5 font-bold text-lg leading-tight">
                    <span>1.120.000đ</span>
                    <button
                      onClick={handleRefresh}
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
                    560.000đ
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

            {/* BẢNG LỊCH CA & ĐIỂM DANH HÔM NAY */}
            <div className="bg-white rounded-xl border border-slate-200/80 shadow-xs p-5 min-h-[385px] flex flex-col justify-between">
              <div>
                <div className="flex justify-between items-center pb-3 border-b border-slate-100">
                  <h3 className="font-bold text-sm text-slate-800">Lịch phân ca hôm nay</h3>
                  <span className="text-[11px] font-semibold text-blue-600 bg-blue-50 px-2 py-0.5 rounded">
                    3 Nhân sự
                  </span>
                </div>

                {/* Danh sách ca kíp thực tế từ flexiwork_seed.sql */}
                <div className="mt-3 space-y-3 text-xs">
                  {/* Item 1: Ca Sáng */}
                  <div className="p-3 bg-slate-50/80 rounded-xl border border-slate-100 flex justify-between items-center">
                    <div>
                      <div className="font-semibold text-slate-800">Lê Văn Nhân Viên (EMP003)</div>
                      <div className="text-slate-500 text-[11px] mt-0.5">Ca sáng chuẩn: 08:00 - 12:00 (1.0 công)</div>
                    </div>
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-700">
                      Đã Check-in
                    </span>
                  </div>

                  {/* Item 2: Ca Chiều */}
                  <div className="p-3 bg-slate-50/80 rounded-xl border border-slate-100 flex justify-between items-center">
                    <div>
                      <div className="font-semibold text-slate-800">Trần Quản Lý (EMP002)</div>
                      <div className="text-slate-500 text-[11px] mt-0.5">Ca chiều chuẩn: 13:00 - 17:00 (1.0 công)</div>
                    </div>
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-200 text-slate-600">
                      Sắp bắt đầu
                    </span>
                  </div>

                  {/* Item 3: Ca Đêm */}
                  <div className="p-3 bg-slate-50/80 rounded-xl border border-slate-100 flex justify-between items-center">
                    <div>
                      <div className="font-semibold text-slate-800">Nguyễn Quản Trị (EMP001)</div>
                      <div className="text-slate-500 text-[11px] mt-0.5">Ca đêm: 22:00 - 06:00 (1.5 công)</div>
                    </div>
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-200 text-slate-600">
                      Chưa diễn ra
                    </span>
                  </div>
                </div>
              </div>

              <div className="pt-3 border-t border-slate-100 text-center">
                <a href="#" className="text-xs font-semibold text-blue-600 hover:underline">
                  Xem toàn bộ lịch tuần & phân ca &rarr;
                </a>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
};
