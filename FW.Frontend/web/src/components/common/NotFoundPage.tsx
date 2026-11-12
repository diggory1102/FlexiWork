import { Button } from '@astryxdesign/core/Button';
import { Center } from '@astryxdesign/core/Center';
import { EmptyState } from '@astryxdesign/core/EmptyState';
import { Icon } from '@astryxdesign/core/Icon';
import { SearchX } from 'lucide-react';

export function NotFoundPage() {
  return (
    <Center axis="both" padding={6} className="min-h-screen bg-body">
      <EmptyState
        icon={<Icon icon={SearchX} size="lg" color="secondary" />}
        title="404 — Không tìm thấy trang"
        description="Đường dẫn không tồn tại hoặc đã được thay đổi."
        actions={<Button label="Quay về trang chủ" variant="primary" href="/" />}
        headingLevel={1}
      />
    </Center>
  );
}
