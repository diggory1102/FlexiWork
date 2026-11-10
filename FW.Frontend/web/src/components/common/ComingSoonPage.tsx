import { Construction } from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';

/** Trang giữ chỗ cho chức năng thuộc các cấp bậc sau trong kế hoạch (docs/PROGRESS.md). */
export function ComingSoonPage({ title, stage }: { title: string; stage: string }) {
  return (
    <>
      <PageHeader title={title} />
      <div className="card flex flex-col items-center px-6 py-16 text-center">
        <div className="flex h-14 w-14 items-center justify-center rounded-full bg-warning/10 text-warning">
          <Construction className="h-7 w-7" />
        </div>
        <p className="mt-4 font-medium">Chức năng đang được phát triển</p>
        <p className="mt-1 text-xs text-muted">Thuộc {stage} trong kế hoạch triển khai.</p>
      </div>
    </>
  );
}
