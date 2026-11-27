import { defineTheme } from '@astryxdesign/core/theme';
import { neutralTheme } from '@astryxdesign/theme-neutral/built';
import { brand, chart, hue, line, status, surface, text, type Pair } from './palette';

/** Manrope: đủ dấu tiếng Việt và có chữ số đều cột (`tabular-nums`) cho giờ công, số liệu. */
const fontStack = "'Manrope', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

/** Một bóng rất nhẹ, chỉ dành cho lớp nổi (dropdown, popover, modal, toast); chế độ tối thêm viền 1px để tách nền. */
const overlayShadow = '0 4px 12px light-dark(rgba(65, 40, 25, 0.14), rgba(0, 0, 0, 0.45)), inset 0 0 0 1px light-dark(transparent, #41362F)';
const ring = (color: string) => `inset 0 0 0 1px ${color}`;

/** Cặp [sáng, tối] → giá trị CSS dùng trong phần ghi đè component. */
const css = ([light, dark]: Pair) => `light-dark(${light}, ${dark})`;

/** `--color-{background,text,icon,border}-<tên>` cho từng họ màu nhãn. */
const hueTokens = Object.fromEntries(
  Object.entries(hue).flatMap(([name, c]) => [
    [`--color-background-${name}`, c.bg],
    [`--color-text-${name}`, c.text],
    [`--color-icon-${name}`, c.text],
    [`--color-border-${name}`, c.text],
  ]),
) as Record<`--color-${'background' | 'text' | 'icon' | 'border'}-${keyof typeof hue}`, Pair>;

/**
 * Theme FlexiWork: kế thừa theme "neutral" của Astryx. Tông kem và đỏ gạch lấy từ logo; thẻ bo góc
 * lớn, bóng nhẹ (tham khảo bộ UI "HRDashboard" trên Figma Community), bảng dữ liệu giữ mật độ gọn.
 * Mã màu nằm ở `palette.ts`.
 */
export const flexiworkTheme = defineTheme({
  name: 'flexiwork',
  extends: neutralTheme,
  tokens: {
    // ---------- Chữ
    '--font-family-body': fontStack,
    '--font-family-heading': fontStack,
    '--text-heading-1-weight': '700',
    '--text-heading-2-weight': '700',
    '--text-heading-3-weight': '700',
    '--text-heading-1-size': '1.5rem',
    '--text-heading-1-leading': '1.4',
    '--text-heading-2-size': '1.25rem',
    '--text-heading-2-leading': '1.4118',
    '--text-heading-3-size': '1.125rem',
    '--text-heading-3-leading': '1.4667',

    // ---------- Bề mặt
    '--color-background-body': surface.body,
    '--color-background-surface': surface.card,
    '--color-background-card': surface.card,
    '--color-background-popover': surface.popover,
    '--color-background-muted': surface.header,
    '--color-overlay-hover': surface.hover,
    '--color-overlay-pressed': surface.pressed,
    '--color-neutral': surface.neutral,
    '--color-skeleton': surface.header,
    '--color-track': line.subtle,

    // ---------- Chữ và biểu tượng
    '--color-text-primary': text.primary,
    '--color-text-secondary': text.secondary,
    '--color-text-disabled': text.disabled,
    '--color-icon-primary': text.primary,
    '--color-icon-secondary': text.secondary,
    '--color-icon-disabled': text.disabled,

    // ---------- Viền
    '--color-border': line.subtle,
    '--color-border-emphasized': line.control,

    // ---------- Màu nhấn
    '--color-accent': brand.accent,
    '--color-on-accent': brand.onAccent,
    '--color-accent-muted': surface.selected,
    '--color-text-accent': brand.accent,
    '--color-icon-accent': brand.accent,

    // ---------- Thông báo hệ thống
    '--color-success': status.success.fill,
    '--color-on-success': status.success.on,
    '--color-success-muted': hue.green.bg,
    '--color-on-success-muted': hue.green.text,
    '--color-text-success': status.success.text,
    '--color-warning': status.warning.fill,
    '--color-on-warning': status.warning.on,
    '--color-warning-muted': hue.yellow.bg,
    '--color-on-warning-muted': hue.yellow.text,
    '--color-text-warning': status.warning.text,
    '--color-error': status.error.fill,
    '--color-on-error': status.error.on,
    '--color-error-muted': hue.red.bg,
    '--color-on-error-muted': hue.red.text,
    '--color-text-error': status.error.text,
    '--color-info': brand.accent,
    '--color-on-info': brand.onAccent,
    '--color-info-muted': hue.blue.bg,
    '--color-on-info-muted': hue.blue.text,
    '--color-text-info': hue.blue.text,

    // ---------- Họ màu nhãn
    ...hueTokens,

    // ---------- Biểu đồ
    '--color-data-categorical-blue': chart.blue,
    '--color-data-categorical-orange': chart.orange,
    '--color-data-categorical-teal': chart.teal,
    '--color-data-categorical-pink': chart.pink,
    '--color-data-categorical-brown': chart.brown,
    '--color-data-categorical-cyan': chart.cyan,
    '--color-data-categorical-green': status.success.fill,
    '--color-data-categorical-red': status.error.fill,
    '--color-data-categorical-purple': chart.brown,
    '--color-data-categorical-indigo': chart.neutral,
    '--color-data-neutral': chart.neutral,

    // ---------- Bo góc và chiều cao control
    '--radius-inner': '6px',
    '--radius-element': '10px',
    '--radius-container': '16px',
    '--radius-page': '16px',
    '--radius-chat': '16px',
    '--size-element-sm': '32px',
    '--size-element-md': '36px',
    '--size-element-lg': '44px',

    // ---------- Bóng
    '--shadow-low': '0 0 0 0 transparent',
    '--shadow-med': overlayShadow,
    '--shadow-high': overlayShadow,
    '--shadow-inset-hover': ring(css(line.control)),
    '--shadow-inset-selected': ring(css(brand.accent)),
    '--shadow-inset-success': ring(css(status.success.text)),
    '--shadow-inset-warning': ring(css(status.warning.text)),
    '--shadow-inset-error': ring(css(status.error.text)),
  },

  components: {
    // Thẻ viền mảnh, bóng rất nhẹ
    card: { base: { borderColor: css(line.subtle), boxShadow: '0 1px 2px light-dark(rgba(92, 58, 30, 0.05), transparent), 0 8px 24px light-dark(rgba(92, 58, 30, 0.05), transparent)' } },
    // Khung: sidebar và thanh trên cùng một màu bề mặt
    // Kiểu "section" mặc định sơn cả vùng nội dung bằng màu card — trả về màu nền trang để card/bảng tách lớp
    'app-shell': { base: { '--fw-chrome': css(surface.chrome) }, 'variant:section': { backgroundColor: css(surface.body) } },
    'app-shell-header': { base: { backgroundColor: css(surface.chrome) } },
    'app-shell-sidenav': { base: { backgroundColor: css(surface.chrome) } },
    'side-nav': { base: { backgroundColor: css(surface.chrome) } },
    'side-nav-item': {
      base: { minHeight: '44px', borderRadius: '10px', fontWeight: '600' },
      selected: { boxShadow: '0 8px 18px light-dark(rgba(129, 28, 22, 0.25), transparent)', backgroundColor: css(brand.accent), color: css(brand.onAccent), fontWeight: '600', '--color-text-primary': css(brand.onAccent), '--color-icon-primary': css(brand.onAccent), '--color-icon-secondary': css(brand.onAccent), '--color-text-secondary': css(brand.onAccent) },
    },

    // Bảng: tiêu đề có nền riêng, chữ số đều cột; độ cao hàng chọn bằng prop `density` của từng bảng
    'table-header-cell': {
      base: { backgroundColor: 'var(--color-background-muted)', fontSize: '0.8125rem', fontWeight: '700', color: 'var(--color-text-secondary)' },
    },
    'table-cell': {
      base: { fontVariantNumeric: 'tabular-nums' },
    },

    badge: { base: { borderRadius: '8px', fontWeight: '600', paddingInline: '10px', minHeight: '24px' } },
    // Theme neutral dùng màu viền control làm nền thanh tiến độ — trả về màu nhạt để phần đã đạt nổi rõ
    'progress-bar': { base: { '--color-background-muted': css(line.subtle) } },
  },
});
