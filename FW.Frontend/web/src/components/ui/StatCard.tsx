import type { ComponentType, ReactNode, SVGProps } from 'react';
import { Card } from '@astryxdesign/core/Card';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';

export type StatTone = 'accent' | 'success' | 'warning' | 'error' | 'secondary' | 'purple';

/** Thẻ một chỉ số: số lớn + nhãn + biểu tượng màu theo ý nghĩa (dùng cho dashboard và báo cáo). */
export function StatCard({
  icon,
  label,
  value,
  hint,
  tone = 'accent',
}: {
  icon: ComponentType<SVGProps<SVGSVGElement>>;
  label: string;
  value: ReactNode;
  /** Dòng phụ dưới nhãn (VD: "trên 24 ca đã có kết quả"). */
  hint?: ReactNode;
  tone?: StatTone;
}) {
  return (
    <Card padding={4}>
      <VStack gap={1}>
        <HStack hAlign="between" vAlign="center" gap={2}>
          <Heading level={3} className="tabular-nums">
            {value}
          </Heading>
          <Icon icon={icon} color={tone} />
        </HStack>
        <Text type="supporting">{label}</Text>
        {hint && <Text type="supporting">{hint}</Text>}
      </VStack>
    </Card>
  );
}
