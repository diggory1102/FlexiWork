import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { ChartTooltip, axisProps, chartColors } from '@/components/charts/ChartCard';
import { formatDayMonth } from '@/lib/dates';
import type { DashboardTrend } from '@/types/reports';

/** Mỗi ngày tách các ca đã xếp thành bốn phần không chồng nhau (đi muộn nằm trong số có mặt). */
const series = [
  { key: 'Đúng giờ', color: 'var(--color-data-categorical-teal)' },
  { key: 'Đi muộn', color: 'var(--color-data-categorical-orange)' },
  { key: 'Vắng mặt', color: 'var(--color-data-categorical-pink)' },
  { key: 'Nghỉ / chưa đến ca', color: 'var(--color-border)' },
] as const;

type Key = (typeof series)[number]['key'];
type Day = { day: string } & Record<Key, number>;

const toDays = (trend: DashboardTrend[]): Day[] =>
  trend.map((t) => ({
    day: formatDayMonth(t.date),
    'Đúng giờ': Math.max(0, t.attendedShifts - t.lateShifts),
    'Đi muộn': t.lateShifts,
    'Vắng mặt': t.absentShifts,
    'Nghỉ / chưa đến ca': Math.max(0, t.scheduledShifts - t.attendedShifts - t.absentShifts),
  }));

/** Đoạn cột: chỉ bo góc đoạn trên cùng, viền màu nền thẻ tạo khe giữa các đoạn; đoạn bằng 0 không vẽ. */
const segment = (index: number) => (props: unknown) => {
  const { x, y, width, height, fill, payload } = props as { x: number; y: number; width: number; height: number; fill: string; payload: Day };
  if (!height) return <g />;
  const isTop = series.slice(index + 1).every((s) => payload[s.key] === 0);
  const r = isTop ? Math.min(6, height) : 0;
  const path = `M${x},${y + height} V${y + r} Q${x},${y} ${x + r},${y} H${x + width - r} Q${x + width},${y} ${x + width},${y + r} V${y + height} Z`;
  return <path d={path} fill={fill} stroke="var(--color-background-card)" strokeWidth={2} />;
};

/** Biểu đồ cột chồng theo ngày: chiều cao cột bằng số ca đã xếp. Lấp đầy chiều cao của khung chứa. */
export function TrendChart({ trend }: { trend: DashboardTrend[] }) {
  return (
    <VStack gap={3} className="h-full">
      <HStack gap={3} wrap="wrap">
        {series.map((s) => (
          <HStack key={s.key} gap={1.5} vAlign="center">
            <span className="size-2.5 shrink-0 rounded-full" style={{ background: s.color }} />
            <Text type="supporting">{s.key}</Text>
          </HStack>
        ))}
      </HStack>
      <div className="min-h-0 flex-1">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={toDays(trend)} margin={{ top: 8, right: 0, left: -28, bottom: 0 }}>
            <CartesianGrid stroke={chartColors.grid} vertical={false} strokeDasharray="3 4" />
            <XAxis dataKey="day" {...axisProps} />
            <YAxis allowDecimals={false} {...axisProps} />
            <Tooltip content={<ChartTooltip />} cursor={{ fill: 'var(--color-overlay-hover)' }} />
            {series.map((s, i) => (
              <Bar key={s.key} dataKey={s.key} stackId="shifts" fill={s.color} maxBarSize={30} shape={segment(i)} isAnimationActive={false} />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>
    </VStack>
  );
}
