import type { ReactNode } from 'react';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Avatar } from './Layout';

/** Ô nhân viên trong bảng: ảnh đại diện + họ tên + mã (kèm dòng phụ như chi nhánh hoặc email). */
export function EmployeeCell({
  name,
  code,
  avatarUrl,
  sub,
  size = 36,
}: {
  name: string;
  code: string;
  avatarUrl?: string | null;
  /** Hiện sau mã nhân viên, ngăn cách bằng " · ". */
  sub?: ReactNode;
  size?: number;
}) {
  return (
    <HStack gap={3} vAlign="center">
      <Avatar name={name} url={avatarUrl} size={size} />
      <VStack gap={0.5} className="min-w-0">
        <Text weight="medium" maxLines={1}>
          {name}
        </Text>
        <Text type="supporting" maxLines={1}>
          <span className="font-mono">{code}</span>
          {sub ? <> · {sub}</> : null}
        </Text>
      </VStack>
    </HStack>
  );
}
