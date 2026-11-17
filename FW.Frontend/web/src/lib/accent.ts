/**
 * Lớp Tailwind cho màu nhấn thương hiệu. Lưu ý: trong theme này `bg-accent` / `text-accent` của Tailwind trỏ tới màu CHỮ nhấn
 * (xám đen), còn xanh thương hiệu là biến `--color-accent` — nên dùng các lớp dưới đây (tên lớp ghi đủ để Tailwind quét được).
 */
export const accent = {
  /** Nền xanh thương hiệu (kèm `onBg` cho chữ trên nền đó). */
  bg: 'bg-[var(--color-accent)]',
  onBg: 'text-[var(--color-on-accent)]',
  /** Chữ / biểu tượng xanh thương hiệu. */
  text: 'text-[var(--color-accent)]',
  /** Nền xanh nhạt (chọn, hiện chưa đọc). */
  tint: 'bg-[color-mix(in_srgb,var(--color-accent)_10%,transparent)]',
  tintSoft: 'bg-[color-mix(in_srgb,var(--color-accent)_6%,transparent)]',
  focusBorder: 'focus:border-[var(--color-accent)]',
} as const;
