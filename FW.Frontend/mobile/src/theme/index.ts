/**
 * Bảng màu & kích thước dùng chung — theo chế độ sáng của Web Admin (`web/src/theme/palette.ts`):
 * tông kem và đỏ gạch lấy từ logo. Đổi màu ở web thì đổi theo ở đây.
 */
export const colors = {
  // Màu nhấn duy nhất
  primary: '#811C16',
  primarySoft: '#F3DED5',
  primaryBorder: '#E2B6A8',
  onPrimary: '#FDF6E9',

  // Bề mặt: thanh điều hướng → nền trang → thẻ, ô nhập
  chrome: '#F1E9D6',
  background: '#F8F4EA',
  surface: '#FFFDF8',

  foreground: '#241A16',
  muted: '#5E4F45',
  placeholder: '#A3968A',
  /** Viền thẻ, đường kẻ (trang trí). */
  border: '#E3D9C3',
  /** Viền ô nhập và control: đạt ≥ 3:1. */
  borderStrong: '#8C7C68',

  // Dải thương hiệu (thẻ đồng hồ, thẻ số liệu nổi bật)
  band: '#811C16',
  onBand: '#FDF6E9',
  onBandMuted: 'rgba(253,246,233,0.82)',

  // Trạng thái: màu chữ / biểu tượng đặt trên nền "Soft" tương ứng
  info: '#1E429F',
  infoSoft: '#DBE6FD',
  success: '#175C2C',
  successSoft: '#D8F0DC',
  successBorder: '#AEDCB6',
  warning: '#664700',
  warningSoft: '#FCEBA3',
  warningBorder: '#F0B400',
  danger: '#B91C1C',
  dangerSoft: '#FBDCDA',

  disabledText: '#BFB3A3',
  backdrop: 'rgba(36,26,22,0.45)',
  white: '#FFFFFF',
} as const;

/** Họ màu để phân biệt nhóm chức năng (biểu tượng) — trùng `hue` của web; không dùng màu tím. */
export const hues = {
  blue: '#1E429F',
  orange: '#92340B',
  teal: '#0F5A63',
  pink: '#8A1650',
  green: '#175C2C',
  brown: '#433C33',
  cyan: '#075379',
} as const;

export const radius = { sm: 8, md: 10, lg: 16, full: 999 } as const;

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 } as const;
