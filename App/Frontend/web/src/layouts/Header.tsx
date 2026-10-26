import React from 'react';
import { Menu, Headphones, MessageSquare, Bell } from 'lucide-react';

interface HeaderProps {
  onToggleSidebar?: () => void;
}

export const Header: React.FC<HeaderProps> = ({ onToggleSidebar }) => {
  return (
    <header className="bg-[#202538] text-white h-14 flex items-center justify-between px-4 z-30 sticky top-0 border-b border-slate-700 shadow-xs shrink-0">
      {/* Left Section: Logo & Toggle & Current Section */}
      <div className="flex items-center space-x-6">
        <div className="flex items-center space-x-2.5">
          <div className="w-8 h-8 rounded-lg bg-white/95 p-1 flex items-center justify-center shadow-xs overflow-hidden">
            <img
              src="/logo.png"
              alt="FlexiWork Logo"
              className="w-full h-full object-contain"
            />
          </div>
          <span className="text-lg font-bold tracking-tight text-white">
            FlexiWork
          </span>
        </div>

        <button
          onClick={onToggleSidebar}
          className="text-slate-300 hover:text-white transition-colors focus:outline-none px-1"
          title="Thu gọn menu"
        >
          <Menu className="w-5 h-5" />
        </button>

        <h1 className="text-[15px] font-semibold text-slate-100 flex items-center">
          Tổng quan
        </h1>
      </div>

      {/* Right Section: Actions & Profile */}
      <div className="flex items-center space-x-3.5">
        <button className="bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white text-xs font-semibold px-3 py-1.5 rounded shadow-xs transition-all">
          Bảng giá
        </button>

        <button
          className="w-8 h-8 rounded-md bg-[#f77f00] text-white flex items-center justify-center hover:opacity-90 transition-opacity"
          title="Hỗ trợ chăm sóc"
        >
          <Headphones className="w-4 h-4" />
        </button>

        <button
          className="w-8 h-8 rounded-full text-slate-300 hover:text-white hover:bg-slate-700/50 flex items-center justify-center transition-colors"
          title="Tin nhắn"
        >
          <MessageSquare className="w-4 h-4" />
        </button>

        <div className="relative">
          <button
            className="w-8 h-8 rounded-full text-slate-300 hover:text-white hover:bg-slate-700/50 flex items-center justify-center transition-colors"
            title="Thông báo"
          >
            <Bell className="w-4 h-4" />
          </button>
          <span className="absolute -top-1 -right-1 bg-red-500 text-white text-[10px] font-bold w-4 h-4 rounded-full flex items-center justify-center border-2 border-[#202538]">
            6
          </span>
        </div>

        <div className="flex items-center pl-1 cursor-pointer">
          <div className="w-8 h-8 rounded-full ring-2 ring-slate-600 bg-indigo-600 flex items-center justify-center font-bold text-xs text-white">
            AD
          </div>
        </div>
      </div>
    </header>
  );
};
