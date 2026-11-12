import { Card } from '@astryxdesign/core/Card';
import { EmptyState } from '@astryxdesign/core/EmptyState';
import { Icon } from '@astryxdesign/core/Icon';
import { Construction } from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';

/** Trang giữ chỗ cho chức năng thuộc các cấp bậc sau trong kế hoạch (docs/PROGRESS.md). */
export function ComingSoonPage({ title, stage }: { title: string; stage: string }) {
  return (
    <>
      <PageHeader title={title} />
      <Card padding={10}>
        <EmptyState
          icon={<Icon icon={Construction} size="lg" color="warning" />}
          title="Chức năng đang được phát triển"
          description={`Thuộc ${stage} trong kế hoạch triển khai.`}
        />
      </Card>
    </>
  );
}
