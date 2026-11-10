/** Logo FlexiWork: biểu tượng đồng hồ + dấu tích (chấm công) — thiết kế riêng, không dùng tài nguyên bên thứ ba. */
export function Logo({ compact = false, dark = false }: { compact?: boolean; dark?: boolean }) {
  return (
    <span className="flex items-center gap-2">
      <svg viewBox="0 0 32 32" className="h-8 w-8 shrink-0" aria-hidden="true">
        <rect width="32" height="32" rx="8" fill="rgb(var(--color-primary))" />
        <circle cx="16" cy="16" r="9" fill="none" stroke="white" strokeWidth="2.2" />
        <path d="M16 10.5V16l3.2 2" fill="none" stroke="white" strokeWidth="2.2" strokeLinecap="round" />
        <circle cx="24" cy="24" r="5.5" fill="#22c55e" stroke="rgb(var(--color-header))" strokeWidth="1.5" />
        <path d="M21.6 24l1.6 1.6 3-3.2" fill="none" stroke="white" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      {!compact && (
        <span className="leading-tight">
          <span className={`block text-base font-bold tracking-tight ${dark ? 'text-foreground' : 'text-white'}`}>FlexiWork</span>
          <span className={`block text-[10px] ${dark ? 'text-muted' : 'text-white/60'}`}>Quản lý ca & chấm công</span>
        </span>
      )}
    </span>
  );
}
