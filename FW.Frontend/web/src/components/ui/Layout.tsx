import type { ReactNode } from 'react';
import { Avatar as AstryxAvatar } from '@astryxdesign/core/Avatar';
import { Heading, Text } from '@astryxdesign/core/Text';
import { HStack, VStack } from '@astryxdesign/core/Stack';

/** Tiêu đề trang (h1) + mô tả + nhóm nút thao tác bên phải. */
export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <HStack gap={3} wrap="wrap" vAlign="end" hAlign="between" className="mb-5">
      <VStack gap={1}>
        <Heading level={1}>{title}</Heading>
        {description && (
          <Text type="supporting" as="p">
            {description}
          </Text>
        )}
      </VStack>
      {actions && (
        <HStack gap={2} wrap="wrap" vAlign="center">
          {actions}
        </HStack>
      )}
    </HStack>
  );
}

/** Thanh bộ lọc phía trên bảng: tự xuống dòng trên màn hình hẹp. */
export function Toolbar({ children }: { children: ReactNode }) {
  return (
    <HStack gap={2} wrap="wrap" vAlign="end" padding={3} className="border-b border-border">
      {children}
    </HStack>
  );
}

/** Kích thước avatar Astryx hỗ trợ — làm tròn kích thước yêu cầu về giá trị gần nhất. */
const AVATAR_SIZES = [16, 20, 24, 32, 36, 40, 48, 60, 64, 72, 96, 128] as const;
type AvatarSize = (typeof AVATAR_SIZES)[number];

/** Ảnh đại diện; chưa có ảnh hoặc ảnh lỗi → chữ cái đầu họ tên (Astryx tự xử lý). */
export function Avatar({ name, url, size = 32 }: { name: string; url?: string | null; size?: number }) {
  const nearest = AVATAR_SIZES.reduce<AvatarSize>((best, s) => (Math.abs(s - size) < Math.abs(best - size) ? s : best), 32);
  return <AstryxAvatar name={name} src={url ?? undefined} size={nearest} tooltip={false} />;
}
