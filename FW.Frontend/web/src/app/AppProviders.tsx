import type { ReactNode } from 'react';
import { Theme } from '@astryxdesign/core/theme';
import { InternationalizationProvider } from '@astryxdesign/core/i18n';
import viVN from '@astryxdesign/core/locales/vi-VN.generated.js';
import { flexiworkTheme } from '@/theme/flexiworkTheme';
import { useUiStore } from '@/stores/uiStore';

/**
 * Provider gốc của Web Admin:
 * - Theme: design token FlexiWork (kế thừa Astryx neutral) + chế độ sáng/tối người dùng chọn.
 * - InternationalizationProvider: chữ của component Astryx (phân trang, "Bắt buộc", "Đóng"…) bằng tiếng Việt.
 */
export function AppProviders({ children }: { children: ReactNode }) {
  const colorMode = useUiStore((s) => s.colorMode);
  return (
    <InternationalizationProvider locale="vi-VN" messages={{ 'vi-VN': viVN }}>
      <Theme theme={flexiworkTheme} mode={colorMode}>
        {children}
      </Theme>
    </InternationalizationProvider>
  );
}
