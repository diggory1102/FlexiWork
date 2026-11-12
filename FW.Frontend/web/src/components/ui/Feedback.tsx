import type { ReactNode } from 'react';
import { Badge as AstryxBadge } from '@astryxdesign/core/Badge';
import { Banner } from '@astryxdesign/core/Banner';
import { EmptyState as AstryxEmptyState } from '@astryxdesign/core/EmptyState';
import { Icon } from '@astryxdesign/core/Icon';
import { Spinner as AstryxSpinner } from '@astryxdesign/core/Spinner';
import { Inbox } from 'lucide-react';

export function Spinner({ size = 'md', label }: { size?: 'sm' | 'md' | 'lg' | 'xl'; label?: string }) {
  return <AstryxSpinner size={size} label={label} />;
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <AstryxEmptyState
      title={title}
      description={description}
      icon={<Icon icon={Inbox} size="lg" color="secondary" />}
      actions={action}
      isCompact
    />
  );
}

export type BadgeTone = 'gray' | 'blue' | 'green' | 'amber' | 'red' | 'purple';

const badgeVariants = {
  gray: 'neutral',
  blue: 'blue',
  green: 'green',
  amber: 'orange',
  red: 'red',
  purple: 'purple',
} as const;

/** Nhãn trạng thái / vai trò (bọc Badge của Astryx, giữ bảng màu theo ý nghĩa nghiệp vụ). */
export function Badge({ tone = 'gray', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <AstryxBadge variant={badgeVariants[tone]} label={children} />;
}

/** Thông báo trong form: lỗi từ API, hướng dẫn, kết quả. Không hiển thị khi rỗng. */
export function Notice({
  status = 'error',
  title,
  description,
}: {
  status?: 'info' | 'warning' | 'error' | 'success';
  title?: string | null;
  description?: ReactNode;
}) {
  if (!title) return null;
  return <Banner status={status} title={title} description={description} collapsible={false} />;
}

/** Lỗi tổng của form (thông báo từ API). */
export function FormError({ message }: { message?: string | null }) {
  return <Notice status="error" title={message} />;
}
