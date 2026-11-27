import type { ReactNode } from 'react';
import { Card } from '@astryxdesign/core/Card';
import { Layout, LayoutContent, LayoutFooter, LayoutHeader } from '@astryxdesign/core/Layout';
import { HStack, StackItem, VStack } from '@astryxdesign/core/Stack';
import { Heading } from '@astryxdesign/core/Text';

interface PageFrameProps {
  title: string;
  /** Dòng phụ có thông tin thật (thời điểm cập nhật, phạm vi dữ liệu…). */
  meta?: ReactNode;
  actions?: ReactNode;
  toolbar?: ReactNode;
  footer?: ReactNode;
  /**
   * `plain` (mặc định): trang nhiều thẻ. `card`: trang danh sách thoáng, một thẻ chứa tất cả.
   * `dense`: bảng làm việc hằng ngày, tràn hết vùng nội dung, đầu trang cố định.
   */
  layout?: 'plain' | 'card' | 'dense';
  children: ReactNode;
}

export function PageFrame({ title, meta, actions, toolbar, footer, layout = 'plain', children }: PageFrameProps) {
  const dense = layout === 'dense';
  const header = (
    <HStack gap={dense ? 2 : 3} vAlign="center" wrap="wrap">
      <StackItem size="fill">
        {dense ? (
          <HStack gap={3} vAlign="center" wrap="wrap">
            <Heading level={1}>{title}</Heading>
            {meta}
          </HStack>
        ) : (
          <VStack gap={1}>
            <Heading level={1}>{title}</Heading>
            {meta}
          </VStack>
        )}
      </StackItem>
      {actions}
    </HStack>
  );

  if (dense) {
    return (
      <Layout
        height="fill"
        padding={0}
        header={
          <LayoutHeader hasDivider padding={0}>
            <VStack gap={2} padding={3} className="bg-surface">
              {header}
              {toolbar}
            </VStack>
          </LayoutHeader>
        }
        content={<LayoutContent padding={0}>{children}</LayoutContent>}
        footer={
          footer ? (
            <LayoutFooter padding={0}>
              <div className="bg-surface">{footer}</div>
            </LayoutFooter>
          ) : undefined
        }
      />
    );
  }

  if (layout === 'card') {
    return (
      <VStack padding={5}>
        <Card padding={0}>
          <VStack gap={4} padding={5}>
            {header}
            {toolbar}
          </VStack>
          <div className="overflow-hidden px-5">{children}</div>
          <div className="px-2 pb-2">{footer}</div>
        </Card>
      </VStack>
    );
  }

  return (
    <VStack gap={5} padding={5}>
      {header}
      {toolbar}
      {children}
      {footer}
    </VStack>
  );
}
