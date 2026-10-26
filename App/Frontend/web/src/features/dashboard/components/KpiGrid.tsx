import React from 'react';
import { CheckCircle2 } from 'lucide-react';
import { DashboardKPIs } from '../types';

interface KpiGridProps {
  kpis: DashboardKPIs;
}

export const KpiGrid: React.FC<KpiGridProps> = ({ kpis }) => {
  return (
    <div className="space-y-4">
      {/* HÀNG CHỈ SỐ 1: Đi muộn, Về sớm, Quên Checkin, Quên Checkout */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-amber-500">{kpis.lateCheckins}</span>
          <span className="text-xs text-slate-500 mt-1 font-medium">Đi muộn</span>
        </div>
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-slate-800">{kpis.earlyCheckouts}</span>
          <span className="text-xs text-slate-500 mt-1 font-medium">Về sớm</span>
        </div>
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-slate-800">{kpis.forgotCheckin}</span>
          <span className="text-xs text-slate-500 mt-1 font-medium">Quên check-in</span>
        </div>
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-slate-800">{kpis.forgotCheckout}</span>
          <span className="text-xs text-slate-500 mt-1 font-medium">Quên check-out</span>
        </div>
      </div>

      {/* HÀNG CHỈ SỐ 2: Nghỉ phép, Không phép, Ca phát sinh, Trùng thiết bị */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-slate-800">{kpis.leaveWithPermission}</span>
          <span className="text-xs text-slate-500 mt-1 font-medium">Nghỉ phép</span>
        </div>
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-slate-800">{kpis.leaveWithoutPermission}</span>
          <span className="text-xs text-slate-500 mt-1 font-medium">Nghỉ không phép</span>
        </div>
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-slate-800">{kpis.unexpectedShifts}</span>
          <span className="text-xs text-slate-500 mt-1 font-medium">Ca phát sinh</span>
        </div>
        <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <span className="text-2xl font-bold text-emerald-600">{kpis.deviceDuplicates}</span>
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
  );
};
