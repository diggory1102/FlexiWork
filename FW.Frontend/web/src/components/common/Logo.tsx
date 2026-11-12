/** Biểu tượng FlexiWork: đồng hồ + dấu tích (chấm công) — thiết kế riêng, màu theo token accent của theme. */
export function LogoMark({ size = 32 }: { size?: number }) {
  return (
    <svg viewBox="0 0 32 32" width={size} height={size} className="shrink-0" aria-hidden="true">
      <rect width="32" height="32" rx="8" fill="var(--color-accent)" />
      <circle cx="16" cy="16" r="9" fill="none" stroke="var(--color-on-accent)" strokeWidth="2.2" />
      <path d="M16 10.5V16l3.2 2" fill="none" stroke="var(--color-on-accent)" strokeWidth="2.2" strokeLinecap="round" />
      <circle cx="24" cy="24" r="5.5" fill="#22c55e" stroke="var(--color-background-surface)" strokeWidth="1.5" />
      <path d="M21.6 24l1.6 1.6 3-3.2" fill="none" stroke="white" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/** Logo đầy đủ (biểu tượng + tên + khẩu hiệu) — dùng ở trang đăng nhập. */
export function Logo({ size = 40 }: { size?: number }) {
  return (
    <span className="flex items-center gap-3">
      <LogoMark size={size} />
      <span className="leading-tight">
        <span className="block text-lg font-bold tracking-tight text-primary">FlexiWork</span>
        <span className="block text-xs text-secondary">Quản lý ca & chấm công</span>
      </span>
    </span>
  );
}
