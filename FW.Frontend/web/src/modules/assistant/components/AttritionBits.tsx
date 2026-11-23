import { Icon } from '@astryxdesign/core/Icon';
import { HStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react';
import { Badge } from '@/components/ui/Feedback';
import type { AttritionLevel } from '@/types/assistant';
import { ATTRITION_LEVEL } from '../assistantUi';

export function AttritionLevelBadge({ level }: { level: AttritionLevel }) {
  const meta = ATTRITION_LEVEL[level];
  return <Badge tone={meta.tone}>{meta.label}</Badge>;
}

/** Điểm so với tuần trước: mũi tên đỏ khi tăng (xấu đi), xanh khi giảm; "mới" nếu chưa có tuần trước để so. */
export function ScoreDelta({ delta }: { delta: number | null }) {
  if (delta == null) return <Text type="supporting">mới</Text>;
  if (delta === 0) {
    return (
      <HStack gap={1} vAlign="center">
        <Icon icon={Minus} size="sm" color="secondary" />
        <Text type="supporting">0</Text>
      </HStack>
    );
  }
  const worse = delta > 0;
  return (
    <HStack gap={1} vAlign="center">
      <Icon icon={worse ? ArrowUpRight : ArrowDownRight} size="sm" color={worse ? 'error' : 'success'} />
      <span className="text-sm tabular-nums" style={{ color: worse ? 'var(--color-error)' : 'var(--color-success)' }}>
        {worse ? '+' : ''}
        {delta}
      </span>
    </HStack>
  );
}

export const ATTRITION_DISCLAIMER =
  'Đây là tín hiệu tham khảo dựa trên thay đổi hành vi so với chính nhân viên đó (đi muộn, vắng mặt, xin nghỉ…). Không dùng để kỷ luật hay kết luận người đó sẽ nghỉ việc — hãy trò chuyện để hiểu và hỗ trợ.';
