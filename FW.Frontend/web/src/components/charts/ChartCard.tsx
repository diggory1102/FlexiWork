import type { ReactNode } from 'react';
import { Card } from '@astryxdesign/core/Card';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { EmptyState } from '@/components/ui/Feedback';

/**
 * Màu biểu đồ lấy từ design token của Astryx (CSS variable) nên tự đổi theo theme sáng / tối.
 * Dùng cho thuộc tính fill / stroke của Recharts (SVG hỗ trợ var()).
 */
export const chartColors = {
  accent: 'var(--color-accent)',
  success: 'var(--color-success)',
  warning: 'var(--color-warning)',
  error: 'var(--color-error)',
  info: 'var(--color-info)',
  purple: 'var(--color-text-purple)',
  grid: 'var(--color-border)',
  text: 'var(--color-text-secondary)',
} as const;

/** Thuộc tính chung cho trục XAxis / YAxis: chữ nhỏ, màu phụ, không vẽ vạch trục. */
export const axisProps = {
  tick: { fill: chartColors.text, fontSize: 12 },
  tickLine: false,
  axisLine: false,
} as const;

/** Thuộc tính chung cho Legend: chấm tròn, chữ nhỏ màu phụ (mặc định của Recharts tô chữ theo màu dãy dữ liệu). */
export const legendProps = {
  iconType: 'circle',
  iconSize: 8,
  formatter: (value: string) => <span style={{ color: chartColors.text, fontSize: 12 }}>{value}</span>,
} as const;

interface TooltipEntry {
  name?: string | number;
  value?: string | number | readonly (string | number)[];
  color?: string;
}

/** Khung chú thích khi rê chuột lên biểu đồ (thay tooltip mặc định của Recharts để đồng bộ màu theme). */
export function ChartTooltip({
  active,
  payload,
  label,
  formatValue,
}: {
  active?: boolean;
  payload?: readonly TooltipEntry[];
  label?: string | number;
  formatValue?: (value: number, name: string) => string;
}) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border border-border bg-surface px-3 py-2 shadow-md">
      {label !== undefined && <Text weight="medium">{label}</Text>}
      <VStack gap={0.5} className="mt-1">
        {payload.map((p) => (
          <HStack key={String(p.name)} gap={2} vAlign="center">
            <span className="inline-block size-2.5 rounded-full" style={{ background: p.color }} />
            <Text type="supporting">
              {p.name}: <b className="tabular-nums">{formatValue ? formatValue(Number(p.value), String(p.name)) : String(p.value)}</b>
            </Text>
          </HStack>
        ))}
      </VStack>
    </div>
  );
}

/** Thẻ chứa một biểu đồ: tiêu đề, mô tả, vùng thao tác bên phải; hiện trạng thái rỗng khi chưa có dữ liệu. */
export function ChartCard({
  title,
  description,
  actions,
  isEmpty,
  emptyText = 'Chưa có dữ liệu để hiển thị',
  height = 260,
  children,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  isEmpty?: boolean;
  emptyText?: string;
  height?: number;
  children: ReactNode;
}) {
  return (
    <Card padding={5}>
      <VStack gap={4}>
        <HStack hAlign="between" vAlign="start" gap={3} wrap="wrap">
          <VStack gap={1}>
            <Heading level={3}>{title}</Heading>
            {description && <Text type="supporting">{description}</Text>}
          </VStack>
          {actions}
        </HStack>
        {isEmpty ? (
          <div className="flex items-center justify-center" style={{ height }}>
            <EmptyState title={emptyText} />
          </div>
        ) : (
          <div style={{ height }}>{children}</div>
        )}
      </VStack>
    </Card>
  );
}
