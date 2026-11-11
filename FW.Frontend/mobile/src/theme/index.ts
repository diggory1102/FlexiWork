/** Bảng màu & kích thước dùng chung — đồng bộ với token màu của Web Admin (index.css). */
export const colors = {
  primary: '#2563EB',
  primaryDark: '#1D4ED8',
  primarySoft: '#EFF4FF',
  header: '#181D31',
  surface: '#FFFFFF',
  background: '#F4F6FA',
  foreground: '#0F172A',
  muted: '#64748B',
  border: '#E2E8F0',
  success: '#16A34A',
  successSoft: '#ECFDF3',
  warning: '#D97706',
  warningSoft: '#FFF7E6',
  danger: '#DC2626',
  dangerSoft: '#FEF2F2',
  white: '#FFFFFF',
} as const;

export const radius = { sm: 8, md: 12, lg: 16, full: 999 } as const;

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 } as const;
