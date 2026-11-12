import { defineTheme } from '@astryxdesign/core/theme';
import { neutralTheme } from '@astryxdesign/theme-neutral/built';

/** Be Vietnam Pro: font thiết kế cho tiếng Việt (Figtree của theme gốc không có bộ dấu tiếng Việt). */
const fontStack = "'Be Vietnam Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

/**
 * Theme FlexiWork: kế thừa theme "neutral" của Astryx (xám trung tính), thay màu nhấn (accent)
 * bằng xanh thương hiệu và font chữ hỗ trợ tiếng Việt. Màu dạng cặp [sáng, tối] để tự đổi theo
 * chế độ hiển thị.
 */
export const flexiworkTheme = defineTheme({
  name: 'flexiwork',
  extends: neutralTheme,
  tokens: {
    '--color-accent': ['#2563EB', '#60A5FA'],
    '--font-family-body': fontStack,
    '--font-family-heading': fontStack,
  },
});
