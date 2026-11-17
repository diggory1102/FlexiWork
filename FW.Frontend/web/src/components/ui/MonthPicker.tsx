import { IconButton } from '@astryxdesign/core/IconButton';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { ChevronLeft, ChevronRight } from 'lucide-react';

export interface YearMonth {
  year: number;
  month: number;
}

/** Tháng hiện tại theo giờ máy người dùng. */
export function currentYearMonth(): YearMonth {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

/** Cộng / trừ tháng (có xử lý qua năm). */
export function shiftMonth({ year, month }: YearMonth, delta: number): YearMonth {
  const index = year * 12 + (month - 1) + delta;
  return { year: Math.floor(index / 12), month: (index % 12) + 1 };
}

export const compareMonth = (a: YearMonth, b: YearMonth) => a.year * 12 + a.month - (b.year * 12 + b.month);

/** Bộ chọn tháng: "‹ Tháng 11/2026 ›". Dùng cho bảng công, bảng lương, báo cáo theo tháng. */
export function MonthPicker({ value, onChange, max }: { value: YearMonth; onChange: (value: YearMonth) => void; max?: YearMonth }) {
  const atMax = max ? compareMonth(value, max) >= 0 : false;
  return (
    <HStack gap={1} vAlign="center">
      <IconButton label="Tháng trước" tooltip="Tháng trước" variant="secondary" icon={<Icon icon={ChevronLeft} />} onClick={() => onChange(shiftMonth(value, -1))} />
      <Text weight="semibold" className="min-w-28 text-center tabular-nums">
        Tháng {String(value.month).padStart(2, '0')}/{value.year}
      </Text>
      <IconButton
        label="Tháng sau"
        tooltip="Tháng sau"
        variant="secondary"
        icon={<Icon icon={ChevronRight} />}
        isDisabled={atMax}
        onClick={() => onChange(shiftMonth(value, 1))}
      />
    </HStack>
  );
}
