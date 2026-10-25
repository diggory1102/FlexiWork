import React, { useState } from 'react';
import { Camera, MapPin, Wifi, CheckCircle2, ShieldCheck, UserCheck } from 'lucide-react';
import { useAppStore } from '../ViewModels/useAppStore';

export const MobileCheckInView: React.FC = () => {
  const { currentUser, currentBranch } = useAppStore();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [checkinSuccess, setCheckinSuccess] = useState<boolean | null>(null);

  const handleCheckin = () => {
    setIsSubmitting(true);
    // Giả lập luồng đối soát đa lớp
    setTimeout(() => {
      setIsSubmitting(false);
      setCheckinSuccess(true);
    }, 1500);
  };

  return (
    <div className="min-h-screen bg-slate-900 text-white flex flex-col justify-between max-w-md mx-auto shadow-2xl relative overflow-hidden">
      {/* Top Header */}
      <div className="p-5 pt-8 bg-slate-800/80 backdrop-blur-md border-b border-slate-700/50 flex justify-between items-center">
        <div>
          <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
            FLEXIWORK MOBILE
          </span>
          <h1 className="text-lg font-bold mt-1 text-slate-100">{currentUser?.fullName}</h1>
          <p className="text-xs text-slate-400">Mã NV: {currentUser?.employeeCode} • Ca sáng</p>
        </div>
        <div className="h-10 w-10 rounded-full bg-gradient-to-tr from-indigo-500 to-purple-500 flex items-center justify-center font-bold text-white shadow-lg shadow-indigo-500/30">
          NV
        </div>
      </div>

      {/* Main Content: Scanner & Multi-layer Status */}
      <div className="p-5 flex-1 flex flex-col items-center justify-center">
        {/* Camera Face Box */}
        <div className="relative w-64 h-72 rounded-3xl bg-slate-800 border-2 border-dashed border-indigo-500/60 flex flex-col items-center justify-center overflow-hidden shadow-inner group">
          <div className="absolute inset-x-0 h-1 bg-gradient-to-r from-transparent via-indigo-400 to-transparent animate-pulse top-10"></div>
          
          <Camera className="w-16 h-16 text-indigo-400/80 mb-3" />
          <p className="text-xs text-slate-300 font-medium px-4 text-center">
            {isSubmitting ? "Đang nhận diện khuôn mặt..." : "Đưa khuôn mặt vào khung hình để điểm danh"}
          </p>

          <div className="absolute bottom-3 flex items-center space-x-1.5 px-3 py-1 bg-slate-950/70 rounded-full text-[11px] text-emerald-400 font-medium">
            <ShieldCheck className="w-3.5 h-3.5" />
            <span>InsightFace ArcFace 512D</span>
          </div>
        </div>

        {/* 3 Layer Status Badges */}
        <div className="w-full grid grid-cols-3 gap-2.5 mt-6">
          {/* Layer 1: Face ID */}
          <div className="bg-slate-800/70 p-3 rounded-2xl border border-slate-700/60 flex flex-col items-center text-center">
            <UserCheck className="w-5 h-5 text-indigo-400 mb-1" />
            <span className="text-[11px] text-slate-400 font-medium">Lớp 1: Mặt</span>
            <span className="text-xs font-semibold text-emerald-400 mt-0.5">Sẵn sàng</span>
          </div>

          {/* Layer 2: GPS Geofence */}
          <div className="bg-slate-800/70 p-3 rounded-2xl border border-slate-700/60 flex flex-col items-center text-center">
            <MapPin className="w-5 h-5 text-amber-400 mb-1" />
            <span className="text-[11px] text-slate-400 font-medium">Lớp 2: GPS</span>
            <span className="text-xs font-semibold text-emerald-400 mt-0.5">Cách 18m</span>
          </div>

          {/* Layer 3: Wi-Fi BSSID */}
          <div className="bg-slate-800/70 p-3 rounded-2xl border border-slate-700/60 flex flex-col items-center text-center">
            <Wifi className="w-5 h-5 text-sky-400 mb-1" />
            <span className="text-[11px] text-slate-400 font-medium">Lớp 3: Wi-Fi</span>
            <span className="text-xs font-semibold text-emerald-400 mt-0.5">Đã khớp</span>
          </div>
        </div>

        {/* Current Branch info */}
        <div className="w-full mt-4 p-3.5 bg-slate-800/40 rounded-xl border border-slate-700/40 text-xs flex items-center space-x-3">
          <div className="p-2 bg-indigo-500/10 rounded-lg text-indigo-400">
            <MapPin className="w-4 h-4" />
          </div>
          <div>
            <div className="font-semibold text-slate-200">{currentBranch?.name}</div>
            <div className="text-[11px] text-slate-400 truncate max-w-[240px]">{currentBranch?.address}</div>
          </div>
        </div>

        {/* Checkin Success Notification */}
        {checkinSuccess && (
          <div className="w-full mt-3 p-3 bg-emerald-500/20 border border-emerald-500/40 rounded-xl flex items-center space-x-2.5 text-emerald-300 text-xs">
            <CheckCircle2 className="w-5 h-5 flex-shrink-0" />
            <span>Check-in thành công lúc <strong>07:55 AM</strong> (Hợp lệ 3 lớp). Chúc bạn làm việc vui vẻ!</span>
          </div>
        )}
      </div>

      {/* Bottom Action Button */}
      <div className="p-5 bg-slate-900/90 border-t border-slate-800/80 backdrop-blur-md">
        <button
          onClick={handleCheckin}
          disabled={isSubmitting}
          className="w-full py-3.5 rounded-2xl font-semibold bg-gradient-to-r from-indigo-500 to-indigo-600 hover:from-indigo-600 hover:to-indigo-700 active:scale-[0.98] transition-all text-white shadow-lg shadow-indigo-500/25 flex items-center justify-center space-x-2 text-sm disabled:opacity-50"
        >
          {isSubmitting ? (
            <span>Đang xác thực điểm danh...</span>
          ) : (
            <>
              <Camera className="w-4 h-4" />
              <span>BẤM ĐỂ CHỤP VÀ CHECK-IN NGAY</span>
            </>
          )}
        </button>
      </div>
    </div>
  );
};
