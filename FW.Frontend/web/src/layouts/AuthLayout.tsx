import type { ReactNode } from 'react';
import { Fingerprint, MapPin, Wifi } from 'lucide-react';
import { Logo } from '@/components/common/Logo';

/** Khung trang đăng nhập / quên mật khẩu: bên trái giới thiệu, bên phải form. */
export function AuthLayout({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="flex min-h-full">
      <section className="hidden w-[44%] flex-col justify-between bg-header p-10 text-white lg:flex">
        <Logo />
        <div>
          <h2 className="text-3xl font-bold leading-tight">
            Chấm công thông minh,
            <br />
            quản lý ca linh hoạt.
          </h2>
          <p className="mt-3 max-w-md text-sm text-white/70">
            Xác thực chấm công 3 lớp trên chính điện thoại của nhân viên — chống chấm công hộ và giả mạo vị trí.
          </p>
          <ul className="mt-8 space-y-3 text-sm">
            <Feature icon={<MapPin className="h-4 w-4" />} text="Định vị GPS Geofencing theo bán kính chi nhánh" />
            <Feature icon={<Wifi className="h-4 w-4" />} text="Đối soát Wi-Fi BSSID nội bộ chống Fake GPS" />
            <Feature icon={<Fingerprint className="h-4 w-4" />} text="Nhận diện khuôn mặt bằng AI chống chấm hộ" />
          </ul>
        </div>
        <p className="text-xs text-white/40">© {new Date().getFullYear()} FlexiWork — Khóa luận tốt nghiệp, Trường Đại học Thăng Long</p>
      </section>

      <section className="flex flex-1 items-center justify-center bg-background p-6">
        <div className="w-full max-w-sm">
          <div className="mb-6 lg:hidden">
            <Logo dark />
          </div>
          <h1 className="text-xl font-semibold text-foreground">{title}</h1>
          {subtitle && <p className="mt-1 text-sm text-muted">{subtitle}</p>}
          <div className="mt-6">{children}</div>
        </div>
      </section>
    </div>
  );
}

function Feature({ icon, text }: { icon: ReactNode; text: string }) {
  return (
    <li className="flex items-center gap-3">
      <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/10 text-white">{icon}</span>
      {text}
    </li>
  );
}
